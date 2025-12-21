using ApiTemplate.Domain.Events;

namespace ApiTemplate.Presentation.Web.Tests.Integration.Kafka
{
    /// <summary>
    /// Test-specific integration events for Kafka integration tests.
    /// These events are only used in tests and are isolated from production code.
    /// </summary>
    
    [KafkaTopic("test.apitemplate.kafka_test_event")]
    public class KafkaTestEvent : IntegrationEvent
    {
        public KafkaTestEvent(int testId, string testData)
            : base()
        {
            TestId = testId;
            TestData = testData;
        }

        public int TestId { get; set; }
        public string TestData { get; set; }
    }

    [KafkaTopic("test.apitemplate.kafka_retry_test_event")]
    public class KafkaRetryTestEvent : IntegrationEvent
    {
        public KafkaRetryTestEvent(int testId, string email, string firstName, string lastName)
            : base()
        {
            TestId = testId;
            Email = email;
            FirstName = firstName;
            LastName = lastName;
        }

        public int TestId { get; set; }
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
}

