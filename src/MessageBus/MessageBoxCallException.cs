using System.Runtime.Serialization;

namespace MessageBus
{
    public class MessageBoxCallException : Exception
    {
        public MessageBoxCallException()
        {
        }

        public MessageBoxCallException(string? message) : base(message)
        {
        }

        public MessageBoxCallException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected MessageBoxCallException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }

}