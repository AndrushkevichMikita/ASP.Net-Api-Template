using Confluent.Kafka;
using Microsoft.Extensions.Options;
using Serilog;

namespace ApiTemplate.EventBus.Kafka.Internal
{
    internal sealed class ConsumerFactory : IConsumerFactory
    {
        private readonly IOptions<ConsumerConfig> _options;
        private readonly ILogger _logger;

        public ConsumerFactory(
            IOptions<ConsumerConfig> options,
            ILogger logger)
        {
            _options = options;
            _logger = logger;
        }

        public IConsumer<string, string> Create()
        {
            var config = new ConsumerConfig(_options.Value);

            string hostId = Environment.MachineName;
            string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);

            if (config.GroupId != null)
            {
                config.ClientId = $"{config.GroupId}-{hostId}-{uniqueId}";
            }

            return new ConsumerBuilder<string, string>(config)
                .SetErrorHandler(KafkaErrorHandler.HandleError(_logger))
                .SetPartitionsAssignedHandler((_, partitions) =>
                {
                    _logger.Debug(
                        "Consumer assigned partitions: {Partitions}",
                        string.Join(", ", partitions.Select(p => $"{p.Topic}-{p.Partition}")));

                    Thread.Sleep(100);
                })
                .SetPartitionsRevokedHandler((_, partitions) =>
                {
                    _logger.Debug(
                        "Consumer partitions are being revoked: {Partitions}",
                        string.Join(", ", partitions.Select(p => $"{p.Topic}-{p.Partition}")));
                })
                .Build();
        }
    }
}
