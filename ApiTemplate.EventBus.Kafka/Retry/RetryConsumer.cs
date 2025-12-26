using ApiTemplate.EventBus.Kafka.Configuration;
using ApiTemplate.EventBus.Kafka.Internal;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;

namespace ApiTemplate.EventBus.Kafka.Retry
{
    /// <summary>
    /// Consumer that reads from retry topics and republishes messages to the main topic after delay.
    /// </summary>
    internal sealed class RetryConsumer : IRetryConsumer, IDisposable
    {
        private readonly IRetryConsumerFactory _retryConsumerFactory;
        private readonly IProducerFactory _producerFactory;
        private readonly RetryConfiguration _retryConfiguration;
        private readonly KafkaConfiguration _kafkaConfiguration;
        private readonly ILogger _logger;
        private IConsumer<string, string>? _consumer;
        private IProducer<string, string>? _producer;
        private string? _currentGroupId; // Store the GroupId for logging
        private readonly object _disposeLock = new object();
        private bool _disposed = false;

        public RetryConsumer(
            IRetryConsumerFactory retryConsumerFactory,
            IProducerFactory producerFactory,
            IOptions<RetryConfiguration> retryConfiguration,
            IOptions<KafkaConfiguration> kafkaConfiguration,
            ILogger logger)
        {
            _retryConsumerFactory = retryConsumerFactory;
            _producerFactory = producerFactory;
            _retryConfiguration = retryConfiguration.Value;
            _kafkaConfiguration = kafkaConfiguration.Value;
            _logger = logger.ForContext<RetryConsumer>();
        }

        public async Task StartAsync(string retryTopic, string originalTopic, RetryLevel retryLevel, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(retryTopic))
            {
                throw new ArgumentException("Retry topic cannot be null or empty", nameof(retryTopic));
            }

            if (string.IsNullOrWhiteSpace(originalTopic))
            {
                throw new ArgumentException("Original topic cannot be null or empty", nameof(originalTopic));
            }

            _logger.Debug($"Starting retry consumer for topic {retryTopic} -> {originalTopic} (Level: {retryLevel})");

            try
            {
                // Create consumer with retry-specific group ID using RetryConsumerFactory
                _currentGroupId = $"{_retryConfiguration.RetryConsumerGroupId}-{retryTopic}"; // Store expected GroupId for logging
                _consumer = _retryConsumerFactory.CreateConsumer(retryTopic);
                _producer = _producerFactory.Create();
                _consumer.Subscribe(retryTopic);

                _logger.Debug($"Retry consumer subscribed to topic {retryTopic} with expected GroupId: {_currentGroupId}, MemberId: {_consumer.MemberId ?? "not-assigned-yet"}");

                await ProcessRetryMessagesAsync(retryTopic, originalTopic, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.Debug("Retry consumer stopped due to cancellation");
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, $"Fatal error in retry consumer for topic {retryTopic}");
                throw;
            }
        }

