using MessageBus.Serializer.Implementation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
// ReSharper disable MemberCanBePrivate.Global

namespace MessageBus.Serializer.Json;

public static class ServiceCollectionExtensions
{
    public static IMessageBusConfigurator UseJsonSerializer(this IMessageBusConfigurator messageBusConfigurator, Action<JsonSerializerOptions>? optionsConfigureAction = null)
    {
        messageBusConfigurator.Services.TryAddSingleton<IMessageSerializerFactory, JsonMessageSerializerFactory>();
        if (optionsConfigureAction != null)
        {
            messageBusConfigurator.Services.AddTransient(_ => new JsonSerializerConfigurator(optionsConfigureAction));
        }

        return messageBusConfigurator;        
    }

    public static IMessageBusConfigurator ConfigureJsonSerializer(this IMessageBusConfigurator messageBusConfigurator, Action<JsonSerializerOptions> optionsConfigureAction)
    {
        messageBusConfigurator.Services.AddTransient(_ => new JsonSerializerConfigurator(optionsConfigureAction));

        return messageBusConfigurator;
    }
}
