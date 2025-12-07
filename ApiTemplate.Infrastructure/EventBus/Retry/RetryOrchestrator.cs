using ApiTemplate.Domain.Events;
using Microsoft.Extensions.Options;
using Serilog;

namespace ApiTemplate.Infrastructure.EventBus.Retry
{
    /// <summary>
    /// Orchestrates retry consumers and DLQ consumer for a given topic.
    /// This service manages the lifecycle of all retry-related consumers.
    /// </summary>
    public interface IRetryOrchestrator
    {
        /// <summary>
        /// Starts all retry consumers and DLQ consumer for a given base topic.
        /// </summary>
        /// <param name="baseTopic">Base topic name (e.g., "account_created").</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task StartRetryConsumersAsync(string baseTopic, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Implementation of IRetryOrchestrator.
    /// </summary>
    internal sealed class RetryOrchestrator : IRetryOrchestrator, IDisposable
    {
        private readonly IRetryConsumer _retryConsumer;
        private readonly IDlqConsumer _dlqConsumer;
        private readonly IOptions<RetryConfiguration> _retryConfiguration;
        private readonly ILogger _logger;
        private readonly HashSet<string> _startedTopics = new();
        private readonly object _lock = new object();
        private bool _disposed = false;

        public RetryOrchestrator(
            IRetryConsumer retryConsumer,
            IDlqConsumer dlqConsumer,
            IOptions<RetryConfiguration> retryConfiguration,
            ILogger logger)
        {
            _retryConsumer = retryConsumer;
            _dlqConsumer = dlqConsumer;
            _retryConfiguration = retryConfiguration;
            _logger = logger.ForContext<RetryOrchestrator>();
        }

        public Task StartRetryConsumersAsync(string baseTopic, CancellationToken cancellationToken)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException(nameof(RetryOrchestrator));
                }

                if (_startedTopics.Contains(baseTopic))
                {
                    _logger.Debug("Retry consumers for topic {BaseTopic} are already started", baseTopic);
                    return Task.CompletedTask;
                }

                var config = _retryConfiguration.Value;

                // Start retry consumers for each retry level
                var retry10sTopic = config.GetRetryTopicName(baseTopic, RetryLevel.Retry10Seconds);
                var retry1mTopic = config.GetRetryTopicName(baseTopic, RetryLevel.Retry1Minute);
                var retry5mTopic = config.GetRetryTopicName(baseTopic, RetryLevel.Retry5Minutes);
                var dlqTopic = config.GetRetryTopicName(baseTopic, RetryLevel.DLQ);

                _ = _retryConsumer.StartAsync(retry10sTopic, baseTopic, RetryLevel.Retry10Seconds, cancellationToken);
                _ = _retryConsumer.StartAsync(retry1mTopic, baseTopic, RetryLevel.Retry1Minute, cancellationToken);
                _ = _retryConsumer.StartAsync(retry5mTopic, baseTopic, RetryLevel.Retry5Minutes, cancellationToken);
                _ = _dlqConsumer.StartAsync(dlqTopic, cancellationToken);

                _startedTopics.Add(baseTopic);

                _logger.Information(
                    "Started retry consumers for base topic {BaseTopic}. Retry topics: {Retry10s}, {Retry1m}, {Retry5m}, DLQ: {Dlq}",
                    baseTopic,
                    retry10sTopic,
                    retry1mTopic,
                    retry5mTopic,
                    dlqTopic);
            }

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _startedTopics.Clear();
            }
        }
    }
}

