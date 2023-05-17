using Microsoft.Extensions.Logging;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static MessageBus.RabbitMQ.Implementation.ConnectionManager;

namespace MessageBus.RabbitMQ.Implementation;

class ConnectionManager
{
    public class ChannelPool
    {
        public sealed class ChannelPoolRent : IDisposable
        {
            private readonly ChannelPool _owner;
            private IModel _channel;

            public ChannelPoolRent(ChannelPool owner, IModel channel)
            {
                _owner = owner;
                _channel = channel;
            }

            public IBasicProperties CreateBasicProperties() =>
                _channel.CreateBasicProperties();

            public void BasicPublish(string exchange, string routingKey, IBasicProperties basicProperties, ReadOnlyMemory<byte> body)
            {
                try
                {
                    _channel.BasicPublish(
                        exchange: exchange,
                        routingKey: routingKey,
                        basicProperties: basicProperties,
                        body: body);
                }
                catch (Exception)
                {
                    _owner._isFaulted = true;
                    throw;
                }
            }
            public void BasicPublish(string exchange, string routingKey, byte[] body)
            {
                try
                {
                    _channel.BasicPublish(
                        exchange: exchange,
                        routingKey: routingKey,
                        body: body);
                }
                catch (Exception)
                {
                    _owner._isFaulted = true;
                    throw;
                }
            }

            public void Dispose()
            {
                _owner.Return(_channel);
            }
        }

        private readonly ConcurrentBag<IModel> _channelCache = new();
        private int _channelRentedCount;
        private readonly Func<IModel> _objectGenerator;
        private readonly Action _connectionFaultHandler;
        private bool _isFaulted;
        

        public ChannelPool(Func<IModel> objectGenerator, Action connectionFaultHandler)
        {
            _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
            _connectionFaultHandler = connectionFaultHandler;
        }

        public bool IsFaulted => _isFaulted;

        public bool HasRentedChannels => _channelRentedCount > 0;

        private void Return(IModel item)
        {
            _channelCache.Add(item);
            Interlocked.Decrement(ref _channelRentedCount);

            if (_isFaulted && _channelRentedCount == 0)
            {
                _connectionFaultHandler();
            }
        }

        public ChannelPoolRent Get()
        {
            Interlocked.Increment(ref _channelRentedCount);

            return new(this, _channelCache.TryTake(out var item) ? item : _objectGenerator());
        }

        public void Disconnect()
        {
            foreach (var channel in _channelCache)
            {
                channel.Close();
                channel.Dispose();
            }

            _channelCache.Clear();
        }
    }

    class ConsumerWrapper
    {
        private readonly IHandlerConsumer _handlerConsumer;
        private readonly EventingBasicConsumer _consumer;
        private readonly Action<IHandlerConsumer, BasicDeliverEventArgs> _receiveMessageCallback;

        public ConsumerWrapper(string key, IHandlerConsumer handlerConsumer, EventingBasicConsumer consumer, Action<IHandlerConsumer, BasicDeliverEventArgs> receiveMessageCallback)
        {
            Key = key;
            _handlerConsumer = handlerConsumer;
            _consumer = consumer;
            _receiveMessageCallback = receiveMessageCallback;

            _consumer.Received += Consumer_Received;
        }

        private void Consumer_Received(object? sender, BasicDeliverEventArgs ea) 
            => _receiveMessageCallback(_handlerConsumer, ea);

        public string Key { get; }

        internal void Disconnect()
        {
            _consumer.Received -= Consumer_Received;
        }
    }

    private readonly ConnectionFactory _factory;
    private IConnection? _connection;
    private IModel? _replyConsumerChannel;
    private readonly Dictionary<string, object> _queueProperties;
    private ChannelPool? _channelPool;
    private readonly RabbitMQBusOptions _options;
    private readonly IEnumerable<IHandlerConsumer> _handlerConsumers;
    private readonly ILogger<Bus> _logger;
    private readonly Action<IHandlerConsumer, BasicDeliverEventArgs> _receiveMessageCallback;
    private readonly Action<object?, BasicDeliverEventArgs> _replyMessageReceivedCallback;
    private EventingBasicConsumer? _replyConsumer;
    private string? _replyQueueName;
    private readonly ConcurrentDictionary<string, ConsumerWrapper> _consumers = new();
    private bool _isConnectionFaulted;

