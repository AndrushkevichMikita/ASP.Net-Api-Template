using Confluent.Kafka;

namespace ApiTemplate.EventBus.Kafka
{
    public interface IProducerFactory
    {
        IProducer<string, string> Create();
    }
}

