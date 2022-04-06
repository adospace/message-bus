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

namespace MessageBus.RabbitMQ.Implementation;

internal class Bus : IBus, IBusClient
{
    private class RpcCall
    {
        public RpcCall()
        {
            WaitReplyEvent = new AsyncAutoResetEvent();
        }

        public AsyncAutoResetEvent WaitReplyEvent { get; }

        public ReadOnlyMemory<byte> ReplyMessage { get; set; }

        public bool IsException { get; set; }
    }

    private readonly RabbitMQBusOptions _options;
    private readonly IEnumerable<IHandlerConsumer> _handlerConsumers;
    private readonly ILogger<Bus> _logger;
    private readonly ConnectionFactory _factory;
    private readonly IMessageSerializer _messageSerializer;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly ConcurrentBag<string> _consumers = new();
    private string? _replyQueueName;
    private readonly ConcurrentDictionary<string, RpcCall> _waitingCalls = new();

    private AsyncEventingBasicConsumer? _replyConsumer;

    public Bus(
        RabbitMQBusOptions options, 
        IEnumerable<IHandlerConsumer> handlerConsumers,
        IMessageSerializerFactory messageSerializerFactory,
        ILogger<Bus> logger)
    {
        _options = options;
        _handlerConsumers = handlerConsumers;
        _logger = logger;
        _factory = new ConnectionFactory()
        {
            HostName = options.HostName,
            DispatchConsumersAsync = true,
            ConsumerDispatchConcurrency = options.MaxDegreeOfParallelism,
            RequestedHeartbeat = TimeSpan.FromSeconds(30)
        };
        _messageSerializer = messageSerializerFactory.CreateMessageSerializer();
    }

    private Dictionary<string, object> CreateQueuePropertiesFromOptions()
    { 
        var args = new Dictionary<string, object>();
        if (_options.QueueExpiration != null)
        {
            args["x-expires"] = (int)_options.QueueExpiration.Value.TotalMilliseconds;
        }
        if (_options.DefaultTimeToLive != null)
        {
            args["x-message-ttl"] = (int)_options.DefaultTimeToLive.Value.TotalMilliseconds;
        }
        return args;
    }

    public async Task Run(CancellationToken cancellationToken = default)
    {
        if (_connection != null)
        {
            throw new InvalidCastException();
        }

        _connection = _factory.CreateConnection();
        _channel = _connection.CreateModel();
        _channel.BasicQos(0, 1, false);

        _replyQueueName = _channel.QueueDeclare(arguments: CreateQueuePropertiesFromOptions()).QueueName;
        _replyConsumer = new AsyncEventingBasicConsumer(_channel);

        foreach (var handlerConsumer in _handlerConsumers)
        {
            if (handlerConsumer is IHandlerConsumerWithoutReply handlerConsumerWithoutReply)
            {
                var consumer = handlerConsumerWithoutReply.IsEventHandler ?
                    RegisterConsumerToExchange(handlerConsumer.Key)
                    :
                    RegisterConsumerToQueue(handlerConsumer.Key);

                consumer.Received += (s, ea) => ReceivedFromConsumerWithoutReply(handlerConsumerWithoutReply, ea);
            }
            else if (handlerConsumer is IHandlerConsumerWithReply handlerConsumerWithReply)
            {
                var consumer = RegisterConsumerToQueue(handlerConsumer.Key);

                consumer.Received += (s, ea) => ReceivedFromConsumerThatRequireReply(handlerConsumerWithReply, ea);
            }
        }

        _replyConsumer.Received += OnReplyMessageReceived;

        _channel.BasicConsume(
            queue: _replyQueueName,
            consumer: _replyConsumer,
            autoAck: false);

        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {

        }
    }

    private Task OnReplyMessageReceived(object? sender, BasicDeliverEventArgs e)
    {
        if (_waitingCalls.TryGetValue(e.BasicProperties.CorrelationId, out var replyHandler))
        {
            if (e.BasicProperties.IsHeadersPresent() &&
                e.BasicProperties.Headers.TryGetValue("IsException", out var _))
            {
                replyHandler.IsException = true;
            }
            
            replyHandler.ReplyMessage = e.Body;
            replyHandler.WaitReplyEvent.Set();
        }

        return Task.CompletedTask;
    }

    public Task Stop(CancellationToken cancellationToken = default)
    {
        if (_replyConsumer != null)
        {
            _replyConsumer.Received -= OnReplyMessageReceived;
            _replyConsumer = null;
            _replyQueueName = null;
        }

        _channel?.Dispose();
        _connection?.Dispose();

        _channel = null;
        _connection = null;

        return Task.CompletedTask;
    }

