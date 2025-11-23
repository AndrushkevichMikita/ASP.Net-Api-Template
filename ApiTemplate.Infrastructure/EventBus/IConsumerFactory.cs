using Confluent.Kafka;

namespace ApiTemplate.Infrastructure.EventBus
{
    public interface IConsumerFactory
    {
        IConsumer<string, string> Create();
    }
}

