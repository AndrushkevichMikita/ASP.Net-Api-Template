#pragma warning disable S107 // Methods should not have too many parameters

using System.Reflection;
using System.Text;
using App.Metrics;
using App.Metrics.Meter;
using App.Metrics.Timer;
using ApiTemplate.Application.Interfaces;
using ApiTemplate.Domain.Events;
using ApiTemplate.Infrastructure.EventBus.Retry;
using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Serilog;

namespace ApiTemplate.Infrastructure.EventBus
{
    internal sealed class KafkaClient : IEventBus, IDisposable
    {
        private static readonly TimerOptions _processAsyncTimer = new()
        {
            Name = "KafkaConsumer.ProcessAsync",
            MeasurementUnit = Unit.Events,
            DurationUnit = TimeUnit.Seconds,
            RateUnit = TimeUnit.Seconds,
        };

        private static readonly MeterOptions _errorsPerSecond = new()
        {
            MeasurementUnit = Unit.Events,
            Name = "KafkaConsumer.Errors",
            RateUnit = TimeUnit.Seconds,
        };

        private static readonly TimerOptions _commitOffsetTimer = new()
        {
            MeasurementUnit = Unit.Events,
            Name = "KafkaConsumer.Offset.Commits",
            RateUnit = TimeUnit.Seconds,
            DurationUnit = TimeUnit.Seconds,
        };

        private readonly IAdminClient _adminClient;
        private readonly IEventBusSubscriptionsManager _subscriptionManager;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger _logger;
        private readonly IMetrics _metrics;
        private readonly IConsumerFactory _consumerFactory;
        private readonly ISubscriptionsProcessor _subscriptionsProcessor;
        private readonly KafkaConfiguration _kafkaConfiguration;
        private readonly Timer _diagnosticTimer;
        private readonly IServiceProvider _serviceProvider;
        private readonly HashSet<string> _retryConsumersStarted = new();

        private readonly CancellationTokenSource _stoppingTokenSource = new();
        private object _disposeLock = new();

        public KafkaClient(
            IAdminClient adminClient,
            IOptions<KafkaConfiguration> configuration,
            IEventPublisher eventPublisher,
            ILogger logger,
            IEventBusSubscriptionsManager subscriptionManager,
            ISubscriptionsProcessor subscriptionsProcessor,
            IMetrics metrics,
            IConsumerFactory consumerFactory,
            IServiceProvider serviceProvider)
        {
            _kafkaConfiguration = configuration.Value;
            _eventPublisher = eventPublisher;
            _subscriptionManager = subscriptionManager;
            _subscriptionsProcessor = subscriptionsProcessor;

            _adminClient = adminClient;
            _logger = logger;
            _metrics = metrics;
            _consumerFactory = consumerFactory;
            _serviceProvider = serviceProvider;
            _logger.Debug("ConsumerFactory instance hash code: {HashCode}", _consumerFactory.GetHashCode());

            _diagnosticTimer = new Timer(
                _ => DiagnosticCheck(),
                null,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30));
        }

        public void Dispose()
        {
            var lockObj = Interlocked.Exchange(ref _disposeLock, null);
            if (lockObj is null)
            {
                return;
            }

            _logger.Information("Shutting down Kafka client");
            _diagnosticTimer?.Dispose();
            _stoppingTokenSource.Cancel();
            _stoppingTokenSource.Dispose();
        }

