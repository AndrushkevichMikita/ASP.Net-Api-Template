using ApiTemplate.Infrastructure.EventBus.Internal;
using Newtonsoft.Json;

namespace ApiTemplate.Infrastructure.EventBus.Retry
{
    /// <summary>
    /// Envelope that wraps the original message with retry metadata.
    /// This envelope is used when publishing messages to retry topics and DLQ.
    /// </summary>
    public class RetryMessageEnvelope
    {
        /// <summary>
        /// Number of retry attempts made so far.
        /// </summary>
        public int RetryCount { get; set; }

        /// <summary>
        /// Timestamp when this message should be retried (UTC).
        /// Used by retry consumers to delay processing until the specified time.
        /// </summary>
        public DateTimeOffset RetryAt { get; set; }

        /// <summary>
        /// Original topic name where the message was first consumed.
        /// </summary>
        public string OriginalTopic { get; set; }

        /// <summary>
        /// Original message payload (JSON string).
        /// </summary>
        public string OriginalPayload { get; set; }

        /// <summary>
        /// Original message key.
        /// </summary>
        public string OriginalKey { get; set; }

        /// <summary>
        /// Optional error reason for logging and debugging.
        /// </summary>
        public string ErrorReason { get; set; }

        /// <summary>
        /// Timestamp when the message was first received.
        /// </summary>
        public DateTimeOffset FirstReceivedAt { get; set; }

        /// <summary>
        /// Timestamp when the last retry attempt was made.
        /// </summary>
        public DateTimeOffset LastRetryAt { get; set; }

        /// <summary>
        /// Serializes the envelope to JSON.
        /// </summary>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, KafkaSerialization.Settings);
        }

        /// <summary>
        /// Deserializes the envelope from JSON.
        /// </summary>
        public static RetryMessageEnvelope FromJson(string json)
        {
            return JsonConvert.DeserializeObject<RetryMessageEnvelope>(json, KafkaSerialization.Settings);
        }

        /// <summary>
        /// Creates a new envelope from an original message.
        /// </summary>
        public static RetryMessageEnvelope Create(
            string originalTopic,
            string originalPayload,
            string originalKey,
            int retryCount = 0,
            string errorReason = null)
        {
            var now = DateTimeOffset.UtcNow;
            return new RetryMessageEnvelope
            {
                RetryCount = retryCount,
                OriginalTopic = originalTopic,
                OriginalPayload = originalPayload,
                OriginalKey = originalKey,
                ErrorReason = errorReason,
                FirstReceivedAt = retryCount == 0 ? now : now, // Will be set properly by retry logic
                LastRetryAt = now,
                RetryAt = now // Will be set by retry producer based on retry level
            };
        }

        /// <summary>
        /// Creates a new envelope with retry delay and optional event ID.
        /// </summary>
        public static RetryMessageEnvelope Create(
            string originalTopic,
            string originalPayload,
            string originalKey,
            int retryCount,
            TimeSpan retryDelay,
            string errorReason = null,
            Guid? originalEventId = null)
        {
            var now = DateTimeOffset.UtcNow;
            return new RetryMessageEnvelope
            {
                RetryCount = retryCount,
                OriginalTopic = originalTopic,
                OriginalPayload = originalPayload,
                OriginalKey = originalKey,
                ErrorReason = errorReason,
                FirstReceivedAt = retryCount == 0 ? now : now,
                LastRetryAt = now,
                RetryAt = now.Add(retryDelay)
            };
        }
    }
}
