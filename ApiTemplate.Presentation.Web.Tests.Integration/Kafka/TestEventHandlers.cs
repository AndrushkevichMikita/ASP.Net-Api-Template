using ApiTemplate.Domain.Events;
using ApiTemplate.EventBus.Abstractions;
using ApiTemplate.EventBus.Domain.Core.Events;
using Serilog;

namespace ApiTemplate.Presentation.Web.Tests.Integration.Kafka
{
    /// <summary>
    /// Test-specific event handlers for Kafka integration tests.
    /// These handlers are only used in tests and are isolated from production code.
    /// </summary>

    /// <summary>
    /// Handler that always fails - used to test retry pipeline.
    /// </summary>
    public class KafkaTestEventFailureHandler : IIntegrationEventHandler<KafkaTestEvent>
    {
        private readonly ILogger _logger;

        public KafkaTestEventFailureHandler(ILogger logger)
        {
            _logger = logger.ForContext<KafkaTestEventFailureHandler>();
        }

        public Task<IntegrationEventResult> HandleAsync(
            KafkaTestEvent integrationEvent,
            Action onComplete,
            CancellationToken cancellationToken)
        {
            _logger.Information(
                "KafkaTestEvent received (will fail) - TestId: {TestId}, TestData: {TestData}, EventId: {EventId}",
                integrationEvent.TestId,
                integrationEvent.TestData,
                integrationEvent.Id);

            // Always fail to test retry pipeline
            return Task.FromResult(IntegrationEventResult.CreateFailureResult(
                new Exception($"Test failure for TestId: {integrationEvent.TestId}")));
        }
    }

    /// <summary>
    /// Handler that always fails - used to test retry pipeline with retry test events.
    /// </summary>
    public class KafkaRetryTestEventFailureHandler : IIntegrationEventHandler<KafkaRetryTestEvent>
    {
        private readonly ILogger _logger;

        public KafkaRetryTestEventFailureHandler(ILogger logger)
        {
            _logger = logger.ForContext<KafkaRetryTestEventFailureHandler>();
        }

        public Task<IntegrationEventResult> HandleAsync(
            KafkaRetryTestEvent integrationEvent,
            Action onComplete,
            CancellationToken cancellationToken)
        {
            _logger.Information(
                "KafkaRetryTestEvent received (will fail) - TestId: {TestId}, Email: {Email}, FirstName: {FirstName}, LastName: {LastName}, EventId: {EventId}, RetryAttempts: {RetryAttempts}",
                integrationEvent.TestId,
                integrationEvent.Email,
                integrationEvent.FirstName,
                integrationEvent.LastName,
                integrationEvent.Id,
                integrationEvent.RetryAttempts);

            // Always fail to test retry pipeline
            return Task.FromResult(IntegrationEventResult.CreateFailureResult(
                new Exception($"Test failure for TestId: {integrationEvent.TestId}")));
        }
    }
}

