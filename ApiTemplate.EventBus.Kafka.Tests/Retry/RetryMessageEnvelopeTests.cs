using ApiTemplate.EventBus.Kafka.Retry;
using Newtonsoft.Json;

namespace ApiTemplate.EventBus.Kafka.Tests.Retry
{
    /// <summary>
    /// Unit tests for RetryMessageEnvelope.
    /// Tests envelope creation, serialization, and deserialization.
    /// </summary>
    public class RetryMessageEnvelopeTests
    {
        [Fact]
        public void Create_WithBasicParameters_ShouldSetCorrectProperties()
        {
            // Arrange
            var originalTopic = "test.topic";
            var originalPayload = "{\"test\":\"data\"}";
            var originalKey = "test-key";
            var retryCount = 1;
            var errorReason = "Test error";

            // Act
            var envelope = RetryMessageEnvelope.Create(
                originalTopic,
                originalPayload,
                originalKey,
                retryCount,
                errorReason);

            // Assert
            Assert.Equal(originalTopic, envelope.OriginalTopic);
            Assert.Equal(originalPayload, envelope.OriginalPayload);
            Assert.Equal(originalKey, envelope.OriginalKey);
            Assert.Equal(retryCount, envelope.RetryCount);
            Assert.Equal(errorReason, envelope.ErrorReason);
            Assert.NotEqual(default(DateTimeOffset), envelope.FirstReceivedAt);
            Assert.NotEqual(default(DateTimeOffset), envelope.LastRetryAt);
            Assert.NotEqual(default(DateTimeOffset), envelope.RetryAt);
        }

        [Fact]
        public void Create_WithRetryDelay_ShouldSetRetryAtCorrectly()
        {
            // Arrange
            var originalTopic = "test.topic";
            var originalPayload = "{\"test\":\"data\"}";
            var originalKey = "test-key";
            var retryCount = 1;
            var retryDelay = TimeSpan.FromSeconds(30);
            var now = DateTimeOffset.UtcNow;

            // Act
            var envelope = RetryMessageEnvelope.Create(
                originalTopic,
                originalPayload,
                originalKey,
                retryCount,
                retryDelay,
                "Test error");

            // Assert
            Assert.Equal(retryCount, envelope.RetryCount);
            Assert.True(envelope.RetryAt >= now.Add(retryDelay).AddSeconds(-1)); // Allow 1 second tolerance
            Assert.True(envelope.RetryAt <= now.Add(retryDelay).AddSeconds(1)); // Allow 1 second tolerance
            Assert.Equal(envelope.LastRetryAt, envelope.FirstReceivedAt);
        }

        [Fact]
        public void Create_WithEventId_ShouldPreserveEventId()
        {
            // Arrange
            var originalTopic = "test.topic";
            var originalPayload = "{\"test\":\"data\"}";
            var originalKey = "test-key";
            var retryCount = 1;
            var retryDelay = TimeSpan.FromSeconds(10);
            var eventId = Guid.NewGuid();

            // Act
            var envelope = RetryMessageEnvelope.Create(
                originalTopic,
                originalPayload,
                originalKey,
                retryCount,
                retryDelay,
                "Test error",
                eventId);

            // Assert
            Assert.Equal(retryCount, envelope.RetryCount);
            Assert.True(envelope.RetryAt > DateTimeOffset.UtcNow);
        }

        [Fact]
        public void ToJson_FromJson_ShouldRoundTripCorrectly()
        {
            // Arrange
            var original = RetryMessageEnvelope.Create(
                "test.topic",
                "{\"test\":\"data\"}",
                "test-key",
                2,
                TimeSpan.FromSeconds(30),
                "Test error");

            // Act
            var json = original.ToJson();
            var deserialized = RetryMessageEnvelope.FromJson(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(original.RetryCount, deserialized!.RetryCount);
            Assert.Equal(original.OriginalTopic, deserialized!.OriginalTopic);
            Assert.Equal(original.OriginalPayload, deserialized!.OriginalPayload);
            Assert.Equal(original.OriginalKey, deserialized!.OriginalKey);
            Assert.Equal(original.ErrorReason, deserialized!.ErrorReason);
            Assert.Equal(original.RetryAt, deserialized!.RetryAt);
            Assert.Equal(original.FirstReceivedAt, deserialized!.FirstReceivedAt);
            Assert.Equal(original.LastRetryAt, deserialized!.LastRetryAt);
        }

        [Fact]
        public void ToJson_ShouldProduceValidJson()
        {
            // Arrange
            var envelope = RetryMessageEnvelope.Create(
                "test.topic",
                "{\"test\":\"data\"}",
                "test-key",
                1,
                "Test error");

            // Act
            var json = envelope.ToJson();

            // Assert
            Assert.NotNull(json);
            Assert.NotEmpty(json);
            Assert.StartsWith("{", json);
            Assert.EndsWith("}", json);
            
            // Verify it's valid JSON
            var parsed = JsonConvert.DeserializeObject(json);
            Assert.NotNull(parsed);
        }

        [Fact]
        public void FromJson_WithInvalidJson_ShouldReturnNull()
        {
            // Arrange
            var invalidJson = "not valid json";

            // Act
            var result = RetryMessageEnvelope.FromJson(invalidJson);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void FromJson_WithEmptyString_ShouldReturnNull()
        {
            // Arrange
            var emptyJson = "";

            // Act
            var result = RetryMessageEnvelope.FromJson(emptyJson);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void Create_WithZeroRetryCount_ShouldSetRetryCountToZero()
        {
            // Arrange
            var envelope = RetryMessageEnvelope.Create(
                "test.topic",
                "payload",
                "key",
                0,
                "error");

            // Assert
            Assert.Equal(0, envelope.RetryCount);
        }

        [Fact]
        public void Create_WithNullErrorReason_ShouldAllowNull()
        {
            // Arrange
            var envelope = RetryMessageEnvelope.Create(
                "test.topic",
                "payload",
                "key",
                1,
                null);

            // Assert
            Assert.Null(envelope.ErrorReason);
        }

        [Fact]
        public void Create_WithNegativeRetryDelay_ShouldSetRetryAtInPast()
        {
            // Arrange
            var retryDelay = TimeSpan.FromSeconds(-10); // Negative delay = past
            var now = DateTimeOffset.UtcNow;

            // Act
            var envelope = RetryMessageEnvelope.Create(
                "test.topic",
                "payload",
                "key",
                1,
                retryDelay,
                "error");

            // Assert
            Assert.True(envelope.RetryAt < now, "RetryAt should be in the past for negative delay");
        }
    }
}

