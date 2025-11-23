using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace ApiTemplate.Infrastructure.EventBus.Internal
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
            };

            return new ProducerBuilder<string, string>(config).Build();
        }
    }
}

