﻿using MessageBus.RabbitMQ.Implementation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MessageBus.RabbitMQ;

public static class ServiceCollectionExtensions
{
    public static IMessageBusConfigurator UseRabbitMQ(this IMessageBusConfigurator messageBusConfigurator, Action<RabbitMQBusOptions>? configureOptions = null)
    {
        messageBusConfigurator.Services.TryAddSingleton(sp => 
        {
            var options = new RabbitMQBusOptions();
            configureOptions?.Invoke(options);
            return new Bus(
                options, 
                sp.GetServices<IHandlerConsumer>(),
                sp.GetRequiredService<IMessageSerializerFactory>(),
                sp.GetRequiredService<ILogger<Bus>>());
        });

        messageBusConfigurator.Services.TryAddSingleton<IBus>(sp => sp.GetRequiredService<Bus>());
        messageBusConfigurator.Services.TryAddScoped<IBusClient, BusClient>();

        return messageBusConfigurator;
    }
}

