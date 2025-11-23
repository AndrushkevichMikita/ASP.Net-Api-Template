using ApiTemplate.Domain.Events;

namespace ApiTemplate.Infrastructure.EventBus
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

