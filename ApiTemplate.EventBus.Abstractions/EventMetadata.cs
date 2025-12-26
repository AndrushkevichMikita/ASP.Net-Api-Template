using ApiTemplate.EventBus.Domain.Core.Events;

namespace ApiTemplate.EventBus.Abstractions
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

        public EventMetadata(string topicName, string computedPartitionKey, int partitionId = -1, long partitionOffset = -1, object? headers = null)
        {
            TopicName = topicName;
            PartitionOffset = partitionOffset;
            PartitionKey = computedPartitionKey;
            PartitionId = partitionId;
            Headers = headers;
        }

        public string? TopicName { get; }

        public string PartitionKey { get; }

        public int PartitionId { get; }

        public long PartitionOffset { get; }

        /// <summary>
        /// Message headers (optional, available when consuming from message broker).
        /// Type is object? to avoid coupling to specific broker implementations (e.g., Confluent.Kafka.Headers).
        /// </summary>
        public object? Headers { get; }
    }
}