    public ConnectionManager(
        RabbitMQBusOptions options,
        IEnumerable<IHandlerConsumer> handlerConsumers,
        ILogger<Bus> logger,
        Action<IHandlerConsumer, BasicDeliverEventArgs> receiveMessageCallback,
        Action<object?, BasicDeliverEventArgs> replyMessageReceivedCallback
        )
    {
        _options = options;
        _handlerConsumers = handlerConsumers;
        _logger = logger;
        _receiveMessageCallback = receiveMessageCallback;
        _replyMessageReceivedCallback = replyMessageReceivedCallback;
        _factory = new ConnectionFactory()
        {
            HostName = options.HostName,
            Uri = options.Uri,
            RequestedHeartbeat = TimeSpan.FromSeconds(30)
        };

        _queueProperties = new();

        if (options.QueueExpiration != null)
        {
            _queueProperties["x-expires"] = (int)options.QueueExpiration.Value.TotalMilliseconds;
        }
        if (options.DefaultTimeToLive != null)
        {
            _queueProperties["x-message-ttl"] = (int)options.DefaultTimeToLive.Value.TotalMilliseconds;
        }
    }
    public string ReplyQueueName => _replyQueueName ?? throw new InvalidOperationException();

    private void OnReplyMessageReceived(object? sender, BasicDeliverEventArgs e)
        => _replyMessageReceivedCallback(sender, e);

    private EventingBasicConsumer RegisterConsumerToQueue(string key)
    {
        if (_connection == null || _replyConsumerChannel == null)
        {
            throw new InvalidCastException("Bus not started");
        }

        if (_consumers.ContainsKey(key))
        {
            throw new InvalidOperationException($"Consumer with key '{key}' already registered");
        }

        try
        {
            _logger.LogDebug("Registering consumer queue '{ConsumerKey}'", key);

            _replyConsumerChannel.QueueDeclare(
                queue: _options.ApplicationId == null ? key : $"{_options.ApplicationId}_{key}",
                durable: false,
                exclusive: false,
                autoDelete: false,
                arguments: _queueProperties);

        }
        catch (OperationInterruptedException ex)
        {
            throw new InvalidOperationException($"Unable to create queue '{key}': if any options property (like QueueExpiration or DefaultTimeToLive) is changed ensure that queue wasn't already created with a different value for that properties.", ex);
        }

        var consumer = new EventingBasicConsumer(_replyConsumerChannel);

        _replyConsumerChannel.BasicConsume(
            queue: _options.ApplicationId == null ? key : $"{_options.ApplicationId}_{key}",
            consumer: consumer,
            autoAck: true);

        return consumer;
    }

    private EventingBasicConsumer RegisterConsumerToExchange(string key)
    {
        if (_connection == null || _replyConsumerChannel == null)
        {
            throw new InvalidCastException("Bus not started");
        }

        if (_consumers.ContainsKey(key))
        {
            throw new InvalidOperationException($"Consumer with key '{key}' already registered");
        }

        _logger.LogDebug("Registering consumer exchange '{ConsumerKey}'", key);

        _replyConsumerChannel.ExchangeDeclare(
            exchange: _options.ApplicationId == null ? key : $"{_options.ApplicationId}_{key}",
            type: ExchangeType.Fanout);

        string queueName;
        try
        {
            queueName = _replyConsumerChannel.QueueDeclare(
                arguments: _queueProperties).QueueName;
        }
        catch (OperationInterruptedException ex)
        {
            throw new InvalidOperationException($"Unable to create queue '{key}': if any options property (like QueueExpiration or DefaultTimeToLive) is changed ensure that queue wasn't already created with a different value for that properties.", ex);
        }

        _replyConsumerChannel.QueueBind(queue: queueName,
                            exchange: _options.ApplicationId == null ? key : $"{_options.ApplicationId}_{key}",
                            routingKey: string.Empty);

        var consumer = new EventingBasicConsumer(_replyConsumerChannel);

        _replyConsumerChannel.BasicConsume(
            queue: queueName,
            consumer: consumer,
            autoAck: true);

        return consumer;
    }
    
