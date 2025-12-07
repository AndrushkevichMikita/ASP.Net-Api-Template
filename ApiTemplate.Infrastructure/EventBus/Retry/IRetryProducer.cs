namespace ApiTemplate.Infrastructure.EventBus.Retry
{
    /// <summary>
    /// Service for publishing messages to retry topics and DLQ.
    /// </summary>
    public interface IRetryProducer
    {
        /// <summary>
        /// Publishes a message to the appropriate retry topic based on retry count.
        /// </summary>
        /// <param name="originalTopic">Original topic name.</param>
        /// <param name="originalPayload">Original message payload.</param>
        /// <param name="originalKey">Original message key.</param>
        /// <param name="retryCount">Current retry count.</param>
        /// <param name="errorReason">Error reason for logging.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if published successfully, false otherwise.</returns>
        Task<bool> PublishToRetryTopicAsync(
            string originalTopic,
            string originalPayload,
            string originalKey,
            int retryCount,
            string errorReason,
            CancellationToken cancellationToken);

        /// <summary>
        /// Publishes a retry envelope to the appropriate retry topic.
        /// </summary>
        /// <param name="envelope">Retry message envelope.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if published successfully, false otherwise.</returns>
        Task<bool> PublishToRetryTopicAsync(
            RetryMessageEnvelope envelope,
            CancellationToken cancellationToken);

        /// <summary>
        /// Publishes a message directly to DLQ.
        /// </summary>
        /// <param name="originalTopic">Original topic name.</param>
        /// <param name="originalPayload">Original message payload.</param>
        /// <param name="originalKey">Original message key.</param>
        /// <param name="retryCount">Final retry count.</param>
        /// <param name="errorReason">Error reason for logging.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if published successfully, false otherwise.</returns>
        Task<bool> PublishToDlqAsync(
            string originalTopic,
            string originalPayload,
            string originalKey,
            int retryCount,
            string errorReason,
            CancellationToken cancellationToken);

        /// <summary>
        /// Publishes a retry envelope directly to DLQ.
        /// </summary>
        /// <param name="envelope">Retry message envelope.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if published successfully, false otherwise.</returns>
        Task<bool> PublishToDlqAsync(
            RetryMessageEnvelope envelope,
            CancellationToken cancellationToken);
    }
}
