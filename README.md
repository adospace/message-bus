# MessageBus

MessageBus is .NET (6+) library that implements the distribuited mediator pattern (RPC + Publish/Subscribe). 

Currently it supports RabbitMQ as message broker and System.Text.Json as serializer.

## Main features:

1. Extremely lightweight and fast to process incoming and outgoing messages
2. Easy to configure, perfectly integrated with the Microsoft dependency injection
3. Supports auto-reconnection by default

## Pulish/Subscribe

```csharp
public record SampleModel();

public class SampleConsumer : 
    IHandler<SampleModel>
{
    public Task Handle(SampleModel message, CancellationToken cancellationToken = default)
    {
        //Handle the SampleModel message
        return Task.CompletedTask;
    }
}
```

```csharp
using var clientHost = Host.CreateDefaultBuilder()
    .AddMessageBus(cfg =>
    {
        cfg.UseRabbitMQ();
        cfg.UseJsonSerializer();
    })
    .Build();

using var consumerHost = Host.CreateDefaultBuilder()
    .AddMessageBus(cfg =>
    {
        cfg.UseRabbitMQ();
        cfg.UseJsonSerializer();
        cfg.AddEventHandler<SampleConsumer, SampleModelPublished>();
    })
    .Build();

await clientHost.StartAsync();
await consumerHost.StartAsync();

var busClient = clientHost.Services.GetRequiredService<IBusClient>();

busClient.Publish(new SampleModelPublished());
```

## RPC

```csharp
public record SampleModel(string Name, string Surname);
public record SampleModelReply(string NameAndSurname);

public class SampleConsumer : 
    IHandler<SampleModel, SampleModelReply>
{
    public Task<SampleModelReply> Handle(SampleModel message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new SampleModelReply($"Hello {message.Name} {message.Surname}!"));
    }
}
```

```csharp
using var clientHost = Host.CreateDefaultBuilder()
    .AddMessageBus(cfg =>
    {
        cfg.UseRabbitMQ();
        cfg.UseJsonSerializer();
    })
    .Build();

using var consumerHost = Host.CreateDefaultBuilder()
    .AddMessageBus(cfg =>
    {
        cfg.UseRabbitMQ();
        cfg.UseJsonSerializer();
        cfg.AddHandler<SampleConsumer, SampleModel, SampleModelReply>();
    })
    .Build();

await clientHost.StartAsync();
await consumerHost.StartAsync();

var busClient = clientHost.Services.GetRequiredService<IBusClient>();
var reply = await busClient.SendAndGetReply<SampleModel, SampleModelReply>(new SampleModel("John", "Smith"));

```