    private AsyncEventingBasicConsumer RegisterConsumerToQueue(string key)
    {
        if (_connection == null || _channel == null)
        {
            throw new InvalidCastException("Bus not started");
        }

        if (_consumers.Contains(key))
        {
            throw new InvalidOperationException($"Consumer with key '{key}' already registered");
        }

        try
        {
            _channel.QueueDeclare(
                queue: _options.ApplicationId == null ? key : $"{_options.ApplicationId}_{key}",
                durable: false,
                exclusive: false,
                autoDelete: false, 
                arguments: CreateQueuePropertiesFromOptions());

        }
        catch (OperationInterruptedException ex)
        {
            throw new InvalidOperationException($"Unable to create queue '{key}': if any options property (like QueueExpiration or DefaultTimeToLive) is changed ensure that queue wasn't already created with a different value for that properties.", ex);
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);

        _channel.BasicConsume(
            queue: _options.ApplicationId == null ? key : $"{_options.ApplicationId}_{key}", 
            consumer: consumer,
            autoAck: false);

        _consumers.Add(key);

        return consumer;
    }

    private AsyncEventingBasicConsumer RegisterConsumerToExchange(string key)
    {
        if (_connection == null || _channel == null)
        {
            throw new InvalidCastException("Bus not started");
        }

        if (_consumers.Contains(key))
        {
            throw new InvalidOperationException($"Consumer with key '{key}' already registered");
        }

        _channel.ExchangeDeclare(
            exchange: _options.ApplicationId == null ? key : $"{_options.ApplicationId}_{key}", 
            type: ExchangeType.Fanout);

        string queueName;
        try
        {
            queueName = _channel.QueueDeclare(
                arguments: CreateQueuePropertiesFromOptions()).QueueName;
        }
        catch (OperationInterruptedException ex)
        {
            throw new InvalidOperationException($"Unable to create queue '{key}': if any options property (like QueueExpiration or DefaultTimeToLive) is changed ensure that queue wasn't already created with a different value for that properties.", ex);
        }

        _channel.QueueBind(queue: queueName,
                            exchange: _options.ApplicationId == null ? key : $"{_options.ApplicationId}_{key}",
                            routingKey: string.Empty);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        _channel.BasicConsume(
            queue: queueName,
            consumer: consumer,
            autoAck: false);

        _consumers.Add(key);

        return consumer;
    }


