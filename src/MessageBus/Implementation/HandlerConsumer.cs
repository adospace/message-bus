using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageBus.Implementation
{
    internal class HandlerConsumer<T> : IHandlerConsumerWithoutReply where T : class
    {
        //private readonly IMessageSerializer _messageSerializer;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<HandlerConsumer<T>> _logger;
        private readonly ServiceLifetime _serviceLifetime;

        public HandlerConsumer(
            IServiceProvider serviceProvider, 
            IMessageSerializerFactory messageSerializerFactory,
            ILogger<HandlerConsumer<T>> logger,
            bool isEventHandler = false,
            ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        {
            //_messageSerializer = messageSerializerFactory.CreateMessageSerializer();
            _serviceProvider = serviceProvider;
            _logger = logger;
            IsEventHandler = isEventHandler;
            _serviceLifetime = serviceLifetime;
        }

        public string Key => typeof(T).FullName ?? throw new InvalidOperationException();

        public Type ModelType => typeof(T);

        public bool IsEventHandler { get; }

        public async Task OnHandle(object untypedMessage, CancellationToken cancellationToken)
        {
            T message = (T)untypedMessage;
            if (_serviceLifetime == ServiceLifetime.Scoped)
            {
                using var scope = _serviceProvider.CreateScope();
                IHandler<T> handler = scope.ServiceProvider.GetRequiredService<IHandler<T>>();

                await CallHandler(message, handler, cancellationToken);
            }
            else
            {
                IHandler<T> handler = _serviceProvider.GetRequiredService<IHandler<T>>();

                await CallHandler(message, handler, cancellationToken);
            }
        }

        private async Task CallHandler(T message, IHandler<T> handler, CancellationToken cancellationToken)
        {
            try
            {
                await handler.Handle(message, cancellationToken);
                
                _logger.LogTrace("Successfully called handler IHandler<{T}>", typeof(T));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception raised when calling handler IHandler<{HandlerType}> ({Message})", typeof(T), message);

                throw new MessageBoxCallException($"Exception raised when calling handler IHandler<{typeof(T)}>:{Environment.NewLine}{ex}");
            }
        }
    }

    internal class HandlerConsumer<T, TReply> : IHandlerConsumerWithReply where T : class
    {
        private readonly IMessageSerializer _messageSerializer;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<HandlerConsumer<T, TReply>> _logger;
        private readonly ServiceLifetime _serviceLifetime;

        public HandlerConsumer(
            IServiceProvider serviceProvider, 
            IMessageSerializerFactory messageSerializerFactory,
            ILogger<HandlerConsumer<T, TReply>> logger,
            ServiceLifetime serviceLifetime = ServiceLifetime.Scoped)
        {
            _messageSerializer = messageSerializerFactory.CreateMessageSerializer();
            _serviceProvider = serviceProvider;
            _logger = logger;
            _serviceLifetime = serviceLifetime;
        }

        public string Key => typeof(T).FullName ?? throw new InvalidOperationException();

        public Type ModelType => typeof(T);

        public async Task<byte[]?> OnHandle(object untypedMessage, CancellationToken cancellationToken)
        {
            T message = (T)untypedMessage;

            //1. Eseguo la chiamata all'handler
            TReply? replyMessage;

            if (_serviceLifetime == ServiceLifetime.Scoped)
            {
                using var scope = _serviceProvider.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<IHandler<T, TReply>>();

                replyMessage = await CallHandler(message, handler, cancellationToken);
            }
            else
            {
                var handler = _serviceProvider.GetRequiredService<IHandler<T, TReply>>();

                replyMessage = await CallHandler(message, handler, cancellationToken);
            }

            //2. Deserializzo il messaggio di Reply
            if (replyMessage == null)
            {
                return null;
            }

            if (replyMessage != null)
            {
                try
                {
                    return _messageSerializer.Serialize(replyMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Raised exception when serializing reply type {ModelTypeReply} (IHandler<{T}, {TReply}>)", typeof(TReply), typeof(T), typeof(TReply));

                    throw new MessageBoxCallException($"Raised exception when serializing reply type {typeof(TReply)} ({typeof(T)}, {typeof(TReply)})>:{Environment.NewLine}{ex.InnerException}");
                }
            }
            else
            {
                _logger.LogTrace("Reply with no return value");
                return null;
            }
        }

        private async Task<TReply?> CallHandler(T message, IHandler<T, TReply> handler, CancellationToken cancellationToken)
        {
            try
            {
                var replyMessage = await handler.Handle(message, cancellationToken);
                _logger.LogTrace("Successfully called handler IHandler<{T}, {TReply}>", typeof(T), typeof(TReply));

                return replyMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception raised when calling handler IHandler<{T}, {TReply}> ({Message})", typeof(T), typeof(TReply), message);

                throw new MessageBoxCallException($"Exception raised when calling handler IHandler<{typeof(T)}, {typeof(TReply)}>:{Environment.NewLine}{ex}");
            }
        }
    }
}
