using ApiTemplate.Infrastructure.EventBus.Internal;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;

namespace ApiTemplate.Infrastructure.EventBus.Retry
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
        private IConsumer<string, string> _consumer;
        private IProducer<string, string> _producer;
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

            _logger.Information(
                "Starting retry consumer for topic {RetryTopic} -> {OriginalTopic} (Level: {RetryLevel})",
                retryTopic,
                originalTopic,
                retryLevel);

            try
            {
                // Create consumer with retry-specific group ID using RetryConsumerFactory
                _consumer = _retryConsumerFactory.CreateConsumer(retryTopic);
                _producer = _producerFactory.Create();
                _consumer.Subscribe(retryTopic);

                _logger.Information("Retry consumer subscribed to topic {RetryTopic}", retryTopic);

                await ProcessRetryMessagesAsync(retryTopic, originalTopic, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.Information("Retry consumer stopped due to cancellation");
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "Fatal error in retry consumer for topic {RetryTopic}", retryTopic);
                throw;
            }
        }

        private async Task ProcessRetryMessagesAsync(string retryTopic, string mainTopic, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var consumeResult = _consumer.Consume(cancellationToken);

                    if (consumeResult == null || consumeResult.Message == null)
                    {
                        continue;
                    }

                    _logger.Debug(
                        "Retry consumer received message from {RetryTopic}. Partition: {Partition}, Offset: {Offset}",
                        retryTopic,
                        consumeResult.Partition,
                        consumeResult.Offset);

                    // Deserialize retry envelope
                    RetryMessageEnvelope envelope;
                    try
                    {
                        envelope = RetryMessageEnvelope.FromJson(consumeResult.Message.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(
                            ex,
                            "Failed to deserialize retry envelope from {RetryTopic}. Partition: {Partition}, Offset: {Offset}",
                            retryTopic,
                            consumeResult.Partition,
                            consumeResult.Offset);

                        // Commit offset even if deserialization fails to avoid reprocessing
                        _consumer.StoreOffset(consumeResult);
                        continue;
                    }

                    // Check if it's time to retry (delay has passed)
                    var now = DateTimeOffset.UtcNow;
                    if (envelope.RetryAt > now)
                    {
                        var delay = envelope.RetryAt - now;
                        _logger.Debug(
                            "Retry delay not yet reached. Waiting {DelayMs}ms. RetryAt: {RetryAt}, Now: {Now}. " +
                            "Seeking back to reprocess later.",
                            delay.TotalMilliseconds,
                            envelope.RetryAt,
                            now);

                        // Seek back to this offset so we can reprocess it later
                        var topicPartition = new TopicPartition(consumeResult.Topic, consumeResult.Partition);
                        var seekOffset = new TopicPartitionOffset(topicPartition, consumeResult.Offset);
                        _consumer.Seek(seekOffset);

                        // Wait for the delay to pass (with a max wait to avoid blocking too long)
                        var waitTime = Math.Min((int)delay.TotalMilliseconds, 5000);
                        if (waitTime > 0)
                        {
                            await Task.Delay(waitTime, cancellationToken);
                        }

                        // Continue to next iteration to reprocess this message
                        continue;
                    }

                    // Republish to main topic
                    var republishSuccess = await RepublishToMainTopicAsync(envelope, mainTopic, cancellationToken);

                    if (republishSuccess)
                    {
                        // Commit offset only after successful republish
                        _consumer.StoreOffset(consumeResult);
                        _logger.Information(
                            "Successfully republished message from {RetryTopic} to {MainTopic}. RetryCount: {RetryCount}",
                            retryTopic,
                            mainTopic,
                            envelope.RetryCount);
                    }
                    else
                    {
                        _logger.Error(
                            "Failed to republish message from {RetryTopic} to {MainTopic}. RetryCount: {RetryCount}. Offset will not be committed.",
                            retryTopic,
                            mainTopic,
                            envelope.RetryCount);
                        // Offset not committed - message will be reprocessed
                    }
                }
                catch (ConsumeException ex)
                {
                    _logger.Error(
                        ex,
                        "Error consuming from retry topic {RetryTopic}. Error: {ErrorCode}, Reason: {Reason}",
                        retryTopic,
                        ex.Error.Code,
                        ex.Error.Reason);

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
                    _logger.Information("Retry consumer operation cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unexpected error in retry consumer for topic {RetryTopic}", retryTopic);
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
                _logger.Debug(
                    "Republishing message to main topic {MainTopic}. RetryCount: {RetryCount}",
                    mainTopic,
                    envelope.RetryCount);

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
                    _logger.Debug(
                        "Successfully republished to main topic {MainTopic}. Partition: {Partition}, Offset: {Offset}",
                        mainTopic,
                        result.Partition,
                        result.Offset);
                    return true;
                }
                else
                {
                    _logger.Warning(
                        "Failed to persist republished message to {MainTopic}. Status: {Status}",
                        mainTopic,
                        result.Status);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(
                    ex,
                    "Error republishing message to main topic {MainTopic}",
                    mainTopic);
                return false;
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
