using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageBus.RabbitMQ.Implementation
{
    internal class BusClient : IBusClient
    {
        private readonly Bus _bus;
        private readonly IMessageContextProvider _messageContextProvider;
        private readonly IMessageContextFactory _messageContextFactory;

        public BusClient(IBus bus, IMessageContextProvider messageContextProvider, IMessageContextFactory messageContextFactory)
        {
            _bus = (Bus)bus;
            _messageContextProvider = messageContextProvider;
            _messageContextFactory = messageContextFactory;
        }

        public void Publish<T>(T model)
        {
            _bus.Publish(new Message(model ?? throw new InvalidOperationException("Model can't be null"), _messageContextProvider.Context));
        }

        public async Task Send<T>(T model, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            await _bus.Send<T>(new Message(model ?? throw new InvalidOperationException("Model can't be null"), _messageContextProvider.Context), timeout, cancellationToken);
        }

        public async Task<TReply> SendAndGetReply<T, TReply>(T model, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
        {
            var replyMessage = await _bus.SendAndGetReply<T, TReply>(new Message(model ?? throw new InvalidOperationException("Model can't be null"), _messageContextProvider.Context), timeout, cancellationToken);

            _messageContextFactory.SetContext(replyMessage.Context);

            return (TReply)replyMessage.Model;
        }
    }
}
