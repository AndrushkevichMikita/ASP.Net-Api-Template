namespace ApiTemplate.Infrastructure.EventBus.Retry
{
    /// <summary>
    /// Service for consuming messages from DLQ for monitoring and alerting.
    /// </summary>
    public interface IDlqConsumer : IDisposable
    {
        /// <summary>
        /// Starts consuming from the DLQ topic.
        /// </summary>
        /// <param name="dlqTopic">DLQ topic name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task StartAsync(string dlqTopic, CancellationToken cancellationToken);
    }
}
