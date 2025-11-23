using App.Metrics;
using App.Metrics.Timer;
using ApiTemplate.Domain.Events;
using ApiTemplate.Infrastructure.EventBus.Internal;
using Confluent.Kafka;
using Newtonsoft.Json;

namespace ApiTemplate.Infrastructure.EventBus
{
    internal sealed class KafkaEventPublisher : IEventPublisher
    {
        private readonly IProducer<string, string> _producer;
        private readonly IMetrics _metrics;

        private readonly TimerOptions _publishOptions = new TimerOptions
        {
            MeasurementUnit = Unit.Events,
            Name = "KafkaEventPublisher.PublishAsync",
            DurationUnit = TimeUnit.Seconds,
            RateUnit = TimeUnit.Seconds,
        };

        public KafkaEventPublisher(
            IProducer<string, string> producer,
            IMetrics metrics)
        {
            _producer = producer;
            _metrics = metrics;
        }

        public Task<bool> PublishAsync(
            IntegrationEvent @event,
            CancellationToken cancellationToken)
        {
            if (@event == null)
            {
                throw new ArgumentNullException(nameof(@event));
            }

            return PublishInternalAsync(@event, cancellationToken);
        }

        private async Task<bool> PublishInternalAsync(
            IntegrationEvent @event,
            CancellationToken cancellationToken)
        {
            using (_metrics.Measure.Timer.Time(
                _publishOptions,
                new MetricTags(
                    "topic",
                    @event.GetTopicName())))
            {
                var result = await _producer.ProduceAsync(
                                @event.GetTopicName(),
                                new Message<string, string>
                                {
                                    Key = @event.ComputedPartitionKey,
                                    Value = JsonConvert.SerializeObject(@event, KafkaSerialization.Settings),
                                },
                                cancellationToken);

                return result.Status == PersistenceStatus.Persisted;
            }
        }
    }
}

