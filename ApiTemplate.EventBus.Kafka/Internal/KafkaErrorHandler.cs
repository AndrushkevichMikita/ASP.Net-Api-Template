using Confluent.Kafka;
using Serilog;

namespace ApiTemplate.EventBus.Kafka.Internal
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

