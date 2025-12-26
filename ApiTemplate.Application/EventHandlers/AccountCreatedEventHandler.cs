using ApiTemplate.Domain.Events;
using ApiTemplate.EventBus.Abstractions;
using ApiTemplate.EventBus.Domain.Core.Events;

namespace ApiTemplate.Application.EventHandlers
{
    public class AccountCreatedEventHandler : IIntegrationEventHandler<AccountCreatedEvent>
    {
        public Task<IntegrationEventResult> HandleAsync(
            AccountCreatedEvent integrationEvent,
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