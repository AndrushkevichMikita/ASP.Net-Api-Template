using Serilog;

namespace ApiTemplate.Domain.Exceptions
{
    public class DomainException : Exception
    {
        public DomainException(string message) : base(message)
        {
            Log.Error(this, Message);
        }

        public DomainException(string message, Exception innerException) : base(message, innerException)
        {
            Log.Error(this, Message);
        }

        public DomainException ThrowAndLog()
        {
            Log.Error(this, Message);
            return this;
        }
    }
}
