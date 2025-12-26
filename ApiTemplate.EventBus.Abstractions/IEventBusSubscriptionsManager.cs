using ApiTemplate.EventBus.Domain.Core.Events;

namespace ApiTemplate.EventBus.Abstractions
{
    public interface IEventBusSubscriptionsManager
    {
        event EventHandler<string> OnEventRemoved;

        bool IsEmpty { get; }

        IEnumerable<string> GetSubscribedTopics();

        void AddSubscription<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>;

        void RemoveSubscription<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>;

        bool HasSubscriptionsForEvent<T>()
            where T : IntegrationEvent;

        bool HasSubscriptionsForEvent(string eventName);

        Type GetEventTypeByName(string eventName);

        void Clear();

        IEnumerable<SubscriptionInfo> GetHandlersForEvent<T>()
            where T : IntegrationEvent;

        IReadOnlyCollection<SubscriptionInfo> GetHandlersForEvent(string eventName);

        string GetEventKey<T>();
    }
}