# Kafka Retry Pipeline - Usage Example

## Quick Start

### 1. Configure Retry Pipeline

Add retry configuration to `appsettings.json`:

```json
{
  "Kafka": {
    "BootstrapServers": "localhost:9092",
    "GroupId": "my-microservice",
    "EnableAutoOffsetStore": false,
    "Retry": {
      "IsRetryEnabled": true,
      "MaxRetryAttempts": 3,
      "RetryDelay10Seconds": "00:00:10",
      "RetryDelay1Minute": "00:01:00",
      "RetryDelay5Minutes": "00:05:00",
      "RetryConsumerGroupId": "my-microservice.retry",
      "DlqConsumerGroupId": "my-microservice.dlq"
    }
  }
}
```

### 2. Subscribe to Main Topic

In your `Program.cs` or startup code:

```csharp
// Subscribe to main topic
eventBus.Subscribe<AccountCreatedEvent, AccountCreatedEventHandler>(numberOfConsumers: 1);

// Start retry pipeline (optional - can be done automatically)
var retryPipelineService = serviceProvider.GetRequiredService<IRetryPipelineService>();
await retryPipelineService.StartRetryConsumersAsync("account_created", cancellationToken);
await retryPipelineService.StartDlqConsumerAsync("account_created", cancellationToken);
```

### 3. Implement Idempotent Handler

```csharp
public class AccountCreatedEventHandler : IIntegrationEventHandler<AccountCreatedEvent>
{
    private readonly ILogger _logger;
    private readonly IAccountRepository _accountRepository;

    public AccountCreatedEventHandler(ILogger logger, IAccountRepository accountRepository)
    {
        _logger = logger;
        _accountRepository = accountRepository;
    }

    public async Task<IntegrationEventResult> HandleAsync(
        AccountCreatedEvent integrationEvent,
        Action onComplete,
        CancellationToken cancellationToken)
    {
        try
        {
            // IDEMPOTENCY CHECK: Verify if already processed
            if (await _accountRepository.ExistsAsync(integrationEvent.AccountId, cancellationToken))
            {
                _logger.Information(
                    "Account {AccountId} already exists. Skipping to maintain idempotency.",
                    integrationEvent.AccountId);
                return IntegrationEventResult.CreateSuccessfulResult("Account already exists");
            }

            // BUSINESS LOGIC (must be idempotent)
            await _accountRepository.CreateAsync(integrationEvent.AccountId, cancellationToken);
            await SendWelcomeEmailAsync(integrationEvent.Email, cancellationToken);

            _logger.Information("Successfully processed AccountCreatedEvent: {EventId}", integrationEvent.Id);
            return IntegrationEventResult.CreateSuccessfulResult("Account created successfully");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error processing AccountCreatedEvent: {EventId}", integrationEvent.Id);
            
            // Return failure - this will trigger retry logic
            return IntegrationEventResult.CreateFailureResult(
                ex,
                saveEventToLog: true,
                message: $"Failed to process: {ex.Message}");
        }
    }
}
```

## Complete Example: Processing Flow

### Scenario: Account Creation with Retry

1. **Initial Message Arrives**
   ```
   Topic: account_created
   Message: { "AccountId": 123, "Email": "user@example.com", ... }
   ```

2. **Main Consumer Processes**
   - Handler attempts to create account
   - Database connection fails
   - Handler returns `IntegrationEventResult.CreateFailureResult()`

3. **Retry Logic Triggers**
   - `SubscriptionsProcessor` detects failure
   - Creates `RetryMessageEnvelope` with:
     - `RetryCount = 1`
     - `RetryAt = Now + 10 seconds`
     - `OriginalPayload = original message`
   - Publishes to `account_created.retry.10s`
   - Commits offset (message safely in retry topic)

4. **Retry Consumer Level 1**
   - Consumes from `account_created.retry.10s`
   - Waits until `RetryAt` timestamp
   - Republishes original payload to `account_created`
   - Commits offset

5. **Main Consumer Processes Again**
   - Handler attempts to create account again
   - Database still unavailable
   - Handler returns failure

6. **Retry Level 2**
   - Message sent to `account_created.retry.1m`
   - `RetryCount = 2`
   - `RetryAt = Now + 1 minute`
   - Offset committed

7. **Retry Consumer Level 2**
   - Waits 1 minute
   - Republishes to main topic

