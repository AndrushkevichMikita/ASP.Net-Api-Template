using Newtonsoft.Json;

namespace ApiTemplate.EventBus.Kafka.Internal
{
    internal static class KafkaSerialization
    {
        public static JsonSerializerSettings Settings { get; } = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter>(),
            DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
        };
    }
}

