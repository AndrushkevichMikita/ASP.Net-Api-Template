using Confluent.Kafka;

namespace ApiTemplate.EventBus.Kafka.Retry
{
    /// <summary>
    /// Factory for creating retry consumer instances.
    /// </summary>
    public interface IRetryConsumerFactory
    {
        /// <summary>
        /// Creates a new retry consumer instance.
        /// </summary>
        IRetryConsumer Create();

        /// <summary>
        /// Creates a Kafka consumer for a specific retry topic with custom group ID.
        /// </summary>
        IConsumer<string, string> CreateConsumer(string retryTopic);
    }
}