    private async Task ReceivedFromConsumerWithoutReply(IHandlerConsumerWithoutReply handlerConsumer, BasicDeliverEventArgs ea)
    {
        try
        {
            await handlerConsumer.OnHandle(ea.Body);
        }
        catch (MessageBoxCallException ex)
        {
            HandleException(ea, ex);
        }

        if (_connection == null || _channel == null)
        {
            return;
        }

        var props = ea.BasicProperties;
        if (props.ReplyTo != null && props.CorrelationId != null)
        {
            var replyProps = _channel.CreateBasicProperties();
            replyProps.CorrelationId = props.CorrelationId;

            _channel.BasicPublish(
                exchange: string.Empty,
                routingKey: props.ReplyTo,
                basicProperties: replyProps,
                body: Array.Empty<byte>());
        }

        _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);
    }

    private async Task ReceivedFromConsumerThatRequireReply(IHandlerConsumerWithReply handlerConsumer, BasicDeliverEventArgs ea)
    {
        try
        {
            var reply = await handlerConsumer.OnHandle(ea.Body);

            if (_connection == null || _channel == null)
            {
                return;
            }

            if (reply == null)
            {
                reply = Array.Empty<byte>();
            }

            var props = ea.BasicProperties;
            var replyProps = _channel.CreateBasicProperties();
            replyProps.CorrelationId = props.CorrelationId;

            _channel.BasicPublish(
                exchange: string.Empty,
                routingKey: props.ReplyTo,
                basicProperties: replyProps,
                body: reply);        
        }
        catch (MessageBoxCallException ex)
        {
            HandleException(ea, ex);
        }

        if (_connection == null || _channel == null)
        {
            return;
        }

        _channel.BasicAck(
            deliveryTag: ea.DeliveryTag,
            multiple: false);
    }

    private void HandleException(BasicDeliverEventArgs ea, MessageBoxCallException ex)
    {
        if (_connection == null || _channel == null)
        {
            return;
        }

        var props = ea.BasicProperties;

        if (props.ReplyTo != null && props.CorrelationId != null)
        {
            var replyProps = _channel.CreateBasicProperties();
            replyProps.CorrelationId = props.CorrelationId;
            replyProps.Headers = new Dictionary<string, object>
            {
                { "IsException", true }
            };            

            _channel.BasicPublish(
                exchange: string.Empty,
                routingKey: props.ReplyTo,
                basicProperties: replyProps,
                body: Encoding.UTF8.GetBytes(ex.ToString()));
        }
    }

    public Task Publish<T>(T model, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (_connection == null || _channel == null)
        {
            throw new InvalidCastException("Bus not started");
        }

        byte[] modelSerialized;

        try
        {
            modelSerialized = _messageSerializer.Serialize(model ?? throw new InvalidOperationException());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to serialize model value of type {ModelType}", model?.GetType());
            throw;
        }
        var key = typeof(T).FullName ?? throw new InvalidOperationException();
        _channel.BasicPublish(
            exchange: _options.ApplicationId == null ? key : $"{_options.ApplicationId}_{key}",
            routingKey: string.Empty,
            body: modelSerialized);

        return Task.CompletedTask;
    }

    public async Task Send<T>(T model, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (_connection == null || _channel == null)
        {
            throw new InvalidCastException("Bus not started");
        }

        byte[] modelSerialized;

        try
        {
            modelSerialized = _messageSerializer.Serialize(model ?? throw new InvalidOperationException());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to serialize model value of type {ModelType}", model?.GetType());
            throw;
        }

        var props = _channel.CreateBasicProperties();
        var correlationId = Guid.NewGuid().ToString();
        props.CorrelationId = correlationId;
        props.ReplyTo = _replyQueueName;

        var call = new RpcCall();

        _waitingCalls[correlationId] = call;

        var key = typeof(T).FullName ?? throw new InvalidOperationException();

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _options.ApplicationId == null ? key : $"{_options.ApplicationId}_{key}",
            basicProperties: props,
            body: modelSerialized);

        try
        {
            if (!await call.WaitReplyEvent.WaitAsync(cancellationToken).CancelAfter(timeout ?? _options.DefaultCallTimeout, cancellationToken: cancellationToken))
            {
                throw new TimeoutException($"Unable to get a reply to the message '{model.GetType()}' in {timeout ?? _options.DefaultCallTimeout}");
            }
        }
        finally
        {
            _waitingCalls.TryRemove(correlationId, out var _);
        }

        if (call.IsException)
        {
            throw new MessageBoxCallException(Encoding.UTF8.GetString(call.ReplyMessage.ToArray()));
        }

    }

    public async Task<TReply> SendAndGetReply<T, TReply>(T model, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        if (_connection == null || _channel == null)
        {
            throw new InvalidCastException("Bus not started");
        }

        byte[] modelSerialized;

        try
        {
            modelSerialized = _messageSerializer.Serialize(model ?? throw new InvalidOperationException());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to serialize model value of type {ModelType}", model?.GetType());
            throw;
        }

        var props = _channel.CreateBasicProperties();
        var correlationId = Guid.NewGuid().ToString();
        props.CorrelationId = correlationId;
        props.ReplyTo = _replyQueueName;

        var call = new RpcCall();

        _waitingCalls[correlationId] = call;

        var key = typeof(T).FullName ?? throw new InvalidOperationException();

        _channel.BasicPublish(
            exchange: string.Empty,
            routingKey: _options.ApplicationId == null ? key : $"{_options.ApplicationId}_{key}",
            basicProperties: props,
            body: modelSerialized);

        try
        {
            if (!await call.WaitReplyEvent.WaitAsync(cancellationToken).CancelAfter(timeout ?? _options.DefaultCallTimeout, cancellationToken: cancellationToken))
            {
                throw new TimeoutException($"Unable to get a reply to the message '{model.GetType()}' in {timeout ?? _options.DefaultCallTimeout}");
            }
        }
        finally
        {
            _waitingCalls.TryRemove(correlationId, out var _);
        }

        if (call.IsException)
        {
            throw new MessageBoxCallException(Encoding.UTF8.GetString(call.ReplyMessage.ToArray()));
        }

        if (call.ReplyMessage.Length == 0)
        {
            return default!;
        }

        object deserializedReplyModel;

        try
        {
            deserializedReplyModel = _messageSerializer.Deserialize(
                call.ReplyMessage,
                typeof(TReply));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to deserialize reply model of type {ModelType}", typeof(TReply));
            throw;
        }

        return (TReply)deserializedReplyModel;
    }
}
