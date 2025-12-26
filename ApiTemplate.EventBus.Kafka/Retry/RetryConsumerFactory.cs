using ApiTemplate.EventBus.Kafka.Internal;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace ApiTemplate.EventBus.Kafka.Retry
{
    /// <summary>
    /// Factory for creating retry consumer instances.
    /// </summary>
    internal sealed class RetryConsumerFactory : IRetryConsumerFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IOptions<ConsumerConfig> _baseConsumerConfig;
        private readonly IOptions<RetryConfiguration> _retryConfiguration;
        private readonly ILogger _logger;

        public RetryConsumerFactory(
            IServiceProvider serviceProvider,
            IOptions<ConsumerConfig> baseConsumerConfig,
            IOptions<RetryConfiguration> retryConfiguration,
            ILogger logger)
        {
            _serviceProvider = serviceProvider;
            _baseConsumerConfig = baseConsumerConfig;
            _retryConfiguration = retryConfiguration;
            _logger = logger;
        }

        public IRetryConsumer Create()
        {
            // Create a new RetryConsumer instance using DI
            return ActivatorUtilities.CreateInstance<RetryConsumer>(_serviceProvider);
        }

        /// <summary>
        /// Creates a Kafka consumer for a specific retry topic with custom group ID.
        /// </summary>
        public IConsumer<string, string> CreateConsumer(string retryTopic)
        {
            // WARNING: CLOSURE BUG FIX
            // CRITICAL: We MUST build GroupId and ClientId as INDEPENDENT local variables
            // BEFORE creating the config object or the partition assignment handler closure.
            // If we build these values from 'config' after it's created, or reference 'config' 
            // in the closure, all consumers created in quick succession will share the same 
            // 'config' reference, causing all partition assignment handlers to use the 
            // GroupId/ClientId from the LAST consumer created.
            // 
            // By building these values FIRST as independent strings (not from config),
            // each consumer's closure gets its own copy, ensuring each consumer checks 
            // committed offsets with its own correct GroupId.
            string groupId = $"{_retryConfiguration.Value.RetryConsumerGroupId}-{retryTopic}";
            var autoOffsetReset = AutoOffsetReset.Earliest;
            
            // Generate ClientId using the independent groupId to ensure consistency
            string hostId = Environment.MachineName;
            string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
            string clientId = $"{groupId}-{hostId}-{uniqueId}";
            
            // NOW create config using the independent variables
            // CRITICAL: Create a NEW ConsumerConfig to avoid any reference sharing issues
            var baseConfig = _baseConsumerConfig.Value;
            var config = new ConsumerConfig
            {
                // Copy all base config properties explicitly
                BootstrapServers = baseConfig.BootstrapServers,
                BrokerVersionFallback = baseConfig.BrokerVersionFallback,
                ApiVersionFallbackMs = baseConfig.ApiVersionFallbackMs,
                SaslMechanism = baseConfig.SaslMechanism,
                SecurityProtocol = baseConfig.SecurityProtocol,
                SaslUsername = baseConfig.SaslUsername,
                SaslPassword = baseConfig.SaslPassword,
                SessionTimeoutMs = baseConfig.SessionTimeoutMs,
                HeartbeatIntervalMs = baseConfig.HeartbeatIntervalMs,
                // Override with retry-specific values using our independent variables
                GroupId = groupId, // CRITICAL: Use the independent variable, not from baseConfig
                AutoOffsetReset = autoOffsetReset,
                EnableAutoOffsetStore = false, // Manual StoreOffset() - we only store after successful processing
                EnableAutoCommit = false, // Manual commit for retry topics - ensures immediate persistence after successful republish
                ClientId = clientId, // CRITICAL: Use the independent variable
            };

            // Log the GroupId and ClientId that will be used BEFORE creating the consumer
            // CRITICAL: Verify config.GroupId matches our independent variable
            if (config.GroupId != groupId)
            {
                _logger.Error($"CRITICAL: Config.GroupId mismatch! Expected: {groupId}, Actual: {config.GroupId}. This indicates a config copy issue.");
                throw new InvalidOperationException($"ConsumerConfig.GroupId mismatch. Expected: {groupId}, Actual: {config.GroupId}");
            }
            if (config.ClientId != clientId)
            {
                _logger.Error($"CRITICAL: Config.ClientId mismatch! Expected: {clientId}, Actual: {config.ClientId}. This indicates a config copy issue.");
                throw new InvalidOperationException($"ConsumerConfig.ClientId mismatch. Expected: {clientId}, Actual: {config.ClientId}");
            }
            
            _logger.Debug($"Creating retry consumer for topic {retryTopic} with GroupId: {groupId}, ClientId: {clientId}, Verified Config.GroupId: {config.GroupId}, Config.ClientId: {config.ClientId}");
            
            var consumer = new ConsumerBuilder<string, string>(config)
                .SetErrorHandler(KafkaErrorHandler.HandleError(_logger))
                .SetPartitionsAssignedHandler((consumer, partitions) =>
                {
                    // Log both the captured values and what the consumer actually has
                    var actualMemberId = consumer.MemberId ?? "not-assigned-yet";
                    _logger.Debug($"Retry consumer assigned partitions: {string.Join(", ", partitions.Select(p => $"{p.Topic}-{p.Partition}"))}. Expected GroupId: {groupId}, ClientId: {clientId}, Actual MemberId: {actualMemberId}");
                    
                    // Validate MemberId starts with expected GroupId
                    if (!string.IsNullOrEmpty(actualMemberId) && actualMemberId != "not-assigned-yet" && !actualMemberId.StartsWith(groupId))
                    {
                        _logger.Error($"CRITICAL: Partition assignment handler - MemberId does not start with expected GroupId! Expected: {groupId}, MemberId: {actualMemberId}");
                    }
                    
                    // Log the starting offset for each partition to verify offset persistence
                    foreach (var partition in partitions)
                    {
                        try
                        {
                            _logger.Debug($"Checking committed offset for partition {partition.Topic}-{partition.Partition}, GroupId: {groupId}, Consumer MemberId: {consumer.MemberId ?? "not-assigned"}");
                            
                            var committed = consumer.Committed(new[] { partition }, TimeSpan.FromSeconds(10));
                            
                            _logger.Debug($"Committed() returned for partition {partition.Topic}-{partition.Partition}: Count={committed?.Count ?? 0}, IsNull={committed == null}");
                            
                            if (committed != null && committed.Count > 0)
                            {
                                var committedOffset = committed[0];
                                var offsetValue = committedOffset.Offset.Value;
                                
                                _logger.Debug($"Committed offset details for {partition.Topic}-{partition.Partition}: Offset={offsetValue}");
                                
                                // Check if offset is valid (not -1 or -1001 which indicate no committed offset)
                                // -1 = Offset.Invalid (no committed offset)
                                // -1001 = Offset.Unset (no committed offset, different representation)
                                if (offsetValue >= 0)
                                {
                                    _logger.Debug($"Retry consumer partition {partition.Topic}-{partition.Partition} starting from committed offset: {offsetValue}");
                                }
                                else
                                {
                                    _logger.Warning($"Retry consumer partition {partition.Topic}-{partition.Partition} has no committed offset (offset value: {offsetValue}). Will start from: {autoOffsetReset}, GroupId: {groupId}");
                                }
                            }
                            else
                            {
                                _logger.Warning($"Retry consumer partition {partition.Topic}-{partition.Partition} has no committed offset (committed is null or empty). Will start from: {autoOffsetReset}, GroupId: {groupId}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, $"Exception while checking committed offset for partition {partition.Topic}-{partition.Partition}, GroupId: {groupId}");
                        }
                    }
                })
                .SetPartitionsRevokedHandler((_, partitions) =>
                {
                    _logger.Debug($"Retry consumer partitions revoked: {string.Join(", ", partitions.Select(p => $"{p.Topic}-{p.Partition}"))}. GroupId: {groupId}");
                })
                .Build();
            
            return consumer;
        }
    }
}


