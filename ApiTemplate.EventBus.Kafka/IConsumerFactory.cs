using Confluent.Kafka;

namespace ApiTemplate.EventBus.Kafka
{
    public interface IConsumerFactory
    {
        IConsumer<string, string> Create();
    }
}

