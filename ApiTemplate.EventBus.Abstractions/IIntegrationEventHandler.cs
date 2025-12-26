using ApiTemplate.EventBus.Domain.Core.Events;

namespace ApiTemplate.EventBus.Abstractions
{
    public interface IIntegrationEventHandler<in TIntegrationEvent>
        where TIntegrationEvent : IntegrationEvent
    {
        Task<IntegrationEventResult> HandleAsync(
            TIntegrationEvent integrationEvent,
            Action onComplete,
            CancellationToken cancellationToken);
    }
}