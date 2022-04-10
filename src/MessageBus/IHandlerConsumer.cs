using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageBus
{
    public interface IHandlerConsumer
    { 
        string Key { get; }
    
        Type ModelType { get; }
    }

    public interface IHandlerConsumerWithoutReply : IHandlerConsumer
    {
        bool IsEventHandler { get; }

        Task OnHandle(object message, CancellationToken cancellationToken = default);
    }

    public interface IHandlerConsumerWithReply : IHandlerConsumer
    {

        Task<byte[]?> OnHandle(object message, CancellationToken cancellationToken = default);
    }
}
