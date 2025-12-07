using ApiTemplate.Infrastructure.EventBus.Internal;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApiTemplate.Infrastructure.EventBus.Retry
{
    /// <summary>
    /// Dependency injection setup for Kafka retry pipeline.
    /// </summary>
    public static class RetryDependencyInjection
    {
        /// <summary>
        /// Adds Kafka retry pipeline services to the service collection.
        /// </summary>
        public static IServiceCollection AddKafkaRetryPipeline(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure retry settings
            services.Configure<RetryConfiguration>(configuration.GetSection("Kafka:Retry"));

            // Register retry services
            services.AddSingleton<RetryConsumerFactory>();
            services.AddSingleton<IRetryProducer, RetryProducer>();
            services.AddSingleton<IRetryConsumer, RetryConsumer>();
            services.AddSingleton<IDlqConsumer, DlqConsumer>();
            services.AddSingleton<IRetryOrchestrator, RetryOrchestrator>();

            return services;
        }
    }
}

