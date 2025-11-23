using Confluent.Kafka;
using Serilog;

namespace ApiTemplate.Infrastructure.EventBus.Internal
{
    internal static class KafkaErrorHandler
    {
        public static Action<IClient, Error> HandleError(ILogger logger)
        {
            return (_, errorHandler) =>
            {
                if (errorHandler.IsFatal)
                {
                    logger.Error("{error}", errorHandler);
                    throw new KafkaException(errorHandler);
                }
            };
        }
    }
}

