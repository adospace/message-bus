using MessageBus.RabbitMQ.Implementation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MessageBus.RabbitMQ;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection UseRabbitMQ(this IServiceCollection serviceCollection, Action<RabbitMQBusOptions>? configureOptions = null)
    {
        serviceCollection.AddSingleton(sp => 
        {
            var options = new RabbitMQBusOptions();
            configureOptions?.Invoke(options);
            return new Bus(
                options, 
                sp.GetServices<IHandlerConsumer>(),
                sp.GetRequiredService<IMessageSerializerFactory>(),
                sp.GetRequiredService<ILogger<Bus>>());
        });

        serviceCollection.AddSingleton<IBus>(sp => sp.GetRequiredService<Bus>());
        serviceCollection.AddSingleton<IBusClient>(sp => sp.GetRequiredService<Bus>());

        return serviceCollection;
    }
    public static IHostBuilder UseRabbitMQ(this IHostBuilder hostBuilder, Action<RabbitMQBusOptions>? configureOptions = null)
    {
        hostBuilder.ConfigureServices((_, services) => services.UseRabbitMQ(configureOptions));
        return hostBuilder;
    }

}

