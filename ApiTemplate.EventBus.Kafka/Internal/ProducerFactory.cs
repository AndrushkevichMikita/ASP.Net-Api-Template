using Confluent.Kafka;
using Microsoft.Extensions.Options;
using ApiTemplate.EventBus.Kafka.Configuration;

namespace ApiTemplate.EventBus.Kafka.Internal
{
    internal class ProducerFactory : IProducerFactory
    {
        private readonly KafkaConfiguration _kafkaConfiguration;

        public ProducerFactory(IOptions<KafkaConfiguration> kafkaConfiguration)
        {
            _kafkaConfiguration = kafkaConfiguration.Value;
        }

        public IProducer<string, string> Create()
        {
            var config = new ProducerConfig
            {
                BootstrapServers = _kafkaConfiguration.BootstrapServers,
                ClientId = _kafkaConfiguration.ClientId,
                Acks = _kafkaConfiguration.AcksMode,
                EnableDeliveryReports = true,
                MessageTimeoutMs = _kafkaConfiguration.MessageTimeoutMs,
                RetryBackoffMs = _kafkaConfiguration.RetryBackoffMs,
                MessageSendMaxRetries = _kafkaConfiguration.MessageSendMaxRetries,
                EnableIdempotence = _kafkaConfiguration.EnableIdempotence,
                MaxInFlight = _kafkaConfiguration.MaxInFlight,
            };

            return new ProducerBuilder<string, string>(config).Build();
        }
    }
}
