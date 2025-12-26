using System.Reflection;
using ApiTemplate.EventBus.Domain.Core.Attributes;

namespace ApiTemplate.EventBus.Domain.Core.Events
{
    public static class IntegrationEventExtensions
    {
        public static string? GetTopicName(this IntegrationEvent integrationEvent)
        {
            return integrationEvent.GetType().GetCustomAttribute<KafkaTopicAttribute>()?.Name;
        }
    }
}

