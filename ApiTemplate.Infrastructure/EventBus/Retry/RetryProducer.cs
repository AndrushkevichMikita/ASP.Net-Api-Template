using ApiTemplate.Infrastructure.EventBus.Internal;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Serilog;

namespace ApiTemplate.Infrastructure.EventBus.Retry
{
    /// <summary>
    /// Implementation of IRetryProducer for publishing messages to retry topics and DLQ.
    /// </summary>
    internal sealed class RetryProducer : IRetryProducer, IDisposable
    {
        private readonly IProducerFactory _producerFactory;
        private readonly IOptions<RetryConfiguration> _retryConfiguration;
        private readonly ILogger _logger;
        private IProducer<string, string> _producer;
        private readonly object _producerLock = new object();
        private bool _disposed = false;

        public RetryProducer(
            IProducerFactory producerFactory,
            IOptions<RetryConfiguration> retryConfiguration,
            ILogger logger)
        {
            _producerFactory = producerFactory;
            _retryConfiguration = retryConfiguration;
            _logger = logger.ForContext<RetryProducer>();
        }

        private IProducer<string, string> GetProducer()
        {
            if (_producer == null)
            {
                lock (_producerLock)
                {
                    if (_producer == null)
                    {
                        _producer = _producerFactory.Create();
                        _logger.Debug("Retry producer created");
                    }
                }
            }

            return _producer;
        }

        public async Task<bool> PublishToRetryTopicAsync(
            string originalTopic,
            string originalPayload,
            string originalKey,
            int retryCount,
            string errorReason,
            CancellationToken cancellationToken)
        {
            try
            {
                var config = _retryConfiguration.Value;
                var retryLevel = config.GetNextRetryLevel(retryCount);
                var retryTopic = config.GetRetryTopicName(originalTopic, retryLevel);
                var delay = config.GetRetryDelay(retryLevel);
                var retryAt = DateTimeOffset.UtcNow.Add(delay);

                var envelope = RetryMessageEnvelope.Create(
                    originalTopic: originalTopic,
                    originalPayload: originalPayload,
                    originalKey: originalKey,
                    retryCount: retryCount + 1,
                    errorReason: errorReason);

                envelope.RetryAt = retryAt;
                envelope.FirstReceivedAt = DateTimeOffset.UtcNow.AddSeconds(-delay.TotalSeconds * (retryCount + 1)); // Approximate

                var message = new Message<string, string>
                {
                    Key = originalKey,
                    Value = envelope.ToJson(),
                    Headers = new Headers
                    {
                        { "retry-level", System.Text.Encoding.UTF8.GetBytes(retryLevel.ToString()) },
                        { "retry-count", System.Text.Encoding.UTF8.GetBytes((retryCount + 1).ToString()) },
                        { "retry-at", System.Text.Encoding.UTF8.GetBytes(retryAt.ToString("O")) },
                        { "original-topic", System.Text.Encoding.UTF8.GetBytes(originalTopic) }
                    }
                };

                var producer = GetProducer();
                var deliveryResult = await producer.ProduceAsync(
                    retryTopic,
                    message,
                    cancellationToken);

                _logger.Information(
                    "Message published to retry topic {RetryTopic} (level: {RetryLevel}, retry count: {RetryCount}, retry at: {RetryAt}). Original topic: {OriginalTopic}, Key: {Key}",
                    retryTopic,
                    retryLevel,
                    retryCount + 1,
                    retryAt,
                    originalTopic,
                    originalKey);

                return deliveryResult.Status == PersistenceStatus.Persisted;
            }
            catch (KafkaException ex)
            {
                _logger.Error(
                    ex,
                    "Failed to publish message to retry topic. Original topic: {OriginalTopic}, Retry count: {RetryCount}, Error: {Error}",
                    originalTopic,
                    retryCount,
                    ex.Message);

                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(
                    ex,
                    "Unexpected error publishing message to retry topic. Original topic: {OriginalTopic}, Retry count: {RetryCount}",
                    originalTopic,
                    retryCount);

                return false;
            }
        }

