using ApiTemplate.Domain.Events;
using Serilog;

namespace ApiTemplate.Application.EventHandlers
{
    /// <summary>
    /// Example of an idempotent event handler.
    /// 
    /// IDEMPOTENCY PRINCIPLE:
    /// Processing the same message multiple times should have no side effects.
    /// This is critical for retry scenarios where the same message may be processed
    /// multiple times due to retries or network issues.
    /// 
    /// IDEMPOTENCY STRATEGIES:
    /// 1. Check if work has already been done (e.g., record exists in database)
    /// 2. Use idempotency keys/tokens stored in database
    /// 3. Use database constraints to prevent duplicates
    /// 4. Use conditional updates (e.g., "UPDATE WHERE version = X")
    /// 5. Use idempotent operations (e.g., SET operations, UPSERT)
    /// </summary>
    public class IdempotentAccountCreatedEventHandler : IIntegrationEventHandler<AccountCreatedEvent>
    {
        private readonly ILogger _logger;
        // In a real scenario, you would inject your repository/service here
        // private readonly IAccountRepository _accountRepository;

        public IdempotentAccountCreatedEventHandler(ILogger logger)
        {
            _logger = logger.ForContext<IdempotentAccountCreatedEventHandler>();
        }

        public async Task<IntegrationEventResult> HandleAsync(
            AccountCreatedEvent integrationEvent,
            Action onComplete,
            CancellationToken cancellationToken)
        {
            try
            {
                // IDEMPOTENCY CHECK #1: Check if this event has already been processed
                // Use the event ID as an idempotency key
                var eventId = integrationEvent.Id;
                
                _logger.Information(
                    "Processing AccountCreatedEvent - EventId: {EventId}, AccountId: {AccountId}, Email: {Email}",
                    eventId,
                    integrationEvent.AccountId,
                    integrationEvent.Email);

                // Example: Check if event has already been processed
                // var existingProcessing = await _eventProcessingRepository.GetByEventIdAsync(eventId);
                // if (existingProcessing != null && existingProcessing.Status == ProcessingStatus.Completed)
                // {
                //     _logger.Information(
                //         "Event {EventId} has already been processed. Skipping (idempotent behavior).",
                //         eventId);
                //     return IntegrationEventResult.CreateSuccessfulResult("Event already processed");
                // }

                // IDEMPOTENCY CHECK #2: Check if the business entity already exists
                // In this case, check if account already exists
                // var existingAccount = await _accountRepository.GetByIdAsync(integrationEvent.AccountId);
                // if (existingAccount != null)
                // {
                //     _logger.Information(
                //         "Account {AccountId} already exists. Skipping creation (idempotent behavior).",
                //         integrationEvent.AccountId);
                //     
                //     // Mark event as processed
                //     // await _eventProcessingRepository.MarkAsProcessedAsync(eventId);
                //     
                //     return IntegrationEventResult.CreateSuccessfulResult("Account already exists");
                // }

                // BUSINESS LOGIC: Perform the actual work
                // This should be idempotent - if run multiple times, it should have no side effects
                
                // Example idempotent operations:
                // 1. UPSERT operation (insert or update if exists)
                // await _accountRepository.UpsertAsync(new Account
                // {
                //     Id = integrationEvent.AccountId,
                //     Email = integrationEvent.Email,
                //     FirstName = integrationEvent.FirstName,
                //     LastName = integrationEvent.LastName
                // });

                // 2. Conditional insert with unique constraint
                // try
                // {
                //     await _accountRepository.CreateAsync(new Account { ... });
                // }
                // catch (UniqueConstraintViolationException)
                // {
                //     // Already exists, this is OK (idempotent)
                //     _logger.Information("Account already exists, skipping creation");
                // }

                // 3. Use database transactions with idempotency checks
                // using var transaction = await _dbContext.BeginTransactionAsync();
                // try
                // {
                //     var exists = await _dbContext.Accounts.AnyAsync(a => a.Id == integrationEvent.AccountId);
                //     if (!exists)
                //     {
                //         await _dbContext.Accounts.AddAsync(new Account { ... });
                //         await _dbContext.SaveChangesAsync();
                //     }
                //     await transaction.CommitAsync();
                // }
                // catch
                // {
                //     await transaction.RollbackAsync();
                //     throw;
                // }

                // Simulate business logic
                _logger.Information(
                    "Processing account creation for AccountId: {AccountId}, Email: {Email}",
                    integrationEvent.AccountId,
                    integrationEvent.Email);

                // Simulate potential failure for retry testing
                // Uncomment to test retry logic:
                // if (integrationEvent.AccountId % 2 == 0)
                // {
                //     throw new Exception("Simulated processing failure for retry testing");
                // }

                // Mark event as processed (idempotency record)
                // await _eventProcessingRepository.MarkAsProcessedAsync(eventId, ProcessingStatus.Completed);

                _logger.Information(
                    "Successfully processed AccountCreatedEvent - EventId: {EventId}, AccountId: {AccountId}",
                    eventId,
                    integrationEvent.AccountId);

                // Commit offset after successful processing
                // Note: The framework handles offset commit, but you can also handle it manually
                // if needed by returning IntegrationEventResult.CreateSuccessfulResultWithCommitHandled()
                return IntegrationEventResult.CreateSuccessfulResult(
                    $"Account {integrationEvent.AccountId} processed successfully",
                    saveEventToLog: true);
            }
            catch (Exception ex)
            {
                _logger.Error(
                    ex,
                    "Failed to process AccountCreatedEvent - EventId: {EventId}, AccountId: {AccountId}",
                    integrationEvent.Id,
                    integrationEvent.AccountId);

                // Return failure result - this will trigger retry logic
                return IntegrationEventResult.CreateFailureResult(
                    ex,
                    saveEventToLog: true,
                    message: $"Failed to process account {integrationEvent.AccountId}: {ex.Message}");
            }
        }
    }
}
