using ApiTemplate.Domain.Events;

namespace ApiTemplate.Infrastructure.EventBus
{
    public interface IEventPublisher
    {
        Task<bool> PublishAsync(
            IntegrationEvent @event,
            CancellationToken cancellationToken);
    }
}

