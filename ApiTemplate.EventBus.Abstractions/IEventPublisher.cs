using ApiTemplate.EventBus.Domain.Core.Events;

namespace ApiTemplate.EventBus.Abstractions
{
    public interface IEventPublisher
    {
        Task<bool> PublishAsync(
            IntegrationEvent @event,
            CancellationToken cancellationToken);
    }
}

