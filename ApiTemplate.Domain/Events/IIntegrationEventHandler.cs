namespace ApiTemplate.Domain.Events
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