        public void Subscribe<T, TH>(int numberOfConsumers)
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            if (_kafkaConfiguration.IsConsumptionEnabled)
            {
                _subscriptionManager.AddSubscription<T, TH>();
                DoInternalSubscribe<T>(numberOfConsumers);
                
                // Automatically start retry consumers for this topic
                StartRetryConsumersForTopic<T>();
            }
        }

        private void StartRetryConsumersForTopic<T>() where T : IntegrationEvent
        {
            try
            {
                var wholeTopic = typeof(T).GetCustomAttribute<KafkaTopicAttribute>();
                if (wholeTopic == null)
                {
                    _logger.Warning("Cannot start retry consumers: KafkaTopicAttribute not found on {EventType}", typeof(T).Name);
                    return;
                }

                var topic = wholeTopic.Name;
                
                // Only start retry consumers once per topic
                lock (_retryConsumersStarted)
                {
                    if (_retryConsumersStarted.Contains(topic))
                    {
                        _logger.Debug("Retry consumers already started for topic {Topic}", topic);
                        return;
                    }

                    _retryConsumersStarted.Add(topic);
                }

                // Start retry consumers asynchronously (fire and forget)
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Use a scope to get IRetryPipelineService
                        using var scope = _serviceProvider.CreateScope();
                        var retryPipelineService = scope.ServiceProvider.GetService<IRetryPipelineService>();
                        
                        if (retryPipelineService == null)
                        {
                            _logger.Warning("IRetryPipelineService not available. Retry consumers will not be started for topic {Topic}", topic);
                            return;
                        }

                        _logger.Information("Starting retry consumers for topic {Topic}", topic);
                        await retryPipelineService.StartRetryConsumersAsync(topic, _stoppingTokenSource.Token);
                        await retryPipelineService.StartDlqConsumerAsync(topic, _stoppingTokenSource.Token);
                        _logger.Information("Successfully started retry consumers for topic {Topic}", topic);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to start retry consumers for topic {Topic}", topic);
                        // Remove from set so we can retry later
                        lock (_retryConsumersStarted)
                        {
                            _retryConsumersStarted.Remove(topic);
                        }
                    }
                }, _stoppingTokenSource.Token);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error starting retry consumers for event type {EventType}", typeof(T).Name);
            }
        }

        public void Unsubscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            _subscriptionManager.RemoveSubscription<T, TH>();
        }

        public Task<bool> PublishAsync(
            IntegrationEvent integrationEvent,
            CancellationToken cancellationToken)
            => _eventPublisher.PublishAsync(integrationEvent, cancellationToken);

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!await PublishAsync(new HealthCheckEvent(), cancellationToken))
                {
                    return HealthCheckResult.Degraded("Publish failed");
                }

                return CheckConsumerHealth();
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Degraded(ex.Message, ex.InnerException ?? ex);
            }
        }

        public async Task ProcessEventAsync(
            string topic,
            string message,
            string messageId,
            Action commitAction,
            CancellationToken cancellationToken)
        {
            await ProcessEventAsync(message, new EventMetadata(topic, messageId), commitAction, cancellationToken);
        }

        internal async Task ProcessEventAsync(
            string message,
            EventMetadata eventMetadata,
            Action commitAction,
            CancellationToken cancellationToken)
        {
            _logger.Information("processing event, id: {eventMessageId}", eventMetadata.PartitionKey);

            var subscriptions = _subscriptionManager.GetHandlersForEvent(eventMetadata.TopicName);

            await _subscriptionsProcessor.ProcessAsync(
                message: message,
                eventMetadata,
                subscriptions: subscriptions,
                commitAction: commitAction,
                cancellationToken: cancellationToken);
        }

        private HealthCheckResult CheckConsumerHealth()
        {
            var metadataResult = _adminClient.GetMetadata(TimeSpan.FromSeconds(10));

            if (metadataResult.Topics.Count == 0)
            {
                return HealthCheckResult.Degraded("Unable to access partitions on kafka server");
            }

            var existingTopics = metadataResult.Topics.ConvertAll(t => t.Topic);
            var subscribedTopics = _subscriptionManager.GetSubscribedTopics();
            var missingTopics = subscribedTopics
                .Where(t => !existingTopics.Contains(t))
                .Distinct()
                .ToList();

            var missingTopicStatus = new StringBuilder();

            foreach (var topic in missingTopics)
            {
                missingTopicStatus
                    .Append(topic)
                    .Append(" is missing. ");
            }

            return missingTopics.Count == 0
                ? HealthCheckResult.Healthy("Published event with no error. No missing topics found.")
                : HealthCheckResult.Degraded(missingTopicStatus.ToString());
        }

        private void StoreTopicOffset(IConsumer<string, string> consumer, ConsumeResult<string, string> consumeResult)
        {
            if (consumer == null || consumeResult == null)
            {
                _logger.Warning("Skipping offset storage - consumer or consumeResult is null");
                return;
            }

            if (consumer.Handle.IsInvalid)
            {
                _logger.Warning("Skipping offset storage for topic {Topic} - consumer is disposed or invalid", consumeResult.Topic);
                return;
            }

            TryStoreOffsetWithRetry(consumer, consumeResult);
        }

        private void TryStoreOffsetWithRetry(IConsumer<string, string> consumer, ConsumeResult<string, string> consumeResult)
        {
            try
            {
                var metricTags = new MetricTags("topic", consumeResult.Topic);

                const int maxRetries = 3;
                int retryCount = 0;
                Exception lastException = null;

                _logger.Debug(
                    "Attempting to store offset for topic {Topic}, Partition: {Partition}, Offset: {Offset}",
                    consumeResult.Topic,
                    consumeResult.Partition,
                    consumeResult.Offset);

                while (retryCount <= maxRetries)
                {
                    try
                    {
                        using (_metrics.Measure.Timer.Time(_commitOffsetTimer, metricTags))
                        {
                            consumer.StoreOffset(consumeResult);
                        }

                        return;
                    }
                    catch (KafkaException kex) when (
                        kex.Error.Code == ErrorCode.Local_Transport ||
                        kex.Error.Code == ErrorCode.Local_AllBrokersDown ||
                        kex.Error.Code == ErrorCode.Local_State)
                    {
                        lastException = kex;
                        retryCount++;

                        if (retryCount <= maxRetries)
                        {
                            _logger.Warning(
                                kex,
                                "Retrying offset storage for topic {Topic}, Partition: {Partition}, Offset: {Offset} after error. Attempt {RetryCount}/{MaxRetries}. Error: {ErrorCode}, Reason: {Reason}",
                                consumeResult.Topic,
                                consumeResult.Partition,
                                consumeResult.Offset,
                                retryCount,
                                maxRetries,
                                kex.Error.Code,
                                kex.Error.Reason);

                            Thread.Sleep(50 * (1 << retryCount));
                        }
                    }
                    catch (Exception ex)
                    {
                        lastException = ex;
                        break;
                    }
                }

                LogOffsetStorageFailure(lastException, consumeResult, retryCount);
            }
            catch (Exception ex)
            {
                _logger.Error(
                    ex,
                    "Error in offset storage handling for topic {Topic}, Partition: {Partition}, Offset: {Offset}",
                    consumeResult?.Topic ?? "unknown",
                    consumeResult?.Partition.ToString() ?? "unknown",
                    consumeResult?.Offset.ToString() ?? "unknown");
            }
        }

        private void LogOffsetStorageFailure(Exception lastException, ConsumeResult<string, string> consumeResult, int retryCount)
        {
            if (lastException is KafkaException kafkaEx)
            {
                _logger.Error(
                    kafkaEx,
                    "Failed to store offset for topic {Topic}, Partition: {Partition}, Offset: {Offset} after {RetryCount} attempts. Error code: {ErrorCode}, Reason: {ErrorReason}, IsFatal: {IsFatal}",
                    consumeResult.Topic,
                    consumeResult.Partition,
                    consumeResult.Offset,
                    retryCount,
                    kafkaEx.Error.Code,
                    kafkaEx.Error.Reason,
                    kafkaEx.Error.IsFatal);
            }
            else if (lastException != null)
            {
                _logger.Error(
                    lastException,
                    "Unexpected error when storing offset for topic {Topic}, Partition: {Partition}, Offset: {Offset} after {RetryCount} attempts",
                    consumeResult.Topic,
                    consumeResult.Partition,
                    consumeResult.Offset,
                    retryCount);
            }
        }

        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
        private async Task ConsumeAndProcessAsync(
            IConsumer<string, string> consumer,
            CancellationToken cancellationToken)
        {
            try
            {
                var consumeResult = consumer.Consume(cancellationToken);

                if (consumeResult != null && _subscriptionManager.HasSubscriptionsForEvent(consumeResult.Topic))
                {
                    var metricTags = new MetricTags("topic", consumeResult.Topic);

                    try
                    {
                        using (_metrics.Measure.Timer.Time(_processAsyncTimer, metricTags))
                        {
                            var eventMetadata = new EventMetadata(
                                consumeResult.Topic,
                                consumeResult.Message.Key,
                                consumeResult.Partition.Value,
                                consumeResult.Offset.Value,
                                consumeResult.Message.Headers);

                            await ProcessEventAsync(
                                consumeResult.Message.Value,
                                eventMetadata,
                                () => StoreTopicOffset(
                                    consumer,
                                    consumeResult),
                                cancellationToken);

                            _logger.Information("event consumed, id: {consumeResultKey}", consumeResult.Message.Key);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Error processing event from topic {Topic}", consumeResult.Topic);
                        throw;
                    }
                }
            }
            catch (ConsumeException ex)
            {
                _metrics.Measure.Meter.Mark(
                    _errorsPerSecond,
                    1,
                    new MetricSetItem(
                        "type",
                        "ConsumeException"));

                _logger.Debug(
                    "Handling ConsumeException, IsFatal: {IsFatal}, Error: {ErrorCode}, Reason: {Reason}",
                    ex.Error.IsFatal,
                    ex.Error.Code,
                    ex.Error.Reason);

                if (ex.Error.IsFatal)
                {
                    _logger.Fatal(ex, "Fatal consume error detected");
                    return;
                }

                _logger.Error(ex, "event consumption error");
            }
            catch (KafkaException ex)
            {
                _metrics.Measure.Meter.Mark(
                    _errorsPerSecond,
                    1,
                    new MetricSetItem(
                        "type",
                        "KafkaException"));

                _logger.Debug(
                    "Handling KafkaException, IsFatal: {IsFatal}, Error: {ErrorCode}, Reason: {Reason}",
                    ex.Error.IsFatal,
                    ex.Error.Code,
                    ex.Error.Reason);

                if (ex.Error.IsFatal)
                {
                    _logger.Fatal(ex, "Fatal kafka error detected");
                    return;
                }

                _logger.Error(ex, "event kafka error");
            }
            catch (OperationCanceledException)
            {
                _metrics.Measure.Meter.Mark(
                    _errorsPerSecond,
                    1,
                    new MetricSetItem(
                        "type",
                        "OperationCanceledException"));

                _logger.Debug("Handling OperationCanceledException");
            }
            catch (Exception ex)
            {
                _metrics.Measure.Meter.Mark(
                    _errorsPerSecond,
                    1,
                    new MetricSetItem(
                        "type",
                        "GeneralException"));

                _logger.Debug("Handling general Exception: {ExceptionType}", ex.GetType().Name);
                _logger.Fatal(ex, "event fatal error");

                throw;
            }
        }

        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
        private void DoInternalSubscribe<T>(int numberOfConsumers)
        {
            var wholeTopic = typeof(T).GetCustomAttribute<KafkaTopicAttribute>();
            var topic = wholeTopic.Name;

            _ = Enumerable
                .Range(1, numberOfConsumers)
                .Select(_ => RunConsumerTask(topic, _stoppingTokenSource.Token))
                .ToList();

            Task RunConsumerTask(string topicName, CancellationToken cancellationToken)
            {
                return Task.Run(
                    async () =>
                    {
                        IConsumer<string, string> kafkaConsumer = null;
                        try
                        {
                            kafkaConsumer = CreateConsumer();
                            await RunConsumerProcessingLoop(kafkaConsumer, topicName, cancellationToken);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            // Expected during shutdown
                        }
                        catch (Exception ex)
                        {
                            _logger.Fatal(ex, "DoInternalSubscribe fatal error for topic {Topic}", topicName);
                        }
                        finally
                        {
                            CloseConsumer(kafkaConsumer, topicName);
                        }
                    },
                    cancellationToken);
            }

            IConsumer<string, string> CreateConsumer()
            {
                var consumer = _consumerFactory.Create();
                consumer.Subscribe(topic);
                return consumer;
            }
        }

        private async Task RunConsumerProcessingLoop(
            IConsumer<string, string> kafkaConsumer,
            string topicName,
            CancellationToken cancellationToken)
        {
            _logger.Information(
                "Starting new Kafka consumer for topic {Topic}",
                topicName);

            await Task.Delay(2000, cancellationToken);

            var isFirstMessage = true;
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await ProcessSingleMessage(
                    kafkaConsumer,
                    topicName,
                    isFirstMessage);

                isFirstMessage = result.IsStillFirstMessage;
            }

            _logger.Information("Closing Kafka consumer for topic {Topic} due to shutdown", topicName);
        }

        private async Task<(bool IsStillFirstMessage, int NewRetryCount)> ProcessSingleMessage(
            IConsumer<string, string> consumer,
            string topicName,
            bool isFirstMessage)
        {
            var currentRetryCount = 0;

            try
            {
                await ConsumeAndProcessAsync(consumer, _stoppingTokenSource.Token);

                if (isFirstMessage)
                {
                    isFirstMessage = false;
                    _logger.Information("Kafka consumer for topic {Topic} successfully processed first message", topicName);
                }
            }
            catch (KafkaException kex) when (kex.Error.Code == ErrorCode.Local_State && isFirstMessage)
            {
                var result = await HandleStartupStateException(kex, topicName, currentRetryCount);
                currentRetryCount = result;
            }
            catch (Exception ex) when (isFirstMessage)
            {
                var result = await HandleStartupException(ex, topicName, currentRetryCount);
                currentRetryCount = result;
            }

            return (isFirstMessage, currentRetryCount);
        }

        private async Task<int> HandleStartupStateException(KafkaException kex, string topicName, int startupRetryCount)
        {
            startupRetryCount++;

            var delayMs = Math.Min(1000 * (1 << Math.Min(startupRetryCount, 5)), 30000);

            _logger.Warning(
                kex,
                "New consumer encountered state error during startup for topic {Topic}. Retry #{RetryCount}, waiting {DelayMs}ms before retry...",
                topicName,
                startupRetryCount,
                delayMs);

            await Task.Delay(delayMs, _stoppingTokenSource.Token);

            return startupRetryCount;
        }

        private async Task<int> HandleStartupException(Exception ex, string topicName, int startupRetryCount)
        {
            startupRetryCount++;

            _logger.Warning(
                ex,
                "New consumer encountered error during startup for topic {Topic}. Retry #{RetryCount}, waiting before retry...",
                topicName,
                startupRetryCount);

            await Task.Delay(1000, _stoppingTokenSource.Token);

            return startupRetryCount;
        }

        private void CloseConsumer(IConsumer<string, string> consumer, string topicName)
        {
            if (consumer != null)
            {
                try
                {
                    consumer.Dispose();
                    _logger.Information("Successfully closed Kafka consumer for topic {Topic}", topicName);
                }
                catch (Exception closeEx)
                {
                    _logger.Error(closeEx, "Error closing Kafka consumer for topic {Topic}", topicName);
                }
            }
        }

        [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
        private void DiagnosticCheck()
        {
            try
            {
                _logger.Debug("Running diagnostic check. ConsumerFactory type: {Type}", _consumerFactory?.GetType().FullName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error during diagnostic check");
            }
        }
    }
}