8. **Main Consumer Processes Again**
   - Database connection restored
   - Account created successfully
   - Returns success
   - Offset committed

## Testing the Retry Pipeline

### Test Idempotency

```csharp
[Fact]
public async Task Handler_Should_Be_Idempotent()
{
    // Arrange
    var event1 = new AccountCreatedEvent(123, "test@example.com", "John", "Doe");
    var event2 = new AccountCreatedEvent(123, "test@example.com", "John", "Doe"); // Same event

    // Act
    var result1 = await handler.HandleAsync(event1, () => { }, CancellationToken.None);
    var result2 = await handler.HandleAsync(event2, () => { }, CancellationToken.None);

    // Assert
    Assert.True(result1.IsSuccessful);
    Assert.True(result2.IsSuccessful); // Should succeed even if already processed
    // Verify no duplicate accounts created
}
```

### Test Retry Flow

```csharp
[Fact]
public async Task Should_Retry_On_Failure()
{
    // Arrange - simulate transient failure
    var handler = new AccountCreatedEventHandler(logger, failingRepository);

    // Act
    var result = await handler.HandleAsync(event, () => { }, CancellationToken.None);

    // Assert
    Assert.False(result.IsSuccessful);
    // Verify message was sent to retry topic
    // Verify offset was committed after retry topic publish
}
```

### Test DLQ

```csharp
[Fact]
public async Task Should_Send_To_DLQ_After_Max_Retries()
{
    // Arrange - simulate persistent failure
    // Process message 3 times (max retries)

    // Assert
    // Verify message was sent to DLQ
    // Verify DLQ consumer logged the message
}
```

## Monitoring

### Key Metrics to Monitor

1. **Retry Topic Message Counts**
   - Monitor message volume in each retry topic
   - Alert if volume exceeds threshold

2. **DLQ Message Count**
   - Alert immediately on DLQ messages
   - Review DLQ messages regularly

3. **Retry Success Rate**
   - Track percentage of messages that succeed after retry
   - Monitor trends over time

4. **Processing Latency**
   - Track time from initial processing to final success
   - Monitor for degradation

5. **Consumer Lag**
   - Monitor lag on main topic
   - Monitor lag on retry topics
   - Alert if lag exceeds threshold

### Logging

The retry pipeline logs extensively:

- **Information**: Successful processing, retry topic publishes, republishes
- **Warning**: DLQ messages, retry delays
- **Error**: Processing failures, publish failures
- **Debug**: Detailed retry flow, offset commits

### Example Logs

```
[Information] Processing AccountCreatedEvent - AccountId: 123, EventId: abc-123
[Error] Failed to process AccountCreatedEvent - AccountId: 123, EventId: abc-123
[Information] Sending failed message to retry topic account_created.retry.10s. RetryCount: 1
[Information] Successfully sent message to retry topic and committed offset
[Information] Retry consumer received message from account_created.retry.10s
[Information] Republishing message to main topic account_created. RetryCount: 1
[Information] Successfully republished to main topic and committed offset
[Information] Processing AccountCreatedEvent - AccountId: 123, EventId: abc-123
[Information] Successfully processed AccountCreatedEvent - AccountId: 123
```

## Troubleshooting

### Messages Not Retrying

1. Check `IsRetryEnabled` in configuration
2. Verify retry topics exist in Kafka
3. Check retry producer logs for errors
4. Verify retry consumers are running

### Messages Going Directly to DLQ

1. Check `MaxRetryAttempts` configuration
2. Verify retry topics are accessible
3. Check retry producer is working

### Duplicate Processing

1. Verify idempotency checks in handlers
2. Check offset commit behavior
3. Review consumer group configuration
4. Check for consumer rebalancing issues

### Offset Not Committing

1. Verify `EnableAutoOffsetStore` is `false`
2. Check if retry/DLQ publish is succeeding
3. Review consumer error logs
4. Check for Kafka connectivity issues

## Best Practices Summary

1. ✅ **Always implement idempotent handlers**
2. ✅ **Return meaningful error messages**
3. ✅ **Log with sufficient context**
4. ✅ **Monitor retry and DLQ metrics**
5. ✅ **Test idempotency thoroughly**
6. ✅ **Use appropriate retry delays**
7. ✅ **Alert on DLQ messages**
8. ✅ **Review DLQ messages regularly**

