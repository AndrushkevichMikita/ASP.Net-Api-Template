using ApiTemplate.Infrastructure.EventBus.Internal;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Serilog;

namespace ApiTemplate.Infrastructure.EventBus.Retry
{
    /// <summary>
    /// Implementation of IDlqConsumer for monitoring DLQ messages.
    /// This consumer logs DLQ messages and can trigger alerts.
    /// </summary>
    internal sealed class DlqConsumer : IDlqConsumer, IDisposable
    {
        private readonly IRetryConsumerFactory _retryConsumerFactory;
        private readonly IOptions<RetryConfiguration> _retryConfiguration;
        private readonly ILogger _logger;
        private IConsumer<string, string> _consumer;
        private Task _consumerTask;
        private CancellationTokenSource _cancellationTokenSource;
        private bool _disposed = false;

        public DlqConsumer(
            IRetryConsumerFactory retryConsumerFactory,
            IOptions<RetryConfiguration> retryConfiguration,
            ILogger logger)
        {
            _retryConsumerFactory = retryConsumerFactory;
            _retryConfiguration = retryConfiguration;
            _logger = logger.ForContext<DlqConsumer>();
        }

        public Task StartAsync(string dlqTopic, CancellationToken cancellationToken)
        {
            if (_consumer != null)
            {
                _logger.Warning("DLQ consumer is already running");
                return Task.CompletedTask;
            }

            _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _consumer = _retryConsumerFactory.CreateConsumer(dlqTopic);
            _consumer.Subscribe(dlqTopic);

            _consumerTask = Task.Run(
                async () => await ConsumeDlqMessagesAsync(dlqTopic, _cancellationTokenSource.Token),
                _cancellationTokenSource.Token);

            _logger.Information("Started DLQ consumer for topic {DlqTopic}", dlqTopic);

            return Task.CompletedTask;
        }

        private async Task ConsumeDlqMessagesAsync(string dlqTopic, CancellationToken cancellationToken)
        {
            _logger.Information("DLQ consumer started for {DlqTopic}", dlqTopic);

            try
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

                        await ProcessDlqMessageAsync(consumeResult, dlqTopic);
                    }
                    catch (ConsumeException ex)
                    {
                        if (ex.Error.IsFatal)
                        {
                            _logger.Fatal(ex, "Fatal consume error in DLQ consumer for {DlqTopic}", dlqTopic);
                            break;
                        }

                        _logger.Warning(ex, "Consume error in DLQ consumer for {DlqTopic}: {Error}", dlqTopic, ex.Error.Reason);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.Information("DLQ consumer for {DlqTopic} cancelled", dlqTopic);
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Unexpected error in DLQ consumer for {DlqTopic}", dlqTopic);
                    }
                }
            }
            finally
            {
                _logger.Information("DLQ consumer for {DlqTopic} stopped", dlqTopic);
            }
        }

        private async Task ProcessDlqMessageAsync(ConsumeResult<string, string> consumeResult, string dlqTopic)
        {
            try
            {
                var envelope = RetryMessageEnvelope.FromJson(consumeResult.Message.Value);

                if (envelope == null)
                {
                    _logger.Error(
                        "Failed to deserialize DLQ envelope from {DlqTopic}, Partition: {Partition}, Offset: {Offset}",
                        dlqTopic,
                        consumeResult.Partition,
                        consumeResult.Offset);

                    _consumer.StoreOffset(consumeResult);
                    _consumer.Commit(consumeResult);
                    return;
                }

                // Log DLQ message with full context
                _logger.Error(
                    "DLQ MESSAGE RECEIVED - Original Topic: {OriginalTopic}, Key: {Key}, Retry Count: {RetryCount}, " +
                    "Error Reason: {ErrorReason}, First Received: {FirstReceivedAt}, Last Retry: {LastRetryAt}, " +
                    "Partition: {Partition}, Offset: {Offset}",
                    envelope.OriginalTopic,
                    envelope.OriginalKey,
                    envelope.RetryCount,
                    envelope.ErrorReason,
                    envelope.FirstReceivedAt,
                    envelope.LastRetryAt,
                    consumeResult.Partition,
                    consumeResult.Offset);

                // TODO: Add alerting/monitoring integration here
                // - Send to monitoring system (e.g., Prometheus, DataDog)
                // - Send email/Slack notification
                // - Update metrics

                // Commit offset after processing
                _consumer.StoreOffset(consumeResult);
                _consumer.Commit(consumeResult);

                // Small delay to prevent overwhelming the system
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                _logger.Error(
                    ex,
                    "Error processing DLQ message from {DlqTopic}, Partition: {Partition}, Offset: {Offset}",
                    dlqTopic,
                    consumeResult.Partition,
                    consumeResult.Offset);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            try
            {
                _cancellationTokenSource?.Cancel();
                _consumerTask?.Wait(TimeSpan.FromSeconds(10));
                _consumer?.Close();
                _consumer?.Dispose();
                _cancellationTokenSource?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error disposing DLQ consumer");
            }
        }
    }
}
