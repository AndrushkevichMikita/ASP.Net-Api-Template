using System.ComponentModel.DataAnnotations;

namespace ApiTemplate.Infrastructure.EventBus.Retry
{
    /// <summary>
    /// Configuration for Kafka retry pipeline.
    /// Defines retry topics, delays, and max retry attempts.
    /// </summary>
    public class RetryConfiguration
    {
        /// <summary>
        /// Maximum number of retry attempts before sending to DLQ.
        /// Default: 3 (10s, 1m, 5m)
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Delay for first retry level (10 seconds).
        /// </summary>
        public TimeSpan RetryDelay10Seconds { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Delay for second retry level (1 minute).
        /// </summary>
        public TimeSpan RetryDelay1Minute { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Delay for third retry level (5 minutes).
        /// </summary>
        public TimeSpan RetryDelay5Minutes { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Consumer group ID for retry consumers.
        /// </summary>
        public string RetryConsumerGroupId { get; set; } = "my-microservice-retry";

        /// <summary>
        /// Consumer group ID for DLQ consumer.
        /// </summary>
        public string DlqConsumerGroupId { get; set; } = "my-microservice-dlq";

        /// <summary>
        /// Whether retry functionality is enabled.
        /// </summary>
        public bool IsRetryEnabled { get; set; } = true;

        /// <summary>
        /// Gets the retry topic name for a given base topic and retry level.
        /// </summary>
        public string GetRetryTopicName(string baseTopic, RetryLevel level)
        {
            return level switch
            {
                RetryLevel.Retry10Seconds => $"{baseTopic}.retry.10s",
                RetryLevel.Retry1Minute => $"{baseTopic}.retry.1m",
                RetryLevel.Retry5Minutes => $"{baseTopic}.retry.5m",
                RetryLevel.DLQ => $"{baseTopic}.dlq",
                _ => throw new ArgumentException($"Unknown retry level: {level}", nameof(level))
            };
        }

        /// <summary>
        /// Gets the delay for a given retry level.
        /// </summary>
        public TimeSpan GetRetryDelay(RetryLevel level)
        {
            return level switch
            {
                RetryLevel.Retry10Seconds => RetryDelay10Seconds,
                RetryLevel.Retry1Minute => RetryDelay1Minute,
                RetryLevel.Retry5Minutes => RetryDelay5Minutes,
                RetryLevel.DLQ => TimeSpan.Zero, // DLQ has no delay
                _ => throw new ArgumentException($"Unknown retry level: {level}", nameof(level))
            };
        }

        /// <summary>
        /// Gets the delay for a given retry count (0-based index).
        /// </summary>
        public TimeSpan GetRetryDelay(int retryCount)
        {
            var level = GetNextRetryLevel(retryCount);
            return GetRetryDelay(level);
        }

        /// <summary>
        /// Gets the next retry level based on current retry count.
        /// </summary>
        public RetryLevel GetNextRetryLevel(int currentRetryCount)
        {
            return currentRetryCount switch
            {
                0 => RetryLevel.Retry10Seconds,
                1 => RetryLevel.Retry1Minute,
                2 => RetryLevel.Retry5Minutes,
                _ => RetryLevel.DLQ
            };
        }
    }

    /// <summary>
    /// Enumeration of retry levels in the pipeline.
    /// </summary>
    public enum RetryLevel
    {
        /// <summary>
        /// First retry level: 10 seconds delay.
        /// </summary>
        Retry10Seconds = 0,

        /// <summary>
        /// Second retry level: 1 minute delay.
        /// </summary>
        Retry1Minute = 1,

        /// <summary>
        /// Third retry level: 5 minutes delay.
        /// </summary>
        Retry5Minutes = 2,

        /// <summary>
        /// Dead Letter Queue: final destination for failed messages.
        /// </summary>
        DLQ = 3
    }
}
