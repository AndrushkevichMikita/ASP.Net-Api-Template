using ApiTemplate.Domain.Events;
using ApiTemplate.Infrastructure.EventBus.Internal;
using ApiTemplate.Infrastructure.EventBus.Retry;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Serilog;

namespace ApiTemplate.Infrastructure.EventBus
{
    internal sealed class SubscriptionsProcessor : ISubscriptionsProcessor, IDisposable
    {
        private readonly ILogger _logger;
        private readonly IRetryProducer _retryProducer;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IEventBusSubscriptionsManager _subscriptionManager;
        private readonly RetryConfiguration _retryConfiguration;
        private CancellationTokenSource _shutdownTokenSource = new();

        public SubscriptionsProcessor( 
            IEventBusSubscriptionsManager subscriptionManager,
            IServiceScopeFactory serviceScopeFactory,
            IRetryProducer retryProducer,
            ILogger logger,
            IOptions<RetryConfiguration> retryConfiguration)
        {
            _retryProducer = retryProducer;
            _subscriptionManager = subscriptionManager;
            _serviceScopeFactory = serviceScopeFactory;
            _retryConfiguration = retryConfiguration.Value;

            using (var scope = _serviceScopeFactory.CreateScope())
            {
                if (scope.ServiceProvider.GetService<IEventDispatcher>() is null)
                {
                    throw new InvalidOperationException($"Required type {typeof(IEventDispatcher)} not registered in DI container");
                }
            }

            _logger = logger;

            AppDomain.CurrentDomain.ProcessExit += (sender, e) => HandleShutdown();
        }

        public async Task ProcessAsync(
            string message,
            EventMetadata eventMetadata,
            IReadOnlyCollection<SubscriptionInfo> subscriptions,
            Action commitAction,
            CancellationToken cancellationToken)
        {
            try
            {
                var typedSubscriptions = subscriptions
                    .Where(s => !s.IsDynamic)
                    .ToList();

                if (typedSubscriptions.Count > 0)
                {
                    await ProcessTypedSubscriptionsAsync(
                        typedSubscriptions: typedSubscriptions,
                        message: message,
                        eventMetadata,
                        commitAction: commitAction,
                        cancellationToken: cancellationToken);
                }
            }
            catch (Exception ex)
            {
                var pii = new
                {
                    eventMessage = message,
                };

                _logger
                    .ForContext("pii.eventMessage", pii.eventMessage)
                    .Error(
                        ex,
                        "general event processing error: {topic} {eventMessageId}, {{pii.eventMessage}}",
                        eventMetadata.TopicName,
                        eventMetadata.PartitionKey);
            }
        }

        public void Dispose()
        {
            var sts = Interlocked.Exchange(ref _shutdownTokenSource, null);

            if (sts is not null)
            {
                sts.Cancel();
                sts.Dispose();
            }

            GC.SuppressFinalize(this);
        }

        private void HandleShutdown()
        {
            _logger.Information("Gracefully shutting down Kafka subscriptions processor");

            var sts = Interlocked.CompareExchange(ref _shutdownTokenSource, null, null);
            sts?.Cancel();
        }

        private bool TryDeserializeIntegrationEvent(
            string topic,
            string message,
            string messageId,
            out Type eventType,
            out IntegrationEvent integrationEvent)
        {
            try
            {
                eventType = _subscriptionManager.GetEventTypeByName(topic);
                integrationEvent = (IntegrationEvent)JsonConvert.DeserializeObject(message, eventType, KafkaSerialization.Settings);

                return true;
            }
            catch (Exception ex)
            {
                eventType = default;
                integrationEvent = default;

                var pii = new
                {
                    eventMessage = message,
                };

                _logger
                    .ForContext("pii.eventMessage", pii.eventMessage)
                    .Error(
                        ex,
                        "TypedSubscription unable to deserialize for {topic}, {eventMessageId}, {{pii.eventMessage}}",
                        topic,
                        messageId);
            }

            return false;
        }

