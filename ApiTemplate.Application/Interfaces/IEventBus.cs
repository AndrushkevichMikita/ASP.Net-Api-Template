using ApiTemplate.Domain.Events;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ApiTemplate.Application.Interfaces
{
    public interface IEventBus : IHealthCheck
    {
        Task<bool> PublishAsync(
            IntegrationEvent integrationEvent,
            CancellationToken cancellationToken);

        Task ProcessEventAsync(
            string topic,
            string message,
            string messageId,
            Action commitAction,
            CancellationToken cancellationToken);

        void Subscribe<T, TH>(int numberOfConsumers)
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>;

        void Unsubscribe<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>;
    }
}

