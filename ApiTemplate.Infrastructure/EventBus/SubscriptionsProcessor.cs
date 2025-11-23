using ApiTemplate.Domain.Events;
using ApiTemplate.Infrastructure.EventBus.Internal;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;

namespace ApiTemplate.Infrastructure.EventBus
{
    internal sealed class SubscriptionsProcessor : ISubscriptionsProcessor, IDisposable
    {
        private readonly IEventBusSubscriptionsManager _subscriptionManager;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger _logger;
        private CancellationTokenSource _shutdownTokenSource = new CancellationTokenSource();

        public SubscriptionsProcessor(
            IEventBusSubscriptionsManager subscriptionManager,
            IServiceScopeFactory serviceScopeFactory,
            ILogger logger)
        {
            _subscriptionManager = subscriptionManager;
            _serviceScopeFactory = serviceScopeFactory;

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

                        transactionMarkedAsFailed |= !LogResult(message, eventMetadata, commitAction, transactionMarkedAsFailed, integrationEventResult);
                    }
                    catch (KafkaException ex) when (ex.Error.Code == ErrorCode.Local_State)
                    {
                        // Handle state errors
                        _logger.Warning(ex, "Kafka state error during event processing: {eventMessageId}", eventMetadata.PartitionKey);
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
                    }
                }
            }
        }

        private bool LogResult(
            string message,
            EventMetadata eventMetadata,
            Action commitAction,
            bool transactionMarkedAsFailed,
            IntegrationEventResult integrationEventResult)
        {
            var eventToLog = integrationEventResult.SaveEventToLog ? message : "event not logged";

            if (integrationEventResult.IsSuccessful)
            {
                string logMessage;

                if (!integrationEventResult.HandlingCommitWithHandler)
                {
                    logMessage = "event handled successfully";
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

                return false;
            }
        }
    }
}

