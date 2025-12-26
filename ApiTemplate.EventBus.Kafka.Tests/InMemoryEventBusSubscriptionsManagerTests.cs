using ApiTemplate.EventBus.Abstractions;
using ApiTemplate.EventBus.Domain.Core.Attributes;
using ApiTemplate.EventBus.Domain.Core.Events;
using ApiTemplate.EventBus.Kafka;

namespace ApiTemplate.EventBus.Kafka.Tests
{
    /// <summary>
    /// Unit tests for InMemoryEventBusSubscriptionsManager.
    /// Tests subscription management, event type resolution, and handler retrieval.
    /// </summary>
    public class InMemoryEventBusSubscriptionsManagerTests
    {
        [KafkaTopic("test.event1")]
        private class TestEvent1 : IntegrationEvent { }

        [KafkaTopic("test.event2")]
        private class TestEvent2 : IntegrationEvent { }

        private class TestHandler1 : IIntegrationEventHandler<TestEvent1>
        {
            public Task<IntegrationEventResult> HandleAsync(TestEvent1 integrationEvent, Action onComplete, CancellationToken cancellationToken)
                => Task.FromResult(IntegrationEventResult.CreateSuccessfulResult());
        }

        private class TestHandler2 : IIntegrationEventHandler<TestEvent1>
        {
            public Task<IntegrationEventResult> HandleAsync(TestEvent1 integrationEvent, Action onComplete, CancellationToken cancellationToken)
                => Task.FromResult(IntegrationEventResult.CreateSuccessfulResult());
        }

        private class TestHandler3 : IIntegrationEventHandler<TestEvent2>
        {
            public Task<IntegrationEventResult> HandleAsync(TestEvent2 integrationEvent, Action onComplete, CancellationToken cancellationToken)
                => Task.FromResult(IntegrationEventResult.CreateSuccessfulResult());
        }

        [Fact]
        public void IsEmpty_WhenNoSubscriptions_ShouldReturnTrue()
        {
            // Arrange
            var manager = new InMemoryEventBusSubscriptionsManager();

            // Assert
            Assert.True(manager.IsEmpty);
        }

        [Fact]
        public void IsEmpty_WhenHasSubscriptions_ShouldReturnFalse()
        {
            // Arrange
            var manager = new InMemoryEventBusSubscriptionsManager();

            // Act
            manager.AddSubscription<TestEvent1, TestHandler1>();

            // Assert
            Assert.False(manager.IsEmpty);
        }

        [Fact]
        public void AddSubscription_ShouldAddHandler()
        {
            // Arrange
            var manager = new InMemoryEventBusSubscriptionsManager();

            // Act
            manager.AddSubscription<TestEvent1, TestHandler1>();

            // Assert
            Assert.True(manager.HasSubscriptionsForEvent<TestEvent1>());
            var handlers = manager.GetHandlersForEvent<TestEvent1>();
            Assert.Single(handlers);
            Assert.Equal(typeof(TestHandler1), handlers.First().HandlerType);
        }

        [Fact]
        public void AddSubscription_MultipleHandlersForSameEvent_ShouldAddAll()
        {
            // Arrange
            var manager = new InMemoryEventBusSubscriptionsManager();

            // Act
            manager.AddSubscription<TestEvent1, TestHandler1>();
            manager.AddSubscription<TestEvent1, TestHandler2>();

            // Assert
            var handlers = manager.GetHandlersForEvent<TestEvent1>();
            Assert.Equal(2, handlers.Count());
            Assert.Contains(handlers, h => h.HandlerType == typeof(TestHandler1));
            Assert.Contains(handlers, h => h.HandlerType == typeof(TestHandler2));
        }

        [Fact]
        public void AddSubscription_DuplicateHandler_ShouldThrow()
        {
            // Arrange
            var manager = new InMemoryEventBusSubscriptionsManager();
            manager.AddSubscription<TestEvent1, TestHandler1>();

            // Act & Assert
            Assert.Throws<ArgumentException>(() => manager.AddSubscription<TestEvent1, TestHandler1>());
        }

        [Fact]
        public void RemoveSubscription_ShouldRemoveHandler()
        {
            // Arrange
            var manager = new InMemoryEventBusSubscriptionsManager();
            manager.AddSubscription<TestEvent1, TestHandler1>();

            // Act
            manager.RemoveSubscription<TestEvent1, TestHandler1>();

            // Assert
            Assert.False(manager.HasSubscriptionsForEvent<TestEvent1>());
        }

        [Fact]
        public void RemoveSubscription_WhenMultipleHandlers_ShouldRemoveOnlySpecified()
        {
            // Arrange
            var manager = new InMemoryEventBusSubscriptionsManager();
            manager.AddSubscription<TestEvent1, TestHandler1>();
            manager.AddSubscription<TestEvent1, TestHandler2>();

            // Act
            manager.RemoveSubscription<TestEvent1, TestHandler1>();

            // Assert
            Assert.True(manager.HasSubscriptionsForEvent<TestEvent1>());
            var handlers = manager.GetHandlersForEvent<TestEvent1>();
            Assert.Single(handlers);
            Assert.Equal(typeof(TestHandler2), handlers.First().HandlerType);
        }

        [Fact]
        public void RemoveSubscription_WhenNoSubscriptions_ShouldNotThrow()
        {
            // Arrange
            var manager = new InMemoryEventBusSubscriptionsManager();

            // Act & Assert
            manager.RemoveSubscription<TestEvent1, TestHandler1>(); // Should not throw
        }

