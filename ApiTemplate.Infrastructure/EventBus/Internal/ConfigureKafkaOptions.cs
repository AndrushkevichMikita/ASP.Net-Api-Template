using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace ApiTemplate.Infrastructure.EventBus.Internal
{
    internal sealed class ConfigureKafkaOptions :
        IConfigureNamedOptions<ProducerConfig>,
        IConfigureNamedOptions<AdminClientConfig>,
        IConfigureNamedOptions<ConsumerConfig>
    {
        private const int DefaultMessageMaxBytes = 1000000;

        private readonly IOptionsMonitor<KafkaConfiguration> kafkaConfiguration;

        public ConfigureKafkaOptions(IOptionsMonitor<KafkaConfiguration> kafkaConfiguration)
        {
            this.kafkaConfiguration = kafkaConfiguration;
        }

        public void Configure(string name, ProducerConfig options)
        {
            var configuration = kafkaConfiguration.Get(name);

            options.BootstrapServers = configuration.BootstrapServers;
            options.BrokerVersionFallback = configuration.BrokerVersionFallback;
            options.ApiVersionFallbackMs = configuration.ApiVersionFallbackMs;
            options.SaslMechanism = configuration.SaslMechanism;
            options.SecurityProtocol = configuration.SecurityProtocol;
            options.SaslUsername = configuration.SaslUsername;
            options.SaslPassword = configuration.SaslPassword;
            options.MessageMaxBytes = configuration.MessageMaxBytes ?? DefaultMessageMaxBytes;
        }

        public void Configure(ProducerConfig options)
        {
            Configure(Options.DefaultName, options);
        }

        public void Configure(string name, AdminClientConfig options)
        {
            var configuration = kafkaConfiguration.Get(name);

            options.BootstrapServers = configuration.BootstrapServers;
            options.BrokerVersionFallback = configuration.BrokerVersionFallback;
            options.ApiVersionFallbackMs = configuration.ApiVersionFallbackMs;
            options.SaslMechanism = configuration.SaslMechanism;
            options.SecurityProtocol = configuration.SecurityProtocol;
            options.SaslUsername = configuration.SaslUsername;
            options.SaslPassword = configuration.SaslPassword;
        }

        public void Configure(AdminClientConfig options)
        {
            Configure(Options.DefaultName, options);
        }

        public void Configure(string name, ConsumerConfig options)
        {
            var configuration = kafkaConfiguration.Get(name);

            options.BootstrapServers = configuration.BootstrapServers;
            options.BrokerVersionFallback = configuration.BrokerVersionFallback;
            options.ClientId = Guid.NewGuid().ToString();
            options.ApiVersionFallbackMs = configuration.ApiVersionFallbackMs;
            options.SaslMechanism = configuration.SaslMechanism;
            options.SecurityProtocol = configuration.SecurityProtocol;
            options.SaslUsername = configuration.SaslUsername;
            options.SaslPassword = configuration.SaslPassword;
            options.GroupId = configuration.GroupId;
            options.AutoOffsetReset = configuration.AutoOffsetReset;
            options.EnableAutoCommit = true;
            options.EnableAutoOffsetStore = configuration.EnableAutoOffsetStore;
            options.SessionTimeoutMs = configuration.SessionTimeoutMs;
            options.HeartbeatIntervalMs = configuration.HeartbeatIntervalMs;
        }

        public void Configure(ConsumerConfig options)
        {
            Configure(Options.DefaultName, options);
        }
    }
}

