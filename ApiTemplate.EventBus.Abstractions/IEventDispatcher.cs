using ApiTemplate.EventBus.Domain.Core.Events;

namespace ApiTemplate.EventBus.Abstractions
{
    public interface IEventDispatcher
    {
        Task<IntegrationEventResult> DispatchAsync(
            IntegrationEvent @event,
            Type eventType,
            Type handlerType,
            Action commitAction,
            CancellationToken cancellationToken);
    }
}

