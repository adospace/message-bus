using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageBus.RabbitMQ;

public class RabbitMQBusOptions : IBusOptions
{
    /// <summary>
    /// RabbitMQ connection Uri
    /// </summary>
    public Uri Uri { get; set; } = new Uri("amqp://guest:guest@localhost");

    /// <summary>
    /// RabbitMQ host name
    /// </summary>
    public string HostName { get; set; } = "localhost";

    /// <summary>
    /// RabbitMQ port (if not specified use the default)
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Default call timeout for <see cref="IBusClient.Send()"/> and <see cref="IBusClient.SendAndGetReply()"/>/>
    /// </summary>
    public TimeSpan DefaultCallTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// NUmber of handlers called in parallel
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = 1;

    /// <summary>
    /// Time to live (TTL) used for messages set at queue level
    /// <see cref="https://www.rabbitmq.com/ttl.html"/>
    /// </summary>
    public TimeSpan? DefaultTimeToLive { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Expiration time for queue that do not have consumers attached
    /// <see cref="https://www.rabbitmq.com/ttl.html"/>
    /// </summary>
    public TimeSpan? QueueExpiration { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Unique Identifier for the application (useful to identify queues and exchanges created from the bus)
    /// </summary>
    public string? ApplicationId { get; set; }
}
