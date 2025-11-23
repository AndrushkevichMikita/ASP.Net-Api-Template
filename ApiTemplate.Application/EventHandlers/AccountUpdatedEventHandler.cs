using ApiTemplate.Domain.Events;

namespace ApiTemplate.Application.EventHandlers
{
    public class AccountUpdatedEventHandler : IIntegrationEventHandler<AccountUpdatedEvent>
    {
        public Task<IntegrationEventResult> HandleAsync(
            AccountUpdatedEvent integrationEvent,
            Action onComplete,
            CancellationToken cancellationToken)
        {
            // Example handler implementation
            // In a real scenario, you would perform business logic here
            // For example: update cache, notify other services, etc.

            return Task.FromResult(IntegrationEventResult.CreateSuccessfulResult());
        }
    }
}