    private void RegisterConsumer(IHandlerConsumer handlerConsumer)
    {
        EventingBasicConsumer consumer;

        if (handlerConsumer is IHandlerConsumerWithoutReply handlerConsumerWithoutReply)
        {
            consumer = handlerConsumerWithoutReply.IsEventHandler ?
                RegisterConsumerToExchange(handlerConsumer.Key)
                :
                RegisterConsumerToQueue(handlerConsumer.Key);
        }
        else //if (handlerConsumer is IHandlerConsumerWithReply handlerConsumerWithReply)
        {
            consumer = RegisterConsumerToQueue(handlerConsumer.Key);
        }

        if (!_consumers.TryAdd(handlerConsumer.Key, new ConsumerWrapper(
            handlerConsumer.Key,
            handlerConsumer,
            consumer,
            _receiveMessageCallback)))
        {
            throw new InvalidOperationException();
        }
    }


    private void TryConnect()
    {
        var policy = Policy
            .Handle<Exception>()
            .WaitAndRetryForever(retryAttempt =>
                TimeSpan.FromSeconds(retryAttempt > 4 ? 30 : Math.Pow(2, retryAttempt)),
                (ex, time) =>
                {
                    _logger.LogError(ex, "Error while connecting to RabbitMQ (retry in {RetrySeconds} seconds)", time.TotalSeconds);
                    Disconnect();
                });

        policy.Execute(() =>
        {
            Connect();
        });
    }

    private void Connect()
    {
        _logger.LogDebug("Connecting to RabbitMQ: {Uri}", _factory.Uri);
        _connection = _factory.CreateConnection();
        _connection.ConnectionShutdown += OnConnectionShutdown;
        _replyConsumerChannel = _connection.CreateModel();
        _replyConsumerChannel.ModelShutdown += OnReplyConsumerChannelShutdown;
        _replyConsumerChannel.BasicQos(0, 1, false);

        _replyQueueName = _replyConsumerChannel.QueueDeclare(arguments: _queueProperties).QueueName;
        _replyConsumer = new EventingBasicConsumer(_replyConsumerChannel);

        foreach (var handlerConsumer in _handlerConsumers)
        {
            RegisterConsumer(handlerConsumer);
        }

        _replyConsumer.Received += OnReplyMessageReceived;

        _replyConsumerChannel.BasicConsume(
            queue: _replyQueueName,
            consumer: _replyConsumer,
            autoAck: true);

        _channelPool = new ChannelPool(() =>
        {
            if (_connection == null)
            {
                throw new InvalidOperationException();
            }

            var newChannel = _connection.CreateModel();
            newChannel.BasicQos(0, 1, false);
            return newChannel;
        }, HandleConnectionFault);

        _logger.LogDebug("Connected to RabbitMQ: {Uri}", _factory.Uri);
    }

    private void OnReplyConsumerChannelShutdown(object? sender, ShutdownEventArgs e)
    {
        _isConnectionFaulted = true;
        HandleConnectionFault();
    }

    private void OnConnectionShutdown(object? sender, ShutdownEventArgs e)
    {
        _isConnectionFaulted = true;
        HandleConnectionFault();
    }

    private void Disconnect()
    {
        if (_connection != null)
        {
            _connection.ConnectionShutdown -= OnConnectionShutdown;
        }

        if (_replyConsumerChannel != null)
        {
            _replyConsumerChannel.ModelShutdown -= OnReplyConsumerChannelShutdown;
        }

        if (_replyConsumer != null)
        {
            _replyConsumer.Received -= OnReplyMessageReceived;
            _replyConsumer = null;
            _replyQueueName = null;
        }

        foreach (var consumerEntry in _consumers)
        {
            consumerEntry.Value.Disconnect();
        }

        _consumers.Clear();

        if (_channelPool != null)
        {
            _channelPool.Disconnect();
        }        

        _replyConsumerChannel?.Dispose();
        _connection?.Dispose();

        _replyConsumerChannel = null;
        _connection = null;    
    }

    internal void HandleConnectionFault()
    {
        if (_isConnectionFaulted || (_channelPool != null && _channelPool.IsFaulted && !_channelPool.HasRentedChannels))
        {
            Disconnect();
            TryConnect();
        }
    }

    public ChannelPool.ChannelPoolRent GetChannel()
    {
        if (_channelPool == null || _channelPool.IsFaulted)
        {
            throw new InvalidOperationException();
        }

        return _channelPool.Get();
    }

    public void Start()
    {
        if (_connection != null)
        {
            throw new InvalidOperationException();
        }

        TryConnect();
    }

    public void Stop() => Disconnect();


}
