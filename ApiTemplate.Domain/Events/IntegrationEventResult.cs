namespace ApiTemplate.Domain.Events
{
    public sealed class IntegrationEventResult
    {
        private IntegrationEventResult(
            bool isSuccessful,
            string message = null,
            bool saveEventToLog = false,
            bool handlingCommitWithinHandler = false,
            Exception exception = null)
        {
            IsSuccessful = isSuccessful;
            Message = message;
            SaveEventToLog = saveEventToLog;
            Exception = exception;
            HandlingCommitWithHandler = handlingCommitWithinHandler;
        }

        public string Message { get; set; }

        public bool IsSuccessful { get; set; }

        public bool SaveEventToLog { get; set; }

        public bool HandlingCommitWithHandler { get; set; }

        public Exception Exception { get; set; }

        public static IntegrationEventResult CreateSuccessfulResult(
            string message = null,
            bool saveEventToLog = false)
        {
            return new IntegrationEventResult(
                true,
                message,
                saveEventToLog,
                false);
        }

        public static IntegrationEventResult CreateSuccessfulResultWithCommitHandled(
            string message = null,
            bool saveEventToLog = false)
        {
            return new IntegrationEventResult(
                true,
                message,
                saveEventToLog,
                true);
        }

        public static IntegrationEventResult CreateFailureResult(
            Exception ex,
            bool saveEventToLog = true,
            string message = null)
        {
            return new IntegrationEventResult(
                false,
                message,
                saveEventToLog,
                false,
                ex);
        }
    }
}