        private async Task ProcessRetryMessagesAsync(string retryTopic, string mainTopic, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (_consumer == null)
                    {
                        _logger.Error("Consumer is null, cannot process messages");
                        break;
                    }

                    var consumeResult = _consumer.Consume(cancellationToken);

                    if (consumeResult == null || consumeResult.Message == null)
                    {
                        continue;
                    }

                    // Log with expected GroupId and MemberId (MemberId contains the GroupId as prefix, so we can validate)
                    var memberId = _consumer.MemberId ?? "unknown";
                    _logger.Debug($"Retry consumer received message from {retryTopic}. Partition: {consumeResult.Partition}, Offset: {consumeResult.Offset}, Expected GroupId: {_currentGroupId}, MemberId: {memberId}");
                    
                    // Validate that the MemberId contains the expected GroupId (MemberId format: GroupId-{hostId}-{uniqueId}-{guid})
                    if (!string.IsNullOrEmpty(memberId) && memberId != "unknown" && _currentGroupId != null && !memberId.StartsWith(_currentGroupId))
                    {
                        _logger.Error($"CRITICAL: Consumer MemberId does not start with expected GroupId! Expected GroupId prefix: {_currentGroupId}, MemberId: {memberId}. This indicates a closure bug or consumer reuse issue.");
                    }

                    // Deserialize retry envelope
                    RetryMessageEnvelope? envelope;
                    try
                    {
                        envelope = RetryMessageEnvelope.FromJson(consumeResult.Message.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to deserialize retry envelope from {retryTopic}. Partition: {consumeResult.Partition}, Offset: {consumeResult.Offset}");

                        // Commit offset even if deserialization fails to avoid reprocessing
                        TryCommitOffsetWithRetry(consumeResult);
                        continue;
                    }

                    if (envelope == null)
                    {
                        _logger.Error($"Failed to deserialize retry envelope (null result) from {retryTopic}. Partition: {consumeResult.Partition}, Offset: {consumeResult.Offset}");
                        TryCommitOffsetWithRetry(consumeResult);
                        continue;
                    }

                    // Republish to main topic
                    var republishSuccess = await RepublishToMainTopicAsync(envelope, mainTopic, cancellationToken);

                    if (republishSuccess)
                    {
                        // Commit offset only after successful republish
                        _logger.Debug($"Republish successful. About to commit offset for {retryTopic}. Partition: {consumeResult.Partition}, Offset: {consumeResult.Offset}");
                        TryCommitOffsetWithRetry(consumeResult);
                        _logger.Debug($"Successfully republished message from {retryTopic} to {mainTopic}. RetryCount: {envelope.RetryCount}");
                    }
                    else
                    {
                        _logger.Error($"Failed to republish message from {retryTopic} to {mainTopic}. RetryCount: {envelope.RetryCount}. Offset will not be committed.");
                        // Offset not committed - message will be reprocessed
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.Error(ex, $"Error consuming from retry topic {retryTopic}. Error: {ex.Error.Code}, Reason: {ex.Error.Reason}");

                    if (ex.Error.IsFatal)
                    {
                        _logger.Fatal("Fatal consume error in retry consumer. Stopping.");
                        break;
                    }

                    // Wait before retrying
                    await Task.Delay(1000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.Debug("Retry consumer operation cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, $"Unexpected error in retry consumer for topic {retryTopic}");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        private async Task<bool> RepublishToMainTopicAsync(
            RetryMessageEnvelope envelope,
            string mainTopic,
            CancellationToken cancellationToken)
        {
            try
            {
                if (_producer == null)
                {
                    _logger.Error("Producer is null, cannot republish message");
                    return false;
                }

                _logger.Debug($"Republishing message to main topic {mainTopic}. RetryCount: {envelope.RetryCount}");

                var message = new Message<string, string>
                {
                    Key = envelope.OriginalKey,
                    Value = envelope.OriginalPayload, // Original payload, not the envelope
                    Headers = new Headers
                    {
                        { "X-Retry-Count", BitConverter.GetBytes(envelope.RetryCount) },
                        { "X-Is-Retry", System.Text.Encoding.UTF8.GetBytes("true") },
                        { "X-Retry-From", System.Text.Encoding.UTF8.GetBytes(envelope.OriginalTopic) },
                    },
                };

                var result = await _producer.ProduceAsync(mainTopic, message, cancellationToken);

                if (result.Status == PersistenceStatus.Persisted)
                {
                    _logger.Debug($"Successfully republished to main topic {mainTopic}. Partition: {result.Partition}, Offset: {result.Offset}");
                    return true;
                }
                else
                {
                    _logger.Warning($"Failed to persist republished message to {mainTopic}. Status: {result.Status}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Error republishing message to main topic {mainTopic}");
                return false;
            }
        }

        /// <summary>
        /// Stores and commits offset with retry logic for retry topics.
        /// Retry topics use manual commits (EnableAutoCommit=false) to ensure immediate persistence
        /// after successful republish, avoiding reprocessing of already-handled messages.
        /// This ensures offsets are persisted even if Kafka is temporarily unavailable.
        /// </summary>
        private void TryCommitOffsetWithRetry(ConsumeResult<string, string> consumeResult)
        {
            _logger.Debug($"TryCommitOffsetWithRetry called for {consumeResult?.Topic ?? "null"}, Partition: {consumeResult?.Partition ?? -1}, Offset: {consumeResult?.Offset ?? -1}. Consumer null: {_consumer == null}, Handle invalid: {_consumer?.Handle.IsInvalid ?? true}");

            if (_consumer == null || consumeResult == null)
            {
                _logger.Warning("Skipping offset storage - consumer or consumeResult is null");
                return;
            }

            if (_consumer.Handle.IsInvalid)
            {
                _logger.Warning($"Skipping offset storage for topic {consumeResult.Topic} - consumer is disposed or invalid");
                return;
            }

            try
            {
                const int maxRetries = 3;
                int retryCount = 0;
                Exception? lastException = null;

                _logger.Debug($"Attempting to store and commit offset for retry topic {consumeResult.Topic}, Partition: {consumeResult.Partition}, Offset: {consumeResult.Offset}, GroupId: {_currentGroupId ?? "unknown"}, MemberId: {_consumer.MemberId ?? "unknown"}.");

                while (retryCount <= maxRetries)
                {
                    try
                    {
                        _logger.Debug($"Calling StoreOffset and Commit for {consumeResult.Topic}, Partition: {consumeResult.Partition}, Offset: {consumeResult.Offset}.");
                        
                        // Store and commit offset immediately for retry topics
                        // Retry topics have lower throughput, so manual commits are acceptable.
                        // This ensures we don't reprocess messages that were already successfully republished.
                        _consumer.StoreOffset(consumeResult);
                        _consumer.Commit(consumeResult);
                        _logger.Debug($"Successfully stored and committed offset for {consumeResult.Topic}, Partition: {consumeResult.Partition}, Offset: {consumeResult.Offset}.");
                        
                        return;
                    }
                    catch (KafkaException kex) when (
                        kex.Error.Code == ErrorCode.Local_Transport ||
                        kex.Error.Code == ErrorCode.Local_AllBrokersDown ||
                        kex.Error.Code == ErrorCode.Local_State)
                    {
                        lastException = kex;
                        retryCount++;

                        _logger.Warning(kex, $"KafkaException during offset storage for topic {consumeResult.Topic}, Partition: {consumeResult.Partition}, Offset: {consumeResult.Offset}. ErrorCode: {kex.Error.Code}, Reason: {kex.Error.Reason}. RetryCount: {retryCount}/{maxRetries}");

                        if (retryCount <= maxRetries)
                        {
                            var backoffMs = 50 * (1 << retryCount);
                            _logger.Debug($"Waiting {backoffMs}ms before retry {retryCount}/{maxRetries}");
                            Thread.Sleep(backoffMs); // Exponential backoff: 100ms, 200ms, 400ms
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Unexpected error storing offset for topic {consumeResult.Topic}, Partition: {consumeResult.Partition}, Offset: {consumeResult.Offset}. Exception type: {ex.GetType().Name}");
                        return;
                    }
                }

                // All retries exhausted
                if (lastException != null)
                {
                    _logger.Error(lastException, $"Failed to store offset after {maxRetries} retries for topic {consumeResult.Topic}, Partition: {consumeResult.Partition}, Offset: {consumeResult.Offset}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Unexpected error in TryCommitOffsetWithRetry for topic {consumeResult.Topic}, Partition: {consumeResult.Partition}, Offset: {consumeResult.Offset}");
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            lock (_disposeLock)
            {
                if (_disposed)
                {
                    return;
                }

                try
                {
                    _consumer?.Unsubscribe();
                    _consumer?.Close();
                    _consumer?.Dispose();
                    _producer?.Flush(TimeSpan.FromSeconds(10));
                    _producer?.Dispose();
                    _logger.Debug("RetryConsumer disposed");
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error disposing RetryConsumer");
                }
                finally
                {
                    _disposed = true;
                }
            }
        }
    }
}