        public async Task<bool> PublishToRetryTopicAsync(
            RetryMessageEnvelope envelope,
            CancellationToken cancellationToken)
        {
            // When we receive an envelope, it already has the correct retry count and RetryAt timestamp
            // We should publish it directly without incrementing
            try
            {
                var config = _retryConfiguration.Value;
                var retryLevel = config.GetNextRetryLevel(envelope.RetryCount - 1); // RetryCount is 1-based, GetNextRetryLevel expects 0-based
                var retryTopic = config.GetRetryTopicName(envelope.OriginalTopic, retryLevel);

                var message = new Message<string, string>
                {
                    Key = envelope.OriginalKey,
                    Value = envelope.ToJson(),
                    Headers = new Headers
                    {
                        { "retry-level", System.Text.Encoding.UTF8.GetBytes(retryLevel.ToString()) },
                        { "retry-count", System.Text.Encoding.UTF8.GetBytes(envelope.RetryCount.ToString()) },
                        { "retry-at", System.Text.Encoding.UTF8.GetBytes(envelope.RetryAt.ToString("O")) },
                        { "original-topic", System.Text.Encoding.UTF8.GetBytes(envelope.OriginalTopic) }
                    }
                };

                var producer = GetProducer();
                var deliveryResult = await producer.ProduceAsync(
                    retryTopic,
                    message,
                    cancellationToken);

                _logger.Information(
                    "Message published to retry topic {RetryTopic} (level: {RetryLevel}, retry count: {RetryCount}, retry at: {RetryAt}). Original topic: {OriginalTopic}, Key: {Key}",
                    retryTopic,
                    retryLevel,
                    envelope.RetryCount,
                    envelope.RetryAt,
                    envelope.OriginalTopic,
                    envelope.OriginalKey);

                return deliveryResult.Status == PersistenceStatus.Persisted;
            }
            catch (KafkaException ex)
            {
                _logger.Error(
                    ex,
                    "Failed to publish envelope to retry topic. Original topic: {OriginalTopic}, Retry count: {RetryCount}, Error: {Error}",
                    envelope.OriginalTopic,
                    envelope.RetryCount,
                    ex.Message);

                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(
                    ex,
                    "Unexpected error publishing envelope to retry topic. Original topic: {OriginalTopic}, Retry count: {RetryCount}",
                    envelope.OriginalTopic,
                    envelope.RetryCount);

                return false;
            }
        }

        public async Task<bool> PublishToDlqAsync(
            string originalTopic,
            string originalPayload,
            string originalKey,
            int retryCount,
            string errorReason,
            CancellationToken cancellationToken)
        {
            try
            {
                var config = _retryConfiguration.Value;
                var dlqTopic = config.GetRetryTopicName(originalTopic, RetryLevel.DLQ);

                var envelope = RetryMessageEnvelope.Create(
                    originalTopic: originalTopic,
                    originalPayload: originalPayload,
                    originalKey: originalKey,
                    retryCount: retryCount,
                    errorReason: errorReason);

                envelope.RetryAt = DateTimeOffset.UtcNow; // DLQ messages are not retried

                var message = new Message<string, string>
                {
                    Key = originalKey,
                    Value = envelope.ToJson(),
                    Headers = new Headers
                    {
                        { "retry-level", System.Text.Encoding.UTF8.GetBytes(RetryLevel.DLQ.ToString()) },
                        { "retry-count", System.Text.Encoding.UTF8.GetBytes(retryCount.ToString()) },
                        { "original-topic", System.Text.Encoding.UTF8.GetBytes(originalTopic) },
                        { "dlq-reason", System.Text.Encoding.UTF8.GetBytes(errorReason ?? "Max retries exceeded") }
                    }
                };

                var producer = GetProducer();
                var deliveryResult = await producer.ProduceAsync(
                    dlqTopic,
                    message,
                    cancellationToken);

                _logger.Warning(
                    "Message published to DLQ {DlqTopic}. Original topic: {OriginalTopic}, Key: {Key}, Retry count: {RetryCount}, Error: {ErrorReason}",
                    dlqTopic,
                    originalTopic,
                    originalKey,
                    retryCount,
                    errorReason);

                return deliveryResult.Status == PersistenceStatus.Persisted;
            }
            catch (KafkaException ex)
            {
                _logger.Error(
                    ex,
                    "Failed to publish message to DLQ. Original topic: {OriginalTopic}, Error: {Error}",
                    originalTopic,
                    ex.Message);

                return false;
            }
            catch (Exception ex)
            {
                _logger.Error(
                    ex,
                    "Unexpected error publishing message to DLQ. Original topic: {OriginalTopic}",
                    originalTopic);

                return false;
            }
        }

        public async Task<bool> PublishToDlqAsync(
            RetryMessageEnvelope envelope,
            CancellationToken cancellationToken)
        {
            return await PublishToDlqAsync(
                envelope.OriginalTopic,
                envelope.OriginalPayload,
                envelope.OriginalKey,
                envelope.RetryCount,
                envelope.ErrorReason,
                cancellationToken);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                lock (_producerLock)
                {
                    if (!_disposed)
                    {
                        _producer?.Flush(TimeSpan.FromSeconds(10));
                        _producer?.Dispose();
                        _producer = null;
                        _disposed = true;
                        _logger.Debug("Retry producer disposed");
                    }
                }
            }
        }
    }
}
