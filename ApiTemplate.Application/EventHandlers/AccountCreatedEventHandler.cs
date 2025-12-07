using ApiTemplate.Domain.Events;
using Serilog;

namespace ApiTemplate.Application.EventHandlers
{
    public class AccountCreatedEventHandler : IIntegrationEventHandler<AccountCreatedEvent>
    {
        private readonly ILogger _logger;

        public AccountCreatedEventHandler(ILogger logger)
        {
            _logger = logger.ForContext<AccountCreatedEventHandler>();
        }

        public Task<IntegrationEventResult> HandleAsync(
            AccountCreatedEvent integrationEvent,
            Action onComplete,
            CancellationToken cancellationToken)
        {
            _logger.Information(
                "AccountCreatedEvent received - AccountId: {AccountId}, Email: {Email}, FirstName: {FirstName}, LastName: {LastName}, EventId: {EventId}, RetryAttempts: {RetryAttempts}",
                integrationEvent.AccountId,
                integrationEvent.Email,
                integrationEvent.FirstName,
                integrationEvent.LastName,
                integrationEvent.Id,
                integrationEvent.RetryAttempts);

            // Example handler implementation
            // In a real scenario, you would perform business logic here
            // For example: send welcome email, create audit log, update cache, etc.

            return Task.FromResult(IntegrationEventResult.CreateFailureResult(new Exception()));
        }
    }
}