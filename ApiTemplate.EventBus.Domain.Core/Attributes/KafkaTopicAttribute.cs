using System.Diagnostics.CodeAnalysis;

namespace ApiTemplate.EventBus.Domain.Core.Attributes
{
    [ExcludeFromCodeCoverage]
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class KafkaTopicAttribute : Attribute
    {
        public const string DeadLetterQueue = "queue.consumer.failure";

        public KafkaTopicAttribute(string name, bool isRetryTopic = false)
        {
            Name = name;
            IsRetryTopic = isRetryTopic;
        }

        public string Name { get; }

        public bool IsRetryTopic { get; }
    }
}

