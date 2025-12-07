using ApiTemplate.Infrastructure.EventBus.Internal;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace ApiTemplate.Infrastructure.EventBus.Retry
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
            var baseConfig = _baseConsumerConfig.Value;
            var config = new ConsumerConfig(baseConfig)
            {
                GroupId = $"{_retryConfiguration.Value.RetryConsumerGroupId}-{retryTopic}",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoOffsetStore = false, // Manual offset commit
                EnableAutoCommit = false, // Manual commit
            };

            string hostId = Environment.MachineName;
            string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
            config.ClientId = $"{config.GroupId}-{hostId}-{uniqueId}";

            return new ConsumerBuilder<string, string>(config)
                .SetErrorHandler(KafkaErrorHandler.HandleError(_logger))
                .SetPartitionsAssignedHandler((_, partitions) =>
                {
                    _logger.Information(
                        "Retry consumer assigned partitions: {Partitions}",
                        string.Join(", ", partitions.Select(p => $"{p.Topic}-{p.Partition}")));
                })
                .SetPartitionsRevokedHandler((_, partitions) =>
                {
                    _logger.Information(
                        "Retry consumer partitions revoked: {Partitions}",
                        string.Join(", ", partitions.Select(p => $"{p.Topic}-{p.Partition}")));
                })
                .Build();
        }
    }
}
