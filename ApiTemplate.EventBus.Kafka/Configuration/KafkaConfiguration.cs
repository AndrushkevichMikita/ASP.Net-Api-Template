using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using Confluent.Kafka;

namespace ApiTemplate.EventBus.Kafka.Configuration
{
    public class KafkaConfiguration
    {
        [Required]
        public AutoOffsetReset AutoOffsetReset { get; set; }

        [Required]
        public string BootstrapServers { get; set; }

        [Required]
        public string BrokerVersionFallback { get; set; }

        [Required]
        public int ApiVersionFallbackMs { get; set; }

        [Required]
        public string GroupId { get; set; }

        [Required]
        public string SaslUsername { get; set; }

        [Required]
        public string SaslPassword { get; set; }

        [Required]
        public SecurityProtocol SecurityProtocol { get; set; }

        [Required]
        public SaslMechanism SaslMechanism { get; set; }

        public int? MessageMaxBytes { get; set; }

        public bool IsConsumptionEnabled { get; set; } = true;

        [ExcludeFromCodeCoverage]
        public string ClientId { get; set; } = "apitemplate-client";

        [ExcludeFromCodeCoverage]
        public Acks AcksMode { get; set; } = Acks.All;

        [ExcludeFromCodeCoverage]
        public int MessageTimeoutMs { get; set; } = 30000;

        [ExcludeFromCodeCoverage]
        public int RetryBackoffMs { get; set; } = 100;

        [ExcludeFromCodeCoverage]
        public int MessageSendMaxRetries { get; set; } = 3;

        [ExcludeFromCodeCoverage]
        public bool EnableAutoOffsetStore { get; set; } = false;

        [ExcludeFromCodeCoverage]
        public int SessionTimeoutMs { get; set; } = 30000;

        [ExcludeFromCodeCoverage]
        public int HeartbeatIntervalMs { get; set; } = 10000;

        [ExcludeFromCodeCoverage]
        public int MaxRevocationWaitTimeMs { get; set; } = 30000;

        [ExcludeFromCodeCoverage]
        public bool EnableIdempotence { get; set; } = false;

        [ExcludeFromCodeCoverage]
        public int MaxInFlight { get; set; } = 5;
    }
}

