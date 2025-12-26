using ApiTemplate.EventBus.Abstractions;
using ApiTemplate.EventBus.Kafka.Configuration;
using ApiTemplate.EventBus.Kafka.Internal;
using ApiTemplate.EventBus.Kafka.Retry;
using App.Metrics;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Serilog;

namespace ApiTemplate.EventBus.Kafka
{
    public static class EventBusDependencyInjection
    {
        public static IServiceCollection AddKafkaClient(this IServiceCollection services, IConfiguration configuration)
        {
            if (services.Any(sd => sd.ServiceType == typeof(IEventBus)))
            {
                return services;
            }

            // Configure Kafka settings
            services.Configure<KafkaConfiguration>(configuration.GetSection("Kafka"));
            
            // Configure Retry settings
            services.Configure<RetryConfiguration>(configuration.GetSection("Kafka:Retry"));

            // Register common dependencies
            services.AddHealthChecks()
                .AddCheck<IEventBus>("Kafka Client");

            services.AddSingleton<IConfigureOptions<AdminClientConfig>, ConfigureKafkaOptions>();
            services.AddSingleton<IConfigureOptions<ConsumerConfig>, ConfigureKafkaOptions>();
            services.AddSingleton<IConfigureOptions<ProducerConfig>, ConfigureKafkaOptions>();
            services.AddSingleton<IConsumerFactory, ConsumerFactory>();
            services.AddSingleton<IProducerFactory, ProducerFactory>();
            services.AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<AdminClientConfig>>().Value;
                var logger = sp.GetRequiredService<ILogger>();

                return new AdminClientBuilder(options)
                    .SetErrorHandler(KafkaErrorHandler.HandleError(logger))
                    .Build();
            });

            services.AddSingleton<IEventBus>(sp =>
            {
                return ActivatorUtilities.CreateInstance<KafkaClient>(sp);
            });

            services.AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<IOptions<ProducerConfig>>().Value;
                var logger = sp.GetRequiredService<ILogger>();

                return new ProducerBuilder<string, string>(options)
                    .SetErrorHandler(KafkaErrorHandler.HandleError(logger))
                    .Build();
            });

            services.AddSingleton<IEventBusSubscriptionsManager, InMemoryEventBusSubscriptionsManager>();

            services.AddSingleton<IEventPublisher>(sp =>
            {
                return new KafkaEventPublisher(
                    sp.GetRequiredService<IProducer<string, string>>(),
                    sp.GetRequiredService<IMetrics>());
            });

            services.AddSingleton<ISubscriptionsProcessor, SubscriptionsProcessor>();
            services.AddScoped<IEventDispatcher, KafkaEventDispatcher>();

            // Register retry pipeline services
            services.AddSingleton<RetryConsumerFactory>();
            services.AddSingleton<IRetryConsumerFactory>(sp => sp.GetRequiredService<RetryConsumerFactory>());
            services.AddSingleton<IRetryProducer, RetryProducer>();
            services.AddSingleton<IRetryConsumer, RetryConsumer>();
            services.AddSingleton<IDlqConsumer, DlqConsumer>();
            services.AddSingleton<IRetryOrchestrator, RetryOrchestrator>();
            services.AddSingleton<IRetryPipelineService, RetryPipelineService>();

            return services;
        }
    }
}


