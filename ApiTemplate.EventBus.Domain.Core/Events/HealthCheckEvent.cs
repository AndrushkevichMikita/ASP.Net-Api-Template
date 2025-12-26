using ApiTemplate.EventBus.Domain.Core.Attributes;

namespace ApiTemplate.EventBus.Domain.Core.Events
{
    [KafkaTopic("test.apitemplate.health_check")]
    public class HealthCheckEvent : IntegrationEvent
    {
    }
}

