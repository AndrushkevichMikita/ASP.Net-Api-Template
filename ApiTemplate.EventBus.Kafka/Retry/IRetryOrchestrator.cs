namespace ApiTemplate.EventBus.Kafka.Retry
{
    /// <summary>
    /// Orchestrates retry consumers and DLQ consumer for a given topic.
    /// This service manages the lifecycle of all retry-related consumers.
    /// </summary>
    public interface IRetryOrchestrator
    {
        /// <summary>
        /// Starts all retry consumers and DLQ consumer for a given base topic.
        /// </summary>
        /// <param name="baseTopic">Base topic name (e.g., "account_created").</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task StartRetryConsumersAsync(string baseTopic, CancellationToken cancellationToken);
    }
}



