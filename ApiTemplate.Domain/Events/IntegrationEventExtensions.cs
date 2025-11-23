using System.Reflection;

namespace ApiTemplate.Domain.Events
{
    public static class IntegrationEventExtensions
    {
        public static string GetTopicName(this IntegrationEvent integrationEvent)
        {
            return integrationEvent.GetType().GetCustomAttribute<KafkaTopicAttribute>()?.Name;
        }
    }
}

