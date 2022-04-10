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

        //public static IHostBuilder AddMessageBusBackgroundService(this IHostBuilder hostBuilder)
        //{
        //    hostBuilder.ConfigureServices((ctx, services) => services.AddMessageBusBackgroundService());
        //    return hostBuilder;
        //}

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

        //public static IHostBuilder AddHandler<TConsumer, T>(this IHostBuilder hostBuilder, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped) where T : class where TConsumer : class, IHandler<T>, new()
        //{
        //    hostBuilder.ConfigureServices((ctx, services) => services.AddHandler<TConsumer, T>(serviceLifetime));
        //    return hostBuilder;
        //}
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

        //public static IHostBuilder AddEventHandler<TConsumer, T>(this IHostBuilder hostBuilder, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped) where T : class where TConsumer : class, IHandler<T>, new()
        //{
        //    hostBuilder.ConfigureServices((ctx, services) => services.AddEventHandler<TConsumer, T>(serviceLifetime));
        //    return hostBuilder;
        //}

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

        //public static IHostBuilder AddHandler<TConsumer, T, TReply>(this IHostBuilder hostBuilder, ServiceLifetime serviceLifetime = ServiceLifetime.Scoped) where T : class where TConsumer : class, IHandler<T, TReply>, new()
        //{
        //    hostBuilder.ConfigureServices((ctx, services) => services.AddHandler<TConsumer, T, TReply>(serviceLifetime));
        //    return hostBuilder;
        //}


        //private static readonly Type MessageContextType = typeof(MessageContext<>);
        //private static readonly Type TaskType = typeof(Task<>);

        //public static IServiceCollection AddConsumer<T>(this IServiceCollection serviceCollection, ServiceLifetime lifetime = ServiceLifetime.Scoped) where T : class
        //{
        //    serviceCollection.Add(new ServiceDescriptor(typeof(T), typeof(T), lifetime));

        //    //AddHandlersOfConsumer<T>(serviceCollection);

        //    return serviceCollection;
        //}
        //public static IServiceCollection AddConsumer<T>(this IServiceCollection serviceCollection, T instance) where T : class
        //{
        //    serviceCollection.Add(new ServiceDescriptor(typeof(T), instance));

        //    //AddHandlersOfConsumer(serviceCollection, instance);

        //    return serviceCollection;
        //}

        //private static void AddHandlersOfConsumer<T>(this IServiceCollection serviceCollection, T? instance = null) where T : class
        //{
        //    var typeOfIHandler = typeof(IHandler);
        //    var listOfHandlerTypes = typeof(T).GetInterfaces().Where(_ => _ != typeOfIHandler && typeOfIHandler.IsAssignableFrom(_)).ToArray();

        //    foreach (var handlerType in listOfHandlerTypes)
        //    {
        //        var typeOfModel = handlerType.GenericTypeArguments[0];

        //        var messageContextActualType = MessageContextType.MakeGenericType(new[] { typeOfModel });
        //        var handleMethod = handlerType.GetMethod("Handle") ?? throw new InvalidOperationException();
        //        if (handlerType.GenericTypeArguments.Length == 1)
        //        {
        //            serviceCollection.AddSingleton<IMessageReceiverCallback>(sp => new MessageReceiverCallbackWithoutReturnValue(
        //                typeOfModel, async (_, model, cancellationToken) =>
        //                {
        //                    using var scope = sp.CreateScope();
        //                    var handler = instance ?? scope.ServiceProvider.GetRequiredService<T>();
        //                    var messageContext = Activator.CreateInstance(messageContextActualType, model);
        //                    await (Task)(handleMethod.Invoke(handler, new[] { messageContext, cancellationToken }) ?? throw new InvalidOperationException());
        //                }));
        //        }
        //        else
        //        {
        //            var typeOfReplyModel = handlerType.GenericTypeArguments[1];
        //            var returnTaskType = TaskType.MakeGenericType(new[] { typeOfReplyModel });
        //            var resultProperty = returnTaskType.GetProperty("Result") ?? throw new InvalidOperationException();

        //            serviceCollection.AddSingleton<IMessageReceiverCallback>(sp => new MessageReceiverCallbackWithReturnValue(
        //                typeOfModel, async (_, model, cancellationToken) =>
        //                {
        //                    using var scope = sp.CreateScope();
        //                    var handler = instance ?? scope.ServiceProvider.GetRequiredService<T>();
        //                    var messageContext = Activator.CreateInstance(messageContextActualType, model);
        //                    var task = (Task)(handleMethod.Invoke(handler, new[] { messageContext, cancellationToken }) ?? throw new InvalidOperationException());
        //                    await task;

        //                    return resultProperty.GetValue(task);
        //                }));
        //        }
        //    }

        //}

        //public static IHostBuilder AddConsumer<T>(this IHostBuilder hostBuilder, ServiceLifetime lifetime = ServiceLifetime.Scoped) where T : class
        //{
        //    hostBuilder.ConfigureServices((_, services) => services.AddConsumer<T>(lifetime));

        //    return hostBuilder;
        //}
        //public static IHostBuilder AddConsumer<T>(this IHostBuilder hostBuilder, T instance) where T : class
        //{
        //    hostBuilder.ConfigureServices((_, services) => services.AddConsumer(instance));

        //    return hostBuilder;
        //}
    }

}