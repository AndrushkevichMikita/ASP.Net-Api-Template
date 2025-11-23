using Confluent.Kafka;

namespace ApiTemplate.Infrastructure.EventBus
{
    public interface IProducerFactory
    {
        IProducer<string, string> Create();
    }
}