        private async Task ProcessTypedSubscriptionsAsync(
            IReadOnlyCollection<SubscriptionInfo> typedSubscriptions,
            string message,
            EventMetadata eventMetadata,
            Action commitAction,
            CancellationToken cancellationToken)
        {
            if (TryDeserializeIntegrationEvent(eventMetadata.TopicName, message, eventMetadata.PartitionKey, out var eventType, out var integrationEvent))
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var transactionMarkedAsFailed = false;
                int currentRetryCount = GetRetryCountFromHeaders(eventMetadata);
                
                // Set retry count on the integration event so handlers can access it
                integrationEvent.RetryAttempts = currentRetryCount;
                
                if (currentRetryCount > 0)
                {
                    _logger.Information(
                        "Processing retry attempt {RetryCount} for event {EventId} from topic {Topic}",
                        currentRetryCount,
                        integrationEvent.Id,
                        eventMetadata.TopicName);
                }

                foreach (var handlerType in typedSubscriptions.Select(sub => sub.HandlerType))
                {
                    try
                    {
                        var integrationEventResult = await scope.ServiceProvider.GetRequiredService<IEventDispatcher>().DispatchAsync(
                            @event: integrationEvent,
                            eventType: eventType,
                            handlerType: handlerType,
                            commitAction: commitAction,
                            cancellationToken: cancellationToken);

                        transactionMarkedAsFailed |= !await LogResultWithRetryAsync(
                            message,
                            eventMetadata,
                            commitAction,
                            transactionMarkedAsFailed,
                            integrationEventResult,
                            currentRetryCount,
                            cancellationToken);
                    }
                    catch (KafkaException ex) when (ex.Error.Code == ErrorCode.Local_State)
                    {
                        // Handle state errors
                        _logger.Warning(ex, "Kafka state error during event processing: {eventMessageId}", eventMetadata.PartitionKey);
                        // Send to retry on state errors
                        await HandleProcessingFailureAsync(
                            message,
                            eventMetadata,
                            commitAction,
                            currentRetryCount,
                            ex.Message,
                            integrationEvent?.Id,
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        var pii = new
                        {
                            eventMessage = message,
                        };

                        _logger
                            .ForContext("pii.eventMessage", pii.eventMessage)
                            .Error(
                                ex,
                                "TypedSubscription {subscriptionType} processing error: {eventMessageId}, {{pii.eventMessage}}",
                                handlerType,
                                eventMetadata.PartitionKey);

                        // Send to retry on unexpected exceptions
                        await HandleProcessingFailureAsync(
                            message,
                            eventMetadata,
                            commitAction,
                            currentRetryCount,
                            ex.Message,
                            integrationEvent?.Id,
                            cancellationToken);
                    }
                }
            }
        }

