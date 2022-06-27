using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageBus
{
    public interface IMessageContextProvider
    {
        IMessageContext Context { get; }
    }

    public interface IMessageContextFactory
    {
        void SetContext(IMessageContext context);
    }
}
