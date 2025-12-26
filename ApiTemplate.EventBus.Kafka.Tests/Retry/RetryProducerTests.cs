using ApiTemplate.EventBus.Kafka.Internal;
using ApiTemplate.EventBus.Kafka.Retry;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Moq;
using Serilog;

namespace ApiTemplate.EventBus.Kafka.Tests.Retry
{
    /// <summary>
    /// Unit tests for RetryProducer.
    /// Tests retry topic routing, DLQ publishing, and envelope creation with mocks.
    /// </summary>
    public class RetryProducerTests : IDisposable
    {
        private readonly Mock<IProducerFactory> _producerFactoryMock;
        private readonly Mock<IProducer<string, string>> _producerMock;
        private readonly Mock<ILogger> _loggerMock;
        private readonly RetryConfiguration _retryConfig;
        private readonly RetryProducer _retryProducer;

        public RetryProducerTests()
        {
            _producerFactoryMock = new Mock<IProducerFactory>();
            _producerMock = new Mock<IProducer<string, string>>();
            _loggerMock = new Mock<ILogger>();
            _loggerMock.Setup(l => l.ForContext<RetryProducer>()).Returns(_loggerMock.Object);
            _loggerMock.Setup(l => l.ForContext(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>())).Returns(_loggerMock.Object);

            _retryConfig = new RetryConfiguration
            {
                RetryDelay10Seconds = TimeSpan.FromSeconds(10),
                RetryDelay1Minute = TimeSpan.FromMinutes(1),
                RetryDelay5Minutes = TimeSpan.FromMinutes(5),
                MaxRetryAttempts = 3
            };

            _producerFactoryMock.Setup(f => f.Create()).Returns(_producerMock.Object);

            var options = Options.Create(_retryConfig);
            _retryProducer = new RetryProducer(_producerFactoryMock.Object, options, _loggerMock.Object);
        }

