using MessageBus.Implementation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MessageBus
{
    public static class ServiceCollectionExtensions
    {
        private class MessageBusConfigurator : IMessageBusConfigurator
        {
            public MessageBusConfigurator(IServiceCollection services)
            {
                Services = services;
            }

            public IServiceCollection Services { get; }
        }

        public static IServiceCollection AddMessageBus(this IServiceCollection serviceCollection, Action<IMessageBusConfigurator> configuratorAction)
        {
            var configurator = new MessageBusConfigurator(serviceCollection);

            configuratorAction(configurator);

            serviceCollection.AddMessageBusBackgroundService();

            return serviceCollection;
        }

        public static IHostBuilder AddMessageBus(this IHostBuilder hostBuilder, Action<IMessageBusConfigurator> configuratorAction)
        {
            hostBuilder.ConfigureServices((_, services) =>
            {
                services.ConfigureMessageBus(configuratorAction);
                services.AddMessageBusBackgroundService();
            });
            return hostBuilder;
        }

        public static IServiceCollection ConfigureMessageBus(this IServiceCollection serviceCollection, Action<IMessageBusConfigurator> configuratorAction)
        {
            var configurator = new MessageBusConfigurator(serviceCollection);

            configuratorAction(configurator);

            return serviceCollection;
        }

        public static IHostBuilder ConfigureMessageBus(this IHostBuilder hostBuilder, Action<IMessageBusConfigurator> configuratorAction)
        {
            hostBuilder.ConfigureServices((_, services) => services.ConfigureMessageBus(configuratorAction));
            return hostBuilder;
        }

        private static IServiceCollection AddMessageBusBackgroundService(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddHostedService<Implementation.BusService>();

            return serviceCollection;
        }

        public static IMessageBusConfigurator AddHandler<TConsumer, T>(this IMessageBusConfigurator messageBusConfigurator, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped) where T : class where TConsumer : class, IHandler<T>
        {
            messageBusConfigurator.Services.TryAdd(new ServiceDescriptor(typeof(IHandler<T>), typeof(TConsumer), serviceLifetime));
            messageBusConfigurator.Services.AddSingleton<IHandlerConsumer>(sp => new HandlerConsumer<T>(
                sp,
                sp.GetRequiredService<IMessageSerializerFactory>(),
                sp.GetRequiredService<ILogger<HandlerConsumer<T>>>(),
                isEventHandler: false,
                serviceLifetime: serviceLifetime));

            return messageBusConfigurator;
        }

        public static IMessageBusConfigurator AddEventHandler<TConsumer, T>(this IMessageBusConfigurator messageBusConfigurator, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped) where T : class where TConsumer : class, IHandler<T>
        {
            messageBusConfigurator.Services.TryAdd(new ServiceDescriptor(typeof(IHandler<T>), typeof(TConsumer), serviceLifetime));
            messageBusConfigurator.Services.AddSingleton<IHandlerConsumer>(sp => new HandlerConsumer<T>(
                sp,
                sp.GetRequiredService<IMessageSerializerFactory>(),
                sp.GetRequiredService<ILogger<HandlerConsumer<T>>>(),
                isEventHandler: true,
                serviceLifetime: serviceLifetime));

            return messageBusConfigurator;
        }

        public static IMessageBusConfigurator AddHandler<TConsumer, T, TReply>(this IMessageBusConfigurator messageBusConfigurator, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped) where T : class where TConsumer : class, IHandler<T, TReply>
        {
            messageBusConfigurator.Services.TryAdd(new ServiceDescriptor(typeof(IHandler<T, TReply>), typeof(TConsumer), serviceLifetime));
            messageBusConfigurator.Services.AddSingleton<IHandlerConsumer>(sp => new HandlerConsumer<T, TReply>(
                sp,
                sp.GetRequiredService<IMessageSerializerFactory>(),
                sp.GetRequiredService<ILogger<HandlerConsumer<T, TReply>>>(),
                serviceLifetime: serviceLifetime));
            return messageBusConfigurator;
        }
    }

}