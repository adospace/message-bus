using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace MessageBus.RabbitMQ.Implementation;

internal class Bus : IBus
{
    private class RpcCall
    {
        private readonly Bus _bus;
        private readonly Type? _typeofReply;
        private readonly IMessageSerializer _messageSerializer;

        public RpcCall(Bus bus, Type? typeofReply = null)
        {
            _bus = bus;
            _typeofReply = typeofReply;
            _messageSerializer = bus._messageSerializer;

            CorrelationId = Guid.NewGuid().ToString();
        }

        public AsyncAutoResetEvent WaitReplyEvent { get; } = new AsyncAutoResetEvent();

        public Message? ReplyMessage { get; private set; }

        public string? RemoteExceptionStackTrace { get; private set; }

        public bool IsException { get; private set; }

        public Exception? ModelDeserializationException { get; private set; }

        public string CorrelationId { get; }

        public void Execute<T>(byte[] modelSerialized)
        {
            using var channelFromPool = _bus._connectionManager.GetChannel();
            var props = channelFromPool.CreateBasicProperties();
            props.CorrelationId = CorrelationId;
            props.ReplyTo = _bus._connectionManager.ReplyQueueName;

            var key = typeof(T).FullName ?? throw new InvalidOperationException();

            channelFromPool.BasicPublish(
                exchange: string.Empty,
                routingKey: _bus._options.ApplicationId == null ? key : $"{_bus._options.ApplicationId}_{key}",
                basicProperties: props,
                body: modelSerialized);
        }

        public void OnReceivedReply(BasicDeliverEventArgs e)
        {
            if (e.BasicProperties.IsHeadersPresent() &&
                e.BasicProperties.Headers.TryGetValue("IsException", out var _))
            {
                IsException = true;
                RemoteExceptionStackTrace = Encoding.UTF8.GetString(e.Body.Span);
            }
            else
            {
                if (_typeofReply != null && e.Body.Length > 0)
                {
                    try
                    {
                        ReplyMessage = _messageSerializer.Deserialize(
                            e.Body,
                            _typeofReply);
                    }
                    catch (Exception ex)
                    {
                        ModelDeserializationException = ex;
                    }
                }
            }

            WaitReplyEvent.Set();
        }
    }

    private class ReceivedCall
    {
        public IBasicProperties BasicProperties { get; }

        public Message Message { get; }

        public string ConsumerTag { get; }

        public ulong DeliveryTag { get; }

        public string Exchange { get; }

        public bool Redelivered { get; }

        public string RoutingKey { get; }

        public IHandlerConsumer HandlerConsumer { get; }

        public ReceivedCall(IHandlerConsumer handlerConsumer, BasicDeliverEventArgs args, Message message)
        {
            HandlerConsumer = handlerConsumer;
            BasicProperties = args.BasicProperties;
            Message = message;
            ConsumerTag = args.ConsumerTag;
            DeliveryTag = args.DeliveryTag;
            Exchange = args.Exchange;
            Redelivered = args.Redelivered;
            RoutingKey = args.RoutingKey;
        }
    }

    private readonly RabbitMQBusOptions _options;
    private readonly ConnectionManager _connectionManager;
    private readonly IEnumerable<IHandlerConsumer> _handlerConsumers;
    private readonly ILogger<Bus> _logger;
    //private readonly ConnectionFactory _factory;
    private readonly IMessageSerializer _messageSerializer;
    //private IConnection? _connection;
    //private IModel? _replyConsumerChannel;
    //private readonly ConcurrentBag<string> _consumers = new();
    private readonly ConcurrentDictionary<string, RpcCall> _waitingCalls = new();
    //private readonly ObjectPool<IModel> _channelPool;

    //private EventingBasicConsumer? _replyConsumer;
    //private string? _replyQueueName;

    private readonly ActionBlock<ReceivedCall> _incomingCalls;

    //private readonly Dictionary<string, object> _queueProperties;

    public Bus(
        RabbitMQBusOptions options, 
        IEnumerable<IHandlerConsumer> handlerConsumers,
        IMessageSerializerFactory messageSerializerFactory,
        ILogger<Bus> logger)
    {
        _options = options;
        _handlerConsumers = handlerConsumers;
        _logger = logger;
        _connectionManager = new ConnectionManager(
            options,
            handlerConsumers,
            logger,
            OnReceivedMessageFromIncomingCall,
            OnReplyMessageReceived);
        //_factory = new ConnectionFactory()
        //{
        //    HostName = options.HostName,
        //    Uri = options.Uri,
        //    RequestedHeartbeat = TimeSpan.FromSeconds(30)
        //};
        _messageSerializer = messageSerializerFactory.CreateMessageSerializer();
        _incomingCalls = new ActionBlock<ReceivedCall>(OnMessageReceivedFromClient, new ExecutionDataflowBlockOptions
        { 
            MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
        });
        //_channelPool = new ObjectPool<IModel>(() => 
        //{
        //    if (_connection == null)
        //    {
        //        throw new InvalidOperationException();
        //    }

        //    var newChannel = _connection.CreateModel();
        //    newChannel.BasicQos(0, 1, false);
        //    return newChannel;
        //});

        //_queueProperties = new();

        //if (_options.QueueExpiration != null)
        //{
        //    _queueProperties["x-expires"] = (int)_options.QueueExpiration.Value.TotalMilliseconds;
        //}
        //if (_options.DefaultTimeToLive != null)
        //{
        //    _queueProperties["x-message-ttl"] = (int)_options.DefaultTimeToLive.Value.TotalMilliseconds;
        //}
    }

    public Task Start(CancellationToken cancellationToken = default)
    {
        _connectionManager.Start();

        //if (_connection != null)
        //{
        //    throw new InvalidOperationException();
        //}

        //_logger.LogDebug("Connecting to RabbitMQ: {Uri}", _factory.Uri);
        //_connection = _factory.CreateConnection();
        //_replyConsumerChannel = _connection.CreateModel();
        //_replyConsumerChannel.BasicQos(0, 1, false);

        //_replyQueueName = _replyConsumerChannel.QueueDeclare(arguments: _queueProperties).QueueName;
        //_replyConsumer = new EventingBasicConsumer(_replyConsumerChannel);

        //foreach (var handlerConsumer in _handlerConsumers)
        //{
        //    if (handlerConsumer is IHandlerConsumerWithoutReply handlerConsumerWithoutReply)
        //    {
        //        var consumer = handlerConsumerWithoutReply.IsEventHandler ?
        //            RegisterConsumerToExchange(handlerConsumer.Key)
        //            :
        //            RegisterConsumerToQueue(handlerConsumer.Key);

        //        consumer.Received += (s, ea) => OnReceivedMessageFromIncomingCall(handlerConsumerWithoutReply, ea);
        //    }
        //    else if (handlerConsumer is IHandlerConsumerWithReply handlerConsumerWithReply)
        //    {
        //        var consumer = RegisterConsumerToQueue(handlerConsumer.Key);

        //        consumer.Received += (s, ea) => OnReceivedMessageFromIncomingCall(handlerConsumerWithReply, ea);
        //    }
        //}

        //_replyConsumer.Received += OnReplyMessageReceived;

        //_replyConsumerChannel.BasicConsume(
        //    queue: _replyQueueName,
        //    consumer: _replyConsumer,
        //    autoAck: true);

        return Task.CompletedTask;
    }

    public async Task Run(CancellationToken cancellationToken = default)
    {
        if (_connectionManager == null)
        {
            throw new InvalidCastException();
        }

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {

        }
        finally
        {
            _incomingCalls.Complete();
        }
    }

    private void OnReceivedMessageFromIncomingCall(IHandlerConsumer handlerConsumer, BasicDeliverEventArgs ea)
    {
        Message message;
        try
        {
            message = _messageSerializer.Deserialize(ea.Body, handlerConsumer.ModelType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to deserialize model of type {ModelType}", handlerConsumer.ModelType);

            HandleExceptionInCallingInternalConsumer(
                ea.BasicProperties, 
                new MessageBoxCallException($"Unable to deserialize model of type {handlerConsumer.ModelType}:{Environment.NewLine}{ex.InnerException}"));
            return;
        }

        _incomingCalls.Post(new ReceivedCall(handlerConsumer, ea, message));
    }

    private void OnReplyMessageReceived(object? sender, BasicDeliverEventArgs e)
    {
        if (_waitingCalls.TryGetValue(e.BasicProperties.CorrelationId, out var replyHandler))
        {
            replyHandler.OnReceivedReply(e);
        }
    }

    public Task Stop(CancellationToken cancellationToken = default)
    {
        _connectionManager.Stop();
        //if (_replyConsumer != null)
        //{
        //    _replyConsumer.Received -= OnReplyMessageReceived;
        //    _replyConsumer = null;
        //    _replyQueueName = null;
        //}

        //_replyConsumerChannel?.Dispose();
        //_connection?.Dispose();

        //_replyConsumerChannel = null;
        //_connection = null;

        return Task.CompletedTask;
    }

    //private EventingBasicConsumer RegisterConsumerToQueue(string key)
    //{
    //    if (_connectionManager == null)
    //    {
    //        throw new InvalidCastException("Bus not started");
    //    }

    //    if (_consumers.Contains(key))
    //    {
    //        throw new InvalidOperationException($"Consumer with key '{key}' already registered");
    //    }

    //    try
    //    {
    //        _logger.LogDebug("Registering consumer queue '{ConsumerKey}'", key);
            
    //        _replyConsumerChannel.QueueDeclare(
    //            queue: _options.ApplicationId == null ? key : $"{_options.ApplicationId}_{key}",
    //            durable: false,
    //            exclusive: false,
    //            autoDelete: false, 
    //            arguments: _queueProperties);

    //    }
    //    catch (OperationInterruptedException ex)
    //    {
    //        throw new InvalidOperationException($"Unable to create queue '{key}': if any options property (like QueueExpiration or DefaultTimeToLive) is changed ensure that queue wasn't already created with a different value for that properties.", ex);
    //    }

    //    var consumer = new EventingBasicConsumer(_replyConsumerChannel);

    //    _replyConsumerChannel.BasicConsume(
    //        queue: _options.ApplicationId == null ? key : $"{_options.ApplicationId}_{key}", 
    //        consumer: consumer,
    //        autoAck: true);

    //    _consumers.Add(key);

    //    return consumer;
    //}

    //private EventingBasicConsumer RegisterConsumerToExchange(string key)
    //{
    //    if (_connection == null || _replyConsumerChannel == null)
    //    {
    //        throw new InvalidCastException("Bus not started");
    //    }

    //    if (_consumers.Contains(key))
    //    {
    //        throw new InvalidOperationException($"Consumer with key '{key}' already registered");
    //    }

    //    _logger.LogDebug("Registering consumer exchange '{ConsumerKey}'", key);
        
    //    _replyConsumerChannel.ExchangeDeclare(
    //        exchange: _options.ApplicationId == null ? key : $"{_options.ApplicationId}_{key}", 
    //        type: ExchangeType.Fanout);

    //    string queueName;
    //    try
    //    {
    //        queueName = _replyConsumerChannel.QueueDeclare(
    //            arguments: _queueProperties).QueueName;
    //    }
    //    catch (OperationInterruptedException ex)
    //    {
    //        throw new InvalidOperationException($"Unable to create queue '{key}': if any options property (like QueueExpiration or DefaultTimeToLive) is changed ensure that queue wasn't already created with a different value for that properties.", ex);
    //    }

    //    _replyConsumerChannel.QueueBind(queue: queueName,
    //                        exchange: _options.ApplicationId == null ? key : $"{_options.ApplicationId}_{key}",
    //                        routingKey: string.Empty);

    //    var consumer = new EventingBasicConsumer(_replyConsumerChannel);

    //    _replyConsumerChannel.BasicConsume(
    //        queue: queueName,
    //        consumer: consumer,
    //        autoAck: true);

    //    _consumers.Add(key);

    //    return consumer;
    //}

    private async Task OnMessageReceivedFromClient(ReceivedCall receivedCall)
    {
        if (receivedCall.HandlerConsumer is IHandlerConsumerWithoutReply)
        {
            await OnMessageReceivedFromClientWithoutReply(receivedCall);
        }
        else
        {
            await OnMessageReceivedFromClientThatRequireReply(receivedCall);
        }
    }

    private async Task OnMessageReceivedFromClientWithoutReply(ReceivedCall receivedCall)
    {
        var props = receivedCall.BasicProperties;
        try
        {
            _logger.LogDebug("Calling handler for message '{ConsumerKey}' (CorrelationId:{CorrelationId})", receivedCall.HandlerConsumer.Key, props.CorrelationId);

            await ((IHandlerConsumerWithoutReply)receivedCall.HandlerConsumer).OnHandle(receivedCall.Message);
        }
        catch (MessageBoxCallException ex)
        {
            _logger.LogError(ex, "Exception raised when calling handler for model '{ConsumerKey}' (CorrelationId:{CorrelationId})", receivedCall.HandlerConsumer.Key, props.CorrelationId);

            HandleExceptionInCallingInternalConsumer(receivedCall.BasicProperties, ex);
        }

        if (_connectionManager == null)
        {
            return;
        }

        if (props.ReplyTo != null && props.CorrelationId != null)
        {
            using var channelForReply = _connectionManager.GetChannel();
            var replyProps = channelForReply.CreateBasicProperties();
            replyProps.CorrelationId = props.CorrelationId;
            
            _logger.LogDebug("Reply to message '{ConsumerKey}' (CorrelationId:{CorrelationId})", receivedCall.HandlerConsumer.Key, props.CorrelationId);

            channelForReply.BasicPublish(
                exchange: string.Empty,
                routingKey: props.ReplyTo,
                basicProperties: replyProps,
                body: Array.Empty<byte>());
        }
    }

    private async Task OnMessageReceivedFromClientThatRequireReply(ReceivedCall receivedCall)
    {
        var props = receivedCall.BasicProperties;
        try
        {
            _logger.LogDebug("Calling handler for message '{ConsumerKey}' (CorrelationId:{CorrelationId})", receivedCall.HandlerConsumer.Key, props.CorrelationId);

            var reply = await ((IHandlerConsumerWithReply)receivedCall.HandlerConsumer).OnHandle(receivedCall.Message);

            if (_connectionManager == null)
            {
                return;
            }

            reply ??= Array.Empty<byte>();

            using var channelForReply = _connectionManager.GetChannel();
            var replyProps = channelForReply.CreateBasicProperties();
            replyProps.CorrelationId = props.CorrelationId;

            _logger.LogDebug("Reply to message '{ConsumerKey}' (CorrelationId:{CorrelationId})", receivedCall.HandlerConsumer.Key, props.CorrelationId);
            
            channelForReply.BasicPublish(
                exchange: string.Empty,
                routingKey: props.ReplyTo,
                basicProperties: replyProps,
                body: reply);        
        }
        catch (MessageBoxCallException ex)
        {
            _logger.LogError(ex, "Exception raised when calling handler for model '{ConsumerKey}' (CorrelationId:{CorrelationId})", receivedCall.HandlerConsumer.Key, props.CorrelationId);

            HandleExceptionInCallingInternalConsumer(receivedCall.BasicProperties, ex);
        }
    }

    private void HandleExceptionInCallingInternalConsumer(IBasicProperties props, MessageBoxCallException ex)
    {
        if (_connectionManager == null)
        {
            return;
        }

        if (props.ReplyTo != null && props.CorrelationId != null)
        {
            using var channelForReply = _connectionManager.GetChannel();
            var replyProps = channelForReply.CreateBasicProperties();
            replyProps.CorrelationId = props.CorrelationId;
            replyProps.Headers = new Dictionary<string, object>
            {
                { "IsException", true }
            };

            channelForReply.BasicPublish(
                exchange: string.Empty,
                routingKey: props.ReplyTo,
                basicProperties: replyProps,
                body: Encoding.UTF8.GetBytes(ex.ToString()));
        }
    }

    public void Publish(Message message)
    {
        if (_connectionManager == null)
        {
            throw new InvalidCastException("Bus not started");
        }

        if (message.Model == null)
        {
            throw new ArgumentNullException();
        }

        _logger.LogDebug("Publish to IHandler<{T}>", message.Model.GetType());

        byte[] modelSerialized;

        try
        {
            modelSerialized = _messageSerializer.Serialize(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to serialize model value of type {ModelType}", message.Model.GetType());
            throw;
        }

        var key = message.Model.GetType().FullName ?? throw new InvalidOperationException();

        using var channelForPublish = _connectionManager.GetChannel();

        channelForPublish.BasicPublish(
            exchange: _options.ApplicationId == null ? key : $"{_options.ApplicationId}_{key}",
            routingKey: string.Empty,
            body: modelSerialized);
    }

    public async Task Send<T>(Message message, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (_connectionManager == null)
        {
            throw new InvalidCastException("Bus not started");
        }

        if (message.Model == null)
        {
            throw new ArgumentNullException();
        }

        var now = DateTime.Now;
        var call = new RpcCall(this);

        try
        {
            _logger.LogDebug("Calling IHandler<{T}> (CorrelationId={CorrelationId})...", message.Model.GetType(), call.CorrelationId);

            byte[] modelSerialized;

            try
            {
                modelSerialized = _messageSerializer.Serialize(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to serialize model value of type {ModelType}", message.Model.GetType());
                throw;
            }


            _waitingCalls[call.CorrelationId] = call;

            call.Execute<T>(modelSerialized);

            try
            {
                if (!await call.WaitReplyEvent.WaitAsync(cancellationToken).CancelAfter(timeout ?? _options.DefaultCallTimeout, cancellationToken: cancellationToken))
                {
                    throw new TimeoutException($"Unable to get a reply to the message '{message.Model.GetType()}' in {timeout ?? _options.DefaultCallTimeout}");
                }
            }
            finally
            {
                _waitingCalls.TryRemove(call.CorrelationId, out var _);
            }

            if (call.IsException)
            {
                throw new MessageBoxCallException(call.RemoteExceptionStackTrace);
            }
        }
        finally
        {
            _logger.LogDebug("Call to IHandler<{T}> (CorrelationId={CorrelationId}) completed in {CallExecutionTime}", typeof(T), call.CorrelationId, (DateTime.Now - now));
        }
    }

    public async Task<Message> SendAndGetReply<T, TReply>(Message message, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (_connectionManager == null)
        {
            throw new InvalidCastException("Bus not started");
        }

        if (message.Model == null)
        {
            throw new ArgumentNullException();
        }

        var now = DateTime.Now;
        var call = new RpcCall(this, typeof(TReply));
        try
        {
            _logger.LogDebug("Calling IHandler<{T}, {TReply}> (CorrelationId={CorrelationId})...", typeof(T), typeof(TReply), call.CorrelationId);

            byte[] modelSerialized;

            try
            {
                modelSerialized = _messageSerializer.Serialize(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to serialize model value of type {ModelType}", message.Model.GetType());
                throw;
            }


            _waitingCalls[call.CorrelationId] = call;

            call.Execute<T>(modelSerialized);

            try
            {
                if (!await call.WaitReplyEvent.WaitAsync(cancellationToken).CancelAfter(timeout ?? _options.DefaultCallTimeout, cancellationToken: cancellationToken))
                {
                    throw new TimeoutException($"Unable to get a reply to the message '{message.Model.GetType()}' (CorrelationId: {call.CorrelationId}) in {timeout ?? _options.DefaultCallTimeout}");
                }
            }
            finally
            {
                _waitingCalls.TryRemove(call.CorrelationId, out var _);
            }

            if (call.IsException)
            {
                throw new MessageBoxCallException(call.RemoteExceptionStackTrace);
            }

            if (call.ModelDeserializationException != null)
            {
                _logger.LogError(call.ModelDeserializationException, "Unable to deserialize reply model of type {ModelType}", typeof(TReply));
                throw call.ModelDeserializationException;
            }

            if (call.ReplyMessage == null)
            {
                return default!;
            }

            return call.ReplyMessage;
        }
        finally
        {
            _logger.LogDebug("Call to IHandler<{T}, {TReply}> (CorrelationId={CorrelationId}) completed in {CallExecutionTime}", typeof(T), typeof(TReply), call.CorrelationId, (DateTime.Now-now));
        }
    }
}
