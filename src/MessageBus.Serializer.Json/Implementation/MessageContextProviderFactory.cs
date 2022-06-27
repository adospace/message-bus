using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageBus.Serializer.Implementation
{
    internal class MessageContextProvider : IMessageContextProvider, IMessageContextFactory
    {
        public MessageContextProvider()
        {
            Context = new MessageContext();
        }

        public IMessageContext Context { get; private set; }

        public void SetContext(IMessageContext messageContext)
        {
            Context = (messageContext as MessageContext) ?? throw new InvalidOperationException();
        }
    }
}
