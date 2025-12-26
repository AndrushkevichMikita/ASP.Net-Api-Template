using ApiTemplate.EventBus.Abstractions;
using ApiTemplate.EventBus.Domain.Core.Events;
using Microsoft.Extensions.DependencyInjection;

namespace ApiTemplate.EventBus.Kafka
{
    internal sealed class KafkaEventDispatcher : IEventDispatcher
    {
        private readonly IServiceProvider _serviceProvider;

        public KafkaEventDispatcher(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task<IntegrationEventResult> DispatchAsync(
            IntegrationEvent @event,
            Type eventType,
            Type handlerType,
            Action commitAction,
            CancellationToken cancellationToken)
        {
            var handler = _serviceProvider.GetRequiredService(handlerType);
            var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);

            await Task.Yield();

            return await (Task<IntegrationEventResult>)concreteType
                .GetMethod("HandleAsync")!
                .Invoke(
                    handler,
                    new object[]
                    {
                        @event,
                        commitAction,
                        cancellationToken,
                    })!;
        }
    }
}
