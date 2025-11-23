namespace ApiTemplate.Domain.Events
{
    [KafkaTopic("test.apitemplate.health_check")]
    public class HealthCheckEvent : IntegrationEvent
    {
    }
}