        private int GetRetryCountFromHeaders(EventMetadata eventMetadata)
        {
            // Extract retry count from message headers if available
            // When messages are republished from retry topics, they include X-Retry-Count header
            if (eventMetadata.Headers != null)
            {
                try
                {
                    var retryCountHeader = eventMetadata.Headers.FirstOrDefault(h => h.Key == "X-Retry-Count");
                    if (retryCountHeader != null)
                    {
                        var headerBytes = retryCountHeader.GetValueBytes();
                        if (headerBytes != null && headerBytes.Length >= 4)
                        {
                            var retryCount = BitConverter.ToInt32(headerBytes, 0);
                            _logger.Debug("Extracted retry count {RetryCount} from message headers", retryCount);
                            return retryCount;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to parse retry count from header. Defaulting to 0");
                }
            }

            // Default to 0 for new messages (first attempt)
            return 0;
        }

        private async Task<bool> LogResultWithRetryAsync(
            string message,
            EventMetadata eventMetadata,
            Action commitAction,
            bool transactionMarkedAsFailed,
            IntegrationEventResult integrationEventResult,
            int currentRetryCount,
            CancellationToken cancellationToken)
        {
            var eventToLog = integrationEventResult.SaveEventToLog ? message : "event not logged";

            if (integrationEventResult.IsSuccessful)
            {
                string logMessage;

                if (!integrationEventResult.HandlingCommitWithHandler)
                {
                    logMessage = "event handled successfully";
                    // Commit offset after successful processing
                    commitAction();
                }
                else
                {
                    logMessage = "event sent to async handler, status unknown";
                }

                var pii = new
                {
                    eventMessage = eventToLog,
                };

                _logger
                    .ForContext("pii.eventMessage", pii.eventMessage)
                    .Information(
                        logMessage + ": {eventMessageId}, {{pii.eventMessage}}",
                        eventMetadata.PartitionKey);

                return true;
            }
            else
            {
                var pii = new
                {
                    eventMessage = eventToLog,
                };

                _logger
                    .ForContext("pii.eventMessage", pii.eventMessage)
                    .Error(
                        integrationEventResult.Exception,
                        "failed to handle event successfully: {eventMessageId}, {{pii.eventMessage}}",
                        eventMetadata.PartitionKey);

                // Handle failure with retry logic
                await HandleProcessingFailureAsync(
                    message,
                    eventMetadata,
                    commitAction,
                    currentRetryCount,
                    integrationEventResult.Exception?.Message ?? integrationEventResult.Message ?? "Unknown error",
                    null, // Event ID will be extracted from message if needed
                    cancellationToken);

                return false;
            }
        }

        /// <summary>
        /// Handles processing failures by sending to retry topic or DLQ, then committing offset.
        /// CRITICAL: Offset is committed AFTER successfully sending to retry/DLQ, not after business logic.
        /// </summary>
        private async Task HandleProcessingFailureAsync(
            string message,
            EventMetadata eventMetadata,
            Action commitAction,
            int currentRetryCount,
            string errorReason,
            Guid? eventId,
            CancellationToken cancellationToken)
        {
            if (!_retryConfiguration.IsRetryEnabled)
            {
                _logger.Warning(
                    "Retry is disabled. Committing offset without retry for failed message: {eventMessageId}",
                    eventMetadata.PartitionKey);
                commitAction();
                return;
            }

            try
            {
                var nextRetryCount = currentRetryCount + 1;

                // Check if we've exceeded max retry attempts
                if (nextRetryCount > _retryConfiguration.MaxRetryAttempts)
                {
                    // Send to DLQ
                    _logger.Warning(
                        "Max retry attempts ({MaxRetryAttempts}) exceeded. Sending to DLQ. OriginalTopic: {OriginalTopic}, EventId: {EventId}",
                        _retryConfiguration.MaxRetryAttempts,
                        eventMetadata.TopicName,
                        eventId);

                    var dlqEnvelope = RetryMessageEnvelope.Create(
                        originalTopic: eventMetadata.TopicName,
                        originalKey: eventMetadata.PartitionKey,
                        originalPayload: message,
                        retryCount: nextRetryCount,
                        retryDelay: TimeSpan.Zero, // No delay for DLQ
                        errorReason: $"Max retries exceeded: {errorReason}",
                        originalEventId: eventId);

                    var dlqSuccess = await _retryProducer.PublishToDlqAsync(dlqEnvelope, cancellationToken);

                    if (dlqSuccess)
                    {
                        // Commit offset only after successful DLQ publish
                        commitAction();
                        _logger.Information(
                            "Successfully sent message to DLQ and committed offset. OriginalTopic: {OriginalTopic}, EventId: {EventId}",
                            eventMetadata.TopicName,
                            eventId);
                    }
                    else
                    {
                        _logger.Error(
                            "Failed to send message to DLQ. Offset will not be committed. OriginalTopic: {OriginalTopic}, EventId: {EventId}",
                            eventMetadata.TopicName,
                            eventId);
                        // Offset not committed - message will be reprocessed
                    }
                }
                else
                {
                    // Send to retry topic
                    var retryLevel = _retryConfiguration.GetNextRetryLevel(currentRetryCount);
                    var retryDelay = _retryConfiguration.GetRetryDelay(retryLevel);
                    var retryTopic = _retryConfiguration.GetRetryTopicName(eventMetadata.TopicName, retryLevel);

                    _logger.Information(
                        "Sending failed message to retry topic {RetryTopic}. RetryCount: {RetryCount}, RetryDelay: {RetryDelay}, OriginalTopic: {OriginalTopic}, EventId: {EventId}",
                        retryTopic,
                        nextRetryCount,
                        retryDelay,
                        eventMetadata.TopicName,
                        eventId);

                    var retryEnvelope = RetryMessageEnvelope.Create(
                        originalTopic: eventMetadata.TopicName,
                        originalKey: eventMetadata.PartitionKey,
                        originalPayload: message,
                        retryCount: nextRetryCount,
                        retryDelay: retryDelay,
                        errorReason: errorReason,
                        originalEventId: eventId);

                    var retrySuccess = await _retryProducer.PublishToRetryTopicAsync(retryEnvelope, cancellationToken);

                    if (retrySuccess)
                    {
                        // Commit offset only after successful retry topic publish
                        commitAction();
                        _logger.Information(
                            "Successfully sent message to retry topic {RetryTopic} and committed offset. RetryCount: {RetryCount}, OriginalTopic: {OriginalTopic}, EventId: {EventId}",
                            retryTopic,
                            nextRetryCount,
                            eventMetadata.TopicName,
                            eventId);
                    }
                    else
                    {
                        _logger.Error(
                            "Failed to send message to retry topic {RetryTopic}. Offset will not be committed. OriginalTopic: {OriginalTopic}, EventId: {EventId}",
                            retryTopic,
                            eventMetadata.TopicName,
                            eventId);
                        // Offset not committed - message will be reprocessed
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error(
                    ex,
                    "Error handling processing failure. Offset will not be committed. OriginalTopic: {OriginalTopic}, EventId: {EventId}",
                    eventMetadata.TopicName,
                    eventId);
                // Offset not committed - message will be reprocessed
            }
        }
    }
}