        [Fact]
        public async Task PublishToRetryTopicAsync_WithRetryCount0_ShouldRouteTo10sTopic()
        {
            // Arrange
            var originalTopic = "test.topic";
            var originalPayload = "{\"test\":\"data\"}";
            var originalKey = "test-key";
            var retryCount = 0;
            var errorReason = "Test error";
            Message<string, string>? capturedMessage = null;

            var deliveryResult = new DeliveryResult<string, string>
            {
                Status = PersistenceStatus.Persisted,
                Topic = $"{originalTopic}.retry.10s"
            };

            _producerMock
                .Setup(p => p.ProduceAsync(
                    It.Is<string>(t => t == $"{originalTopic}.retry.10s"),
                    It.IsAny<Message<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, Message<string, string>, CancellationToken>((topic, msg, ct) => capturedMessage = msg)
                .ReturnsAsync(deliveryResult);

            // Act
            var result = await _retryProducer.PublishToRetryTopicAsync(
                originalTopic,
                originalPayload,
                originalKey,
                retryCount,
                errorReason,
                CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.NotNull(capturedMessage);
            Assert.Equal(originalKey, capturedMessage!.Key);
            Assert.True(capturedMessage.Headers.Any(h => h.Key == "retry-count"));
            
            var envelope = RetryMessageEnvelope.FromJson(capturedMessage.Value);
            Assert.NotNull(envelope);
            Assert.Equal(originalPayload, envelope!.OriginalPayload);
            
            _producerMock.Verify(
                p => p.ProduceAsync(
                    It.Is<string>(t => t == $"{originalTopic}.retry.10s"),
                    It.IsAny<Message<string, string>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Theory]
        [InlineData(0, "retry.10s")]
        [InlineData(1, "retry.1m")]
        [InlineData(2, "retry.5m")]
        public async Task PublishToRetryTopicAsync_ShouldRouteToCorrectTopic(int retryCount, string expectedSuffix)
        {
            // Arrange
            var originalTopic = "test.topic";
            var expectedTopic = $"{originalTopic}.{expectedSuffix}";

            var deliveryResult = new DeliveryResult<string, string>
            {
                Status = PersistenceStatus.Persisted,
                Topic = expectedTopic
            };

            _producerMock
                .Setup(p => p.ProduceAsync(
                    It.Is<string>(t => t == expectedTopic),
                    It.IsAny<Message<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(deliveryResult);

            // Act
            var result = await _retryProducer.PublishToRetryTopicAsync(
                originalTopic,
                "payload",
                "key",
                retryCount,
                "error",
                CancellationToken.None);

            // Assert
            Assert.True(result);
            _producerMock.Verify(
                p => p.ProduceAsync(
                    It.Is<string>(t => t == expectedTopic),
                    It.IsAny<Message<string, string>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task PublishToRetryTopicAsync_ShouldIncrementRetryCount()
        {
            // Arrange
            var originalTopic = "test.topic";
            var retryCount = 0;
            Message<string, string>? capturedMessage = null;

            var deliveryResult = new DeliveryResult<string, string>
            {
                Status = PersistenceStatus.Persisted
            };

            _producerMock
                .Setup(p => p.ProduceAsync(
                    It.IsAny<string>(),
                    It.IsAny<Message<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, Message<string, string>, CancellationToken>((topic, msg, ct) => capturedMessage = msg)
                .ReturnsAsync(deliveryResult);

            // Act
            await _retryProducer.PublishToRetryTopicAsync(
                originalTopic,
                "payload",
                "key",
                retryCount,
                "error",
                CancellationToken.None);

            // Assert
            Assert.NotNull(capturedMessage);
            var retryCountHeader = capturedMessage!.Headers.FirstOrDefault(h => h.Key == "retry-count");
            Assert.NotNull(retryCountHeader);
            var headerValue = System.Text.Encoding.UTF8.GetString(retryCountHeader!.GetValueBytes());
            Assert.Equal((retryCount + 1).ToString(), headerValue);
        }

        [Fact]
        public async Task PublishToRetryTopicAsync_ShouldSetRetryAtTimestamp()
        {
            // Arrange
            var originalTopic = "test.topic";
            var retryCount = 0;
            var now = DateTimeOffset.UtcNow;
            Message<string, string>? capturedMessage = null;

            var deliveryResult = new DeliveryResult<string, string>
            {
                Status = PersistenceStatus.Persisted
            };

            _producerMock
                .Setup(p => p.ProduceAsync(
                    It.IsAny<string>(),
                    It.IsAny<Message<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, Message<string, string>, CancellationToken>((topic, msg, ct) => capturedMessage = msg)
                .ReturnsAsync(deliveryResult);

            // Act
            await _retryProducer.PublishToRetryTopicAsync(
                originalTopic,
                "payload",
                "key",
                retryCount,
                "error",
                CancellationToken.None);

            // Assert
            Assert.NotNull(capturedMessage);
            var retryAtHeader = capturedMessage!.Headers.FirstOrDefault(h => h.Key == "retry-at");
            Assert.NotNull(retryAtHeader);
            var retryAtStr = System.Text.Encoding.UTF8.GetString(retryAtHeader!.GetValueBytes());
            var retryAt = DateTimeOffset.Parse(retryAtStr);
            // Should be approximately 10 seconds in the future (within 1 second tolerance)
            Assert.True(retryAt >= now.AddSeconds(9) && retryAt <= now.AddSeconds(11),
                $"RetryAt should be approximately 10 seconds in the future. Actual: {retryAt}, Expected range: {now.AddSeconds(9)} - {now.AddSeconds(11)}");
        }

        [Fact]
        public async Task PublishToRetryTopicAsync_WithEnvelope_ShouldUseEnvelopeRetryCount()
        {
            // Arrange
            var originalTopic = "test.topic";
            var envelope = RetryMessageEnvelope.Create(
                originalTopic,
                "payload",
                "key",
                2,
                TimeSpan.FromSeconds(30),
                "error");
            Message<string, string>? capturedMessage = null;

            var deliveryResult = new DeliveryResult<string, string>
            {
                Status = PersistenceStatus.Persisted
            };

            _producerMock
                .Setup(p => p.ProduceAsync(
                    It.IsAny<string>(),
                    It.IsAny<Message<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, Message<string, string>, CancellationToken>((topic, msg, ct) => capturedMessage = msg)
                .ReturnsAsync(deliveryResult);

            // Act
            var result = await _retryProducer.PublishToRetryTopicAsync(
                envelope,
                CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.NotNull(capturedMessage);
            var retryCountHeader = capturedMessage!.Headers.FirstOrDefault(h => h.Key == "retry-count");
            Assert.NotNull(retryCountHeader);
            var headerValue = System.Text.Encoding.UTF8.GetString(retryCountHeader!.GetValueBytes());
            Assert.Equal(envelope.RetryCount.ToString(), headerValue);
        }

        [Fact]
        public async Task PublishToDlqAsync_ShouldRouteToDlqTopic()
        {
            // Arrange
            var originalTopic = "test.topic";
            var expectedTopic = $"{originalTopic}.dlq";

            var deliveryResult = new DeliveryResult<string, string>
            {
                Status = PersistenceStatus.Persisted,
                Topic = expectedTopic
            };

            _producerMock
                .Setup(p => p.ProduceAsync(
                    It.Is<string>(t => t == expectedTopic),
                    It.IsAny<Message<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(deliveryResult);

            // Act
            var result = await _retryProducer.PublishToDlqAsync(
                originalTopic,
                "payload",
                "key",
                3,
                "Max retries exceeded",
                CancellationToken.None);

            // Assert
            Assert.True(result);
            _producerMock.Verify(
                p => p.ProduceAsync(
                    It.Is<string>(t => t == expectedTopic),
                    It.Is<Message<string, string>>(m =>
                        m.Key == "key" &&
                        m.Headers.Any(h => h.Key == "retry-level" && 
                            System.Text.Encoding.UTF8.GetString(h.GetValueBytes()) == "DLQ") &&
                        m.Headers.Any(h => h.Key == "dlq-reason")),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task PublishToDlqAsync_WithEnvelope_ShouldUseEnvelopeData()
        {
            // Arrange
            var envelope = RetryMessageEnvelope.Create(
                "test.topic",
                "payload",
                "key",
                3,
                "Max retries exceeded");

            var deliveryResult = new DeliveryResult<string, string>
            {
                Status = PersistenceStatus.Persisted
            };

            _producerMock
                .Setup(p => p.ProduceAsync(
                    It.IsAny<string>(),
                    It.IsAny<Message<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(deliveryResult);

            // Act
            var result = await _retryProducer.PublishToDlqAsync(
                envelope,
                CancellationToken.None);

            // Assert
            Assert.True(result);
            _producerMock.Verify(
                p => p.ProduceAsync(
                    It.Is<string>(t => t == "test.topic.dlq"),
                    It.IsAny<Message<string, string>>(),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task PublishToRetryTopicAsync_WhenProducerFails_ShouldReturnFalse()
        {
            // Arrange
            _producerMock
                .Setup(p => p.ProduceAsync(
                    It.IsAny<string>(),
                    It.IsAny<Message<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KafkaException(new Error(ErrorCode.Local_Fail, "Test error")));

            // Act
            var result = await _retryProducer.PublishToRetryTopicAsync(
                "test.topic",
                "payload",
                "key",
                0,
                "error",
                CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task PublishToRetryTopicAsync_WhenNotPersisted_ShouldReturnFalse()
        {
            // Arrange
            var deliveryResult = new DeliveryResult<string, string>
            {
                Status = PersistenceStatus.NotPersisted
            };

            _producerMock
                .Setup(p => p.ProduceAsync(
                    It.IsAny<string>(),
                    It.IsAny<Message<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(deliveryResult);

            // Act
            var result = await _retryProducer.PublishToRetryTopicAsync(
                "test.topic",
                "payload",
                "key",
                0,
                "error",
                CancellationToken.None);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task Dispose_ShouldFlushAndDisposeProducer()
        {
            // Arrange - Use the producer first to ensure it's created (lazy initialization)
            var deliveryResult = new DeliveryResult<string, string>
            {
                Status = PersistenceStatus.Persisted
            };

            _producerMock
                .Setup(p => p.ProduceAsync(
                    It.IsAny<string>(),
                    It.IsAny<Message<string, string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(deliveryResult);

            // Create the producer by using it
            await _retryProducer.PublishToRetryTopicAsync(
                "test.topic",
                "payload",
                "key",
                0,
                "error",
                CancellationToken.None);

            // Act
            _retryProducer.Dispose();

            // Assert
            _producerMock.Verify(p => p.Flush(It.IsAny<TimeSpan>()), Times.Once);
            _producerMock.Verify(p => p.Dispose(), Times.Once);
        }

        public void Dispose()
        {
            _retryProducer?.Dispose();
        }
    }
}

