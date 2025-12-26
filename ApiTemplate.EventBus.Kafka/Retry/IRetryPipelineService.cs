namespace ApiTemplate.EventBus.Kafka.Retry
{
    /// <summary>
    /// Service that manages the retry pipeline: retry consumers and DLQ consumer.
    /// </summary>
    public interface IRetryPipelineService : IDisposable
    {
        /// <summary>
        /// Starts retry consumers for a main topic.
        /// </summary>
        /// <param name="mainTopic">The main topic name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task StartRetryConsumersAsync(string mainTopic, CancellationToken cancellationToken = default);

        /// <summary>
        /// Starts DLQ consumer for a main topic.
        /// </summary>
        /// <param name="mainTopic">The main topic name.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task StartDlqConsumerAsync(string mainTopic, CancellationToken cancellationToken = default);
    }
}

