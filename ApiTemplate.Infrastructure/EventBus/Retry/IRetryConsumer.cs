namespace ApiTemplate.Infrastructure.EventBus.Retry
{
    /// <summary>
    /// Service for consuming messages from retry topics and republishing to the main topic.
    /// </summary>
    public interface IRetryConsumer : IDisposable
    {
        /// <summary>
        /// Starts consuming from the specified retry topic.
        /// </summary>
        /// <param name="retryTopic">Retry topic name.</param>
        /// <param name="originalTopic">Original topic name to republish to.</param>
        /// <param name="retryLevel">Retry level for this consumer.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task StartAsync(string retryTopic, string originalTopic, RetryLevel retryLevel, CancellationToken cancellationToken);
    }
}