        [Fact]
        public void GetHandlersForEvent_WhenNoSubscriptions_ShouldThrow()
        {
            // Arrange
            var manager = new InMemoryEventBusSubscriptionsManager();

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => manager.GetHandlersForEvent<TestEvent1>());
        }

        [Fact]
        public void GetHandlersForEvent_ByString_ShouldReturnHandlers()
        {
            // Arrange
            var manager = new InMemoryEventBusSubscriptionsManager();
            manager.AddSubscription<TestEvent1, TestHandler1>();

            // Act
            var handlers = manager.GetHandlersForEvent("test.event1");

            // Assert
            Assert.Single(handlers);
            Assert.Equal(typeof(TestHandler1), handlers.First().HandlerType);
        }

        [Fact]
        public void HasSubscriptionsForEvent_WhenHasSubscriptions_ShouldReturnTrue()
        {
            // Arrange
            var manager = new InMemoryEventBusSubscriptionsManager();
            manager.AddSubscription<TestEvent1, TestHandler1>();

            // Act & Assert
            Assert.True(manager.HasSubscriptionsForEvent<TestEvent1>());
            Assert.True(manager.HasSubscriptionsForEvent("test.event1"));
        }

        [Fact]
        public void HasSubscriptionsForEvent_WhenNoSubscriptions_ShouldReturnFalse()
        {
            // Arrange
            var manager = new InMemoryEventBusSubscriptionsManager();

            // Act & Assert
            Assert.False(manager.HasSubscriptionsForEvent<TestEvent1>());
            Assert.False(manager.HasSubscriptionsForEvent("test.event1"));
        }

        [Fact]
        public void GetEventTypeByName_ShouldReturnCorrectType()
        {
            // Arrange
            var manager = new InMemoryEventBusSubscriptionsManager();
            manager.AddSubscription<TestEvent1, TestHandler1>();

            // Act
            var eventType = manager.GetEventTypeByName("test.event1");

            // Assert
            Assert.Equal(typeof(TestEvent1), eventType);
        }

        [Fact]
        public void GetEventTypeByName_WhenNotRegistered_ShouldThrow()
        {
            // Arrange
            var manager = new InMemoryEventBusSubscriptionsManager();

            // Act & Assert
            Assert.Throws<KeyNotFoundException>(() => manager.GetEventTypeByName("test.event1"));
        }

        [Fact]
        public void GetEventKey_ShouldReturnTopicName()
        {
            // Arrange
            var manager = new InMemoryEventBusSubscriptionsManager();

            // Act
            var key = manager.GetEventKey<TestEvent1>();

            // Assert
            Assert.Equal("test.event1", key);
        }

        [Fact]
        public void GetEventKey_WhenNoAttribute_ShouldReturnNull()
        {
            // Arrange
            var manager = new InMemoryEventBusSubscriptionsManager();

            // Act
            var key = manager.GetEventKey<IntegrationEvent>();

            // Assert
            Assert.Null(key);
        }

        [Fact]
        public void GetSubscribedTopics_ShouldReturnAllTopics()
        {
            // Arrange
            var manager = new InMemoryEventBusSubscriptionsManager();
            manager.AddSubscription<TestEvent1, TestHandler1>();
            manager.AddSubscription<TestEvent2, TestHandler3>();

            // Act
            var topics = manager.GetSubscribedTopics().ToList();

            // Assert
            Assert.Equal(2, topics.Count);
            Assert.Contains("test.event1", topics);
            Assert.Contains("test.event2", topics);
        }

        [Fact]
        public void Clear_ShouldRemoveAllSubscriptions()
        {
            // Arrange
            var manager = new InMemoryEventBusSubscriptionsManager();
            manager.AddSubscription<TestEvent1, TestHandler1>();
            manager.AddSubscription<TestEvent2, TestHandler3>();

            // Act
            manager.Clear();

            // Assert
            Assert.True(manager.IsEmpty);
            Assert.False(manager.HasSubscriptionsForEvent<TestEvent1>());
            Assert.False(manager.HasSubscriptionsForEvent<TestEvent2>());
        }

        [Fact]
        public void OnEventRemoved_WhenLastHandlerRemoved_ShouldRaiseEvent()
        {
            // Arrange
            var manager = new InMemoryEventBusSubscriptionsManager();
            manager.AddSubscription<TestEvent1, TestHandler1>();
            string? removedEvent = null;
            manager.OnEventRemoved += (sender, eventName) => removedEvent = eventName;

            // Act
            manager.RemoveSubscription<TestEvent1, TestHandler1>();

            // Assert
            Assert.Equal("test.event1", removedEvent);
        }

        [Fact]
        public void OnEventRemoved_WhenMultipleHandlersRemain_ShouldNotRaiseEvent()
        {
            // Arrange
            var manager = new InMemoryEventBusSubscriptionsManager();
            manager.AddSubscription<TestEvent1, TestHandler1>();
            manager.AddSubscription<TestEvent1, TestHandler2>();
            bool eventRaised = false;
            manager.OnEventRemoved += (sender, eventName) => eventRaised = true;

            // Act
            manager.RemoveSubscription<TestEvent1, TestHandler1>();

            // Assert
            Assert.False(eventRaised);
        }
    }
}


