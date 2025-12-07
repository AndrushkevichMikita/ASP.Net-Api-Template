# Kafka Retry Pipeline Architecture

This document describes the robust Kafka retry pipeline implementation for handling message processing failures with exponential backoff and Dead Letter Queue (DLQ) support.

## 📋 Table of Contents

- [Overview](#overview)
- [Architecture Diagram](#architecture-diagram)
- [Flow Diagram](#flow-diagram)
- [Components](#components)
- [Configuration](#configuration)
- [Usage](#usage)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

## 🎯 Overview

The retry pipeline provides a robust mechanism for handling message processing failures in Kafka consumers. It implements a multi-level retry strategy with configurable delays and automatic DLQ routing for messages that exceed maximum retry attempts.

### Key Features

- **Multi-level retry**: 3 retry levels with configurable delays (10s, 1m, 5m)
- **Dead Letter Queue**: Automatic routing to DLQ after max retries
- **Idempotent processing**: Handlers must be idempotent to handle retries safely
- **Offset commit safety**: Offsets committed only after successful handoff to retry/DLQ
- **Comprehensive logging**: Full visibility into retry attempts and failures
- **Configurable delays**: Easy to adjust retry delays per environment

## 🏗️ Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         KAFKA RETRY PIPELINE                              │
└─────────────────────────────────────────────────────────────────────────┘

┌──────────────┐
│ Main Topic   │  account_created
│ (Consumer)   │  Consumer Group: my-microservice
└──────┬───────┘
       │
       │ Message consumed
       ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                    Main Consumer (KafkaClient)                            │
│  • Consumes from main topic                                              │
│  • Processes message with handler                                         │
│  • On failure: Publishes to retry topic                                   │
│  • Commits offset AFTER successful handoff to retry/DLQ                 │
└──────────────────────────────────────────────────────────────────────────┘
       │
       │ Processing fails
       ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                    Retry Producer (IRetryProducer)                        │
│  • Determines retry level based on retry count                           │
│  • Creates RetryMessageEnvelope with metadata                             │
│  • Publishes to appropriate retry topic                                   │
└──────────────────────────────────────────────────────────────────────────┘
       │
       ├──────────────────┬──────────────────┬──────────────────┐
       │                  │                  │                  │
       ▼                  ▼                  ▼                  ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ Retry Topic   │  │ Retry Topic   │  │ Retry Topic   │  │ DLQ Topic    │
│ .retry.10s    │  │ .retry.1m     │  │ .retry.5m     │  │ .dlq         │
│ Delay: 10s    │  │ Delay: 1m     │  │ Delay: 5m     │  │ Final        │
└──────┬───────┘  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘
       │                  │                  │                  │
       │                  │                  │                  │
       ▼                  ▼                  ▼                  ▼
┌──────────────────────────────────────────────────────────────────────────┐
│              Retry Consumers (IRetryConsumer)                            │
│  • Consume from retry topics                                             │
│  • Wait for RetryAt timestamp                                            │
│  • Republish original payload to main topic                               │
│  • Commit offset AFTER successful republish                              │
└──────────────────────────────────────────────────────────────────────────┘
       │                  │                  │
       │                  │                  │
       └──────────────────┴──────────────────┘
                          │
                          │ Republished to main topic
                          ▼
                  ┌──────────────┐
                  │ Main Topic    │  (cycle repeats)
                  └──────────────┘

┌──────────────────────────────────────────────────────────────────────────┐
│                    DLQ Consumer (IDlqConsumer)                            │
│  • Consumes from DLQ topic                                                │
│  • Logs DLQ messages with full context                                    │
│  • Triggers alerts/monitoring                                             │
│  • Commits offset after logging                                           │
└──────────────────────────────────────────────────────────────────────────┘
```

## 🔄 Flow Diagram

```
Message Processing Flow:

1. Main Consumer receives message from account_created
   │
   ├─► Handler processes message
   │   │
   │   ├─► SUCCESS
   │   │   └─► Commit offset ✅
   │   │
   │   └─► FAILURE
   │       │
   │       ├─► Retry Count < Max (0, 1, 2)
   │       │   │
   │       │   ├─► Retry Count = 0 → account_created.retry.10s (delay 10s)
   │       │   ├─► Retry Count = 1 → account_created.retry.1m (delay 1m)
   │       │   └─► Retry Count = 2 → account_created.retry.5m (delay 5m)
   │       │       │
   │       │       └─► Publish to retry topic → Commit offset ✅
   │       │
   │       └─► Retry Count >= Max (3)
   │           │
   │           └─► account_created.dlq
   │               │
   │               └─► Publish to DLQ → Commit offset ✅

2. Retry Consumer receives message from retry topic
   │
   ├─► Check RetryAt timestamp
   │   │
   │   ├─► Not ready yet → Skip (don't commit)
   │   │
   │   └─► Ready to retry
   │       │
   │       └─► Republish original payload to main topic
   │           │
   │           └─► Commit offset ✅

3. DLQ Consumer receives message from DLQ
   │
   └─► Log with full context → Trigger alerts → Commit offset ✅
```

## 🧩 Components

### Core Components

#### 1. **RetryMessageEnvelope**
Wraps the original message with retry metadata:
- `RetryCount`: Number of retry attempts
- `RetryAt`: Timestamp when message should be retried
- `OriginalTopic`: Original topic name
- `OriginalPayload`: Original message payload (JSON)
- `OriginalKey`: Original message key
- `ErrorReason`: Error reason for logging
- `FirstReceivedAt`: When message was first received
- `LastRetryAt`: When last retry was attempted

#### 2. **RetryConfiguration**
Configuration for retry pipeline:
- `IsRetryEnabled`: Enable/disable retry functionality
- `MaxRetryAttempts`: Maximum retry attempts (default: 3)
- `RetryDelay10Seconds`: Delay for first retry (default: 10s)
- `RetryDelay1Minute`: Delay for second retry (default: 1m)
- `RetryDelay5Minutes`: Delay for third retry (default: 5m)
- `RetryConsumerGroupId`: Consumer group for retry consumers

#### 3. **IRetryProducer**
Publishes messages to retry topics and DLQ:
- `PublishToRetryTopicAsync()`: Publishes to appropriate retry topic
- `PublishToDlqAsync()`: Publishes directly to DLQ

#### 4. **IRetryConsumer**
Consumes from retry topics and republishes to main topic:
- `StartAsync()`: Starts consuming from retry topic
- Waits for `RetryAt` timestamp before republishing
- Commits offset only after successful republish

#### 5. **IDlqConsumer**
Consumes from DLQ for monitoring and alerting:
- `StartAsync()`: Starts consuming from DLQ
- Logs DLQ messages with full context
- Can trigger alerts/monitoring integrations

#### 6. **IRetryOrchestrator**
Orchestrates retry consumers and DLQ consumer:
- `StartRetryConsumersAsync()`: Starts all retry consumers for a topic

### Integration Points

#### **SubscriptionsProcessor**
Modified to integrate retry logic:
- On handler failure, calls `HandleProcessingFailureAsync()`
- Determines retry level based on retry count
- Publishes to retry topic or DLQ
- Commits offset only after successful publish

## ⚙️ Configuration

### appsettings.json

```json
{
  "Kafka": {
    "GroupId": "my-microservice",
    "EnableAutoOffsetStore": false,
    "Retry": {
      "IsRetryEnabled": true,
      "MaxRetryAttempts": 3,
      "RetryDelay10Seconds": "00:00:10",
      "RetryDelay1Minute": "00:01:00",
      "RetryDelay5Minutes": "00:05:00",
      "RetryConsumerGroupId": "my-microservice-retry"
    }
  }
}
```

### Topic Naming Convention

For a main topic `account_created`, the retry pipeline creates:
- `account_created.retry.10s` - First retry (10 seconds delay)
- `account_created.retry.1m` - Second retry (1 minute delay)
- `account_created.retry.5m` - Third retry (5 minutes delay)
- `account_created.dlq` - Dead Letter Queue

## 🚀 Usage

### 1. Enable Retry Pipeline

The retry pipeline is automatically enabled when:
- `Kafka:Retry:IsRetryEnabled` is `true`
- Retry services are registered in DI (done automatically)

### 2. Start Retry Consumers

When subscribing to a topic, start retry consumers:

```csharp
// In Program.cs or startup
var retryOrchestrator = serviceProvider.GetRequiredService<IRetryOrchestrator>();
await retryOrchestrator.StartRetryConsumersAsync("account_created", cancellationToken);
```

Or use `IRetryPipelineService`:

```csharp
var retryPipelineService = serviceProvider.GetRequiredService<IRetryPipelineService>();
await retryPipelineService.StartRetryConsumersAsync("account_created", cancellationToken);
await retryPipelineService.StartDlqConsumerAsync("account_created", cancellationToken);
```

### 3. Implement Idempotent Handlers

Handlers must be idempotent to handle retries safely:

```csharp
public class AccountCreatedEventHandler : IIntegrationEventHandler<AccountCreatedEvent>
{
    public async Task<IntegrationEventResult> HandleAsync(
        AccountCreatedEvent integrationEvent,
        Action onComplete,
        CancellationToken cancellationToken)
    {
        // IDEMPOTENCY CHECK: Check if already processed
        var eventId = integrationEvent.Id;
        if (await _eventRepository.IsProcessedAsync(eventId))
        {
            return IntegrationEventResult.CreateSuccessfulResult("Already processed");
        }

        // BUSINESS LOGIC: Perform idempotent operation
        await _accountRepository.UpsertAsync(new Account { ... });

        // Mark as processed
        await _eventRepository.MarkAsProcessedAsync(eventId);

        return IntegrationEventResult.CreateSuccessfulResult();
    }
}
```

See `IdempotentAccountCreatedEventHandler.cs` for a complete example.

## ✅ Best Practices

### 1. **Idempotent Processing**
- Always check if work has already been done
- Use idempotency keys (e.g., event ID)
- Use database constraints to prevent duplicates
- Use UPSERT operations where possible

### 2. **Offset Commit Strategy**
- ✅ **DO**: Commit offset after successful handoff to retry/DLQ
- ✅ **DO**: Commit offset after successful business processing
- ❌ **DON'T**: Commit offset before handoff to retry/DLQ
- ❌ **DON'T**: Commit offset if publish to retry/DLQ fails

### 3. **Error Handling**
- Return `IntegrationEventResult.CreateFailureResult()` on business logic failures
- Let the framework handle retry logic
- Log errors with full context for debugging

### 4. **Monitoring**
- Monitor DLQ message count
- Alert on high DLQ message rates
- Track retry attempt metrics
- Monitor retry topic lag

### 5. **Configuration**
- Adjust retry delays based on business requirements
- Set appropriate `MaxRetryAttempts` (default: 3)
- Use different delays for different environments
- Disable retry in development if needed

## 🔍 Troubleshooting

### Messages Stuck in Retry Topics

**Symptoms**: Messages not being republished from retry topics

**Causes**:
- Retry consumers not started
- `RetryAt` timestamp not reached
- Retry consumer errors

**Solutions**:
1. Verify retry consumers are started: Check logs for "Started retry consumer"
2. Check `RetryAt` timestamp: Messages wait until this time
3. Check retry consumer logs for errors
4. Verify retry topics exist in Kafka

### Messages Going to DLQ Immediately

**Symptoms**: Messages sent to DLQ without retries

**Causes**:
- `MaxRetryAttempts` set too low
- Retry count incorrectly calculated
- Retry disabled

**Solutions**:
1. Check `MaxRetryAttempts` configuration
2. Verify retry count logic in `HandleProcessingFailureAsync`
3. Ensure `IsRetryEnabled` is `true`

### Offset Not Committing

**Symptoms**: Messages reprocessed repeatedly

**Causes**:
- Publish to retry/DLQ failing
- Offset commit logic not executing
- Consumer configuration issues

**Solutions**:
1. Check retry producer logs for publish failures
2. Verify offset commit happens after successful publish
3. Check consumer configuration (`EnableAutoOffsetStore: false`)

### High DLQ Message Rate

**Symptoms**: Many messages in DLQ

**Causes**:
- Persistent business logic failures
- Invalid message format
- External service unavailable

**Solutions**:
1. Investigate DLQ messages for error patterns
2. Fix underlying business logic issues
3. Add validation to prevent invalid messages
4. Check external service health

## 📊 Monitoring Metrics

Recommended metrics to monitor:

- **Retry Attempts**: Count of messages sent to retry topics
- **DLQ Messages**: Count of messages sent to DLQ
- **Retry Topic Lag**: Consumer lag on retry topics
- **Processing Time**: Time to process messages (including retries)
- **Error Rate**: Rate of processing failures

## 🔐 Security Considerations

- Retry topics should have same security as main topics
- DLQ topics may contain sensitive data - restrict access
- Monitor DLQ for security-related failures
- Rotate credentials used by retry consumers

## 📚 Additional Resources

- [Idempotent Handler Example](../Application/EventHandlers/IdempotentAccountCreatedEventHandler.cs)
- [Retry Configuration](../Retry/RetryConfiguration.cs)
- [Retry Message Envelope](../Retry/RetryMessageEnvelope.cs)
