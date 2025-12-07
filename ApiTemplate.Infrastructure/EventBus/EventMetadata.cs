using ApiTemplate.Domain.Events;
using Confluent.Kafka;

namespace ApiTemplate.Infrastructure.EventBus
{
    public class EventMetadata
    {
        public EventMetadata(IntegrationEvent integrationEvent, int partitionId = -1, long partitionOffset = -1)
        {
            TopicName = integrationEvent.GetTopicName();
            PartitionKey = integrationEvent.ComputedPartitionKey;
            PartitionId = partitionId;
            PartitionOffset = partitionOffset;
        }

        public EventMetadata(string topicName, string computedPartitionKey, int partitionId = -1, long partitionOffset = -1, Headers headers = null)
        {
            TopicName = topicName;
            PartitionOffset = partitionOffset;
            PartitionKey = computedPartitionKey;
            PartitionId = partitionId;
            Headers = headers;
        }

        public string TopicName { get; }

        public string PartitionKey { get; }

        public int PartitionId { get; }

        public long PartitionOffset { get; }

        /// <summary>
        /// Kafka message headers (optional, available when consuming from Kafka).
        /// </summary>
        public Headers Headers { get; }
    }
}

