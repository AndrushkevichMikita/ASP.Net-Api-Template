using ApiTemplate.EventBus.Kafka.Retry;

namespace ApiTemplate.EventBus.Kafka.Tests.Retry
{
    /// <summary>
    /// Unit tests for RetryConfiguration.
    /// Tests retry delay calculations, topic name generation, and retry level determination.
    /// </summary>
    public class RetryConfigurationTests
    {
        [Fact]
        public void GetRetryDelay_ByLevel_ShouldReturnCorrectDelay()
        {
            // Arrange
            var config = new RetryConfiguration
            {
                RetryDelay10Seconds = TimeSpan.FromSeconds(10),
                RetryDelay1Minute = TimeSpan.FromMinutes(1),
                RetryDelay5Minutes = TimeSpan.FromMinutes(5)
            };

            // Act & Assert
            Assert.Equal(TimeSpan.FromSeconds(10), config.GetRetryDelay(RetryLevel.Retry10Seconds));
            Assert.Equal(TimeSpan.FromMinutes(1), config.GetRetryDelay(RetryLevel.Retry1Minute));
            Assert.Equal(TimeSpan.FromMinutes(5), config.GetRetryDelay(RetryLevel.Retry5Minutes));
            Assert.Equal(TimeSpan.Zero, config.GetRetryDelay(RetryLevel.DLQ));
        }

        [Fact]
        public void GetRetryDelay_ByLevel_ShouldThrowForInvalidLevel()
        {
            // Arrange
            var config = new RetryConfiguration();
            var invalidLevel = (RetryLevel)999;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => config.GetRetryDelay(invalidLevel));
        }

        [Theory]
        [InlineData(0, RetryLevel.Retry10Seconds)]
        [InlineData(1, RetryLevel.Retry1Minute)]
        [InlineData(2, RetryLevel.Retry5Minutes)]
        [InlineData(3, RetryLevel.DLQ)]
        [InlineData(4, RetryLevel.DLQ)]
        [InlineData(10, RetryLevel.DLQ)]
        public void GetNextRetryLevel_ShouldReturnCorrectLevel(int retryCount, RetryLevel expectedLevel)
        {
            // Arrange
            var config = new RetryConfiguration();

            // Act
            var level = config.GetNextRetryLevel(retryCount);

            // Assert
            Assert.Equal(expectedLevel, level);
        }

        [Theory]
        [InlineData(0, "00:00:10")]
        [InlineData(1, "00:01:00")]
        [InlineData(2, "00:05:00")]
        [InlineData(3, "00:00:00")] // DLQ has no delay
        [InlineData(4, "00:00:00")] // DLQ has no delay
        public void GetRetryDelay_ByCount_ShouldReturnCorrectDelay(int retryCount, string expectedDelay)
        {
            // Arrange
            var config = new RetryConfiguration
            {
                RetryDelay10Seconds = TimeSpan.FromSeconds(10),
                RetryDelay1Minute = TimeSpan.FromMinutes(1),
                RetryDelay5Minutes = TimeSpan.FromMinutes(5)
            };

            // Act
            var delay = config.GetRetryDelay(retryCount);

            // Assert
            Assert.Equal(TimeSpan.Parse(expectedDelay), delay);
        }

        [Theory]
        [InlineData("test.topic", RetryLevel.Retry10Seconds, "test.topic.retry.10s")]
        [InlineData("test.topic", RetryLevel.Retry1Minute, "test.topic.retry.1m")]
        [InlineData("test.topic", RetryLevel.Retry5Minutes, "test.topic.retry.5m")]
        [InlineData("test.topic", RetryLevel.DLQ, "test.topic.dlq")]
        [InlineData("queue.apitemplate.account_created", RetryLevel.Retry10Seconds, "queue.apitemplate.account_created.retry.10s")]
        public void GetRetryTopicName_ShouldReturnCorrectTopicName(string baseTopic, RetryLevel level, string expectedTopic)
        {
            // Arrange
            var config = new RetryConfiguration();

            // Act
            var topicName = config.GetRetryTopicName(baseTopic, level);

            // Assert
            Assert.Equal(expectedTopic, topicName);
        }

        [Fact]
        public void GetRetryTopicName_ShouldThrowForInvalidLevel()
        {
            // Arrange
            var config = new RetryConfiguration();
            var invalidLevel = (RetryLevel)999;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => config.GetRetryTopicName("test.topic", invalidLevel));
        }

        [Fact]
        public void DefaultValues_ShouldBeSetCorrectly()
        {
            // Arrange & Act
            var config = new RetryConfiguration();

            // Assert
            Assert.Equal(3, config.MaxRetryAttempts);
            Assert.Equal(TimeSpan.FromSeconds(10), config.RetryDelay10Seconds);
            Assert.Equal(TimeSpan.FromMinutes(1), config.RetryDelay1Minute);
            Assert.Equal(TimeSpan.FromMinutes(5), config.RetryDelay5Minutes);
            Assert.True(config.IsRetryEnabled);
            Assert.Equal("my-microservice-retry", config.RetryConsumerGroupId);
            Assert.Equal("my-microservice-dlq", config.DlqConsumerGroupId);
        }
    }
}


