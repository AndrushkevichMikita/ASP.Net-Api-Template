using Microsoft.Extensions.Options;
using Serilog;

namespace ApiTemplate.EventBus.Kafka.Retry
{
    /// <summary>
    /// Service that manages retry consumers and DLQ consumer for the retry pipeline.
    /// </summary>
    internal sealed class RetryPipelineService : IRetryPipelineService, IDisposable
    {
        private readonly RetryConfiguration _retryConfiguration;
        private readonly IRetryConsumerFactory _retryConsumerFactory;
        private readonly IDlqConsumer _dlqConsumer;
        private readonly ILogger _logger;
        private readonly List<(IRetryConsumer Consumer, Task Task)> _activeRetryConsumers = new();
        private readonly object _disposeLock = new object();
        private bool _disposed = false;

        public RetryPipelineService(
            IOptions<RetryConfiguration> retryConfiguration,
            IRetryConsumerFactory retryConsumerFactory,
            IDlqConsumer dlqConsumer,
            ILogger logger)
        {
            _retryConfiguration = retryConfiguration.Value;
            _retryConsumerFactory = retryConsumerFactory;
            _dlqConsumer = dlqConsumer;
            _logger = logger.ForContext<RetryPipelineService>();
        }

        public async Task StartRetryConsumersAsync(string mainTopic, CancellationToken cancellationToken = default)
        {
            if (!_retryConfiguration.IsRetryEnabled)
            {
                _logger.Debug("Retry pipeline is disabled. Skipping retry consumers for topic {MainTopic}", mainTopic);
                return;
            }

            _logger.Debug("Starting retry consumers for main topic {MainTopic}", mainTopic);

            // Start a consumer for each retry level
            var retryLevels = new[] { RetryLevel.Retry10Seconds, RetryLevel.Retry1Minute, RetryLevel.Retry5Minutes };
            foreach (var retryLevel in retryLevels)
            {
                var retryTopic = _retryConfiguration.GetRetryTopicName(mainTopic, retryLevel);
                var retryConsumer = _retryConsumerFactory.Create();
                
                var task = Task.Run(
                    async () =>
                    {
                        try
                        {
                            await retryConsumer.StartAsync(retryTopic, mainTopic, retryLevel, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.Debug("Retry consumer for {RetryTopic} stopped due to cancellation", retryTopic);
                        }
                        catch (Exception ex)
                        {
                            _logger.Fatal(ex, "Fatal error in retry consumer for {RetryTopic}", retryTopic);
                        }
                    },
                    cancellationToken);

                _activeRetryConsumers.Add((retryConsumer, task));
                _logger.Debug("Started retry consumer for {RetryTopic}", retryTopic);
            }
        }

        public async Task StartDlqConsumerAsync(string mainTopic, CancellationToken cancellationToken = default)
        {
            if (!_retryConfiguration.IsRetryEnabled)
            {
                _logger.Debug("Retry pipeline is disabled. Skipping DLQ consumer for topic {MainTopic}", mainTopic);
                return;
            }

            var dlqTopic = _retryConfiguration.GetRetryTopicName(mainTopic, RetryLevel.DLQ);
            _logger.Warning("Starting DLQ consumer for topic {DlqTopic}", dlqTopic);

            _ = Task.Run(
                async () =>
                {
                    try
                    {
                        await _dlqConsumer.StartAsync(dlqTopic, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Debug("DLQ consumer for {DlqTopic} stopped due to cancellation", dlqTopic);
                    }
                    catch (Exception ex)
                    {
                        _logger.Fatal(ex, "Fatal error in DLQ consumer for {DlqTopic}", dlqTopic);
                    }
                },
                cancellationToken);
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
                    _logger.Debug("Disposing RetryPipelineService");
                    
                    foreach (var (consumer, _) in _activeRetryConsumers)
                    {
                        try
                        {
                            consumer?.Dispose();
                        }
                        catch (Exception ex)
                        {
                            _logger.Error(ex, "Error disposing retry consumer");
                        }
                    }
                    
                    _activeRetryConsumers.Clear();
                    _dlqConsumer?.Dispose();
                    _disposed = true;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error disposing RetryPipelineService");
                }
            }
        }
    }
}


