using System.Reflection;
using ApiTemplate.Domain.Events;

namespace ApiTemplate.Infrastructure.EventBus
{
    internal sealed class InMemoryEventBusSubscriptionsManager : IEventBusSubscriptionsManager
    {
        private readonly Dictionary<string, List<SubscriptionInfo>> _handlers =
            new Dictionary<string, List<SubscriptionInfo>>();

        private readonly Dictionary<string, Type> _eventTypes =
            new Dictionary<string, Type>();

        public event EventHandler<string> OnEventRemoved;

        public bool IsEmpty => _handlers.Keys.Count == 0;

        public IEnumerable<string> GetSubscribedTopics()
        {
            return _handlers.Select(x => x.Key);
        }

        public void Clear()
        {
            _handlers.Clear();
            _eventTypes.Clear();
        }

        public void AddSubscription<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = GetEventKey<T>();
            DoAddSubscription(typeof(TH), eventName, false);

            if (!_eventTypes.ContainsKey(eventName))
            {
                _eventTypes.Add(eventName, typeof(T));
            }
        }

        public void RemoveSubscription<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var handlerToRemove = FindSubscriptionToRemove<T, TH>();
            var eventName = GetEventKey<T>();
            DoRemoveHandler(eventName, handlerToRemove);
        }

        public IEnumerable<SubscriptionInfo> GetHandlersForEvent<T>()
            where T : IntegrationEvent
        {
            var key = GetEventKey<T>();

            return GetHandlersForEvent(key);
        }

        public IReadOnlyCollection<SubscriptionInfo> GetHandlersForEvent(string eventName)
            => _handlers[eventName].AsReadOnly();

        public bool HasSubscriptionsForEvent<T>()
            where T : IntegrationEvent
        {
            var key = GetEventKey<T>();

            return HasSubscriptionsForEvent(key);
        }

        public bool HasSubscriptionsForEvent(string eventName)
        {
            return _handlers.ContainsKey(eventName);
        }

        public Type GetEventTypeByName(string eventName)
        {
            return _eventTypes[eventName];
        }

        public string GetEventKey<T>()
        {
            var type = typeof(T);
            return type.GetCustomAttribute<KafkaTopicAttribute>()?.Name;
        }

        private void DoAddSubscription(Type handlerType, string eventName, bool isDynamic)
        {
            if (!HasSubscriptionsForEvent(eventName))
            {
                _handlers.Add(eventName, new List<SubscriptionInfo>());
            }

            if (_handlers[eventName].Any(s => s.HandlerType == handlerType))
            {
                throw new ArgumentException(
                    $"Handler Type {handlerType.Name} already registered for '{eventName}'", nameof(handlerType));
            }

            _handlers[eventName].Add(SubscriptionInfo.Typed(handlerType));
        }

        private void DoRemoveHandler(string eventName, SubscriptionInfo subsToRemove)
        {
            if (subsToRemove != null)
            {
                _handlers[eventName].Remove(subsToRemove);

                if (_handlers[eventName].Count == 0)
                {
                    _handlers.Remove(eventName);

                    if (_eventTypes.ContainsKey(eventName))
                    {
                        _eventTypes.Remove(eventName);
                    }

                    RaiseOnEventRemoved(eventName);
                }
            }
        }

        private void RaiseOnEventRemoved(string eventName)
        {
            var handler = OnEventRemoved;

            if (handler != null)
            {
                OnEventRemoved(this, eventName);
            }
        }

        private SubscriptionInfo FindSubscriptionToRemove<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = GetEventKey<T>();

            return DoFindSubscriptionToRemove(eventName, typeof(TH));
        }

        private SubscriptionInfo DoFindSubscriptionToRemove(string eventName, Type handlerType)
        {
            if (!HasSubscriptionsForEvent(eventName))
            {
                return null;
            }

            return _handlers[eventName].SingleOrDefault(s => s.HandlerType == handlerType);
        }
    }
}

