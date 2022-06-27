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

        messageBusConfigurator.Services.TryAddScoped<MessageContextProvider>();
        messageBusConfigurator.Services.TryAddScoped<IMessageContextProvider>(sp => sp.GetRequiredService<Implementation.MessageContextProvider>());
        messageBusConfigurator.Services.TryAddScoped<IMessageContextFactory>(sp => sp.GetRequiredService<Implementation.MessageContextProvider>());

        return messageBusConfigurator;
    }

    public static IMessageBusConfigurator ConfigureJsonSerializer(this IMessageBusConfigurator messageBusConfigurator, Action<JsonSerializerOptions> optionsConfigureAction)
    {
        messageBusConfigurator.Services.AddTransient(_ => new JsonSerializerConfigurator(optionsConfigureAction));

        return messageBusConfigurator;
    }

    public static IMessageBusConfigurator ConfigureJsonSerializer(this IMessageBusConfigurator messageBusConfigurator, string optionsSelectorKey, Action<JsonSerializerOptions> optionsConfigureAction)
    {
        if (string.IsNullOrWhiteSpace(optionsSelectorKey))
        {
            throw new ArgumentException("OptionsSelectorKey must be not null, empty or whitespace", nameof(optionsSelectorKey));
        }

        messageBusConfigurator.Services.AddTransient(_ => new JsonSerializerConfigurator(optionsConfigureAction, optionsSelectorKey));

        return messageBusConfigurator;
    }

    public static IServiceCollection ConfigureMessageBusJsonSerializer(this IServiceCollection services, Action<JsonSerializerOptions> optionsConfigureAction)
    {
        services.AddTransient(_ => new JsonSerializerConfigurator(optionsConfigureAction));

        return services;
    }

    public static IServiceCollection ConfigureMessageBusJsonSerializer(this IServiceCollection services, string optionsSelectorKey, Action<JsonSerializerOptions> optionsConfigureAction)
    {
        if (string.IsNullOrWhiteSpace(optionsSelectorKey))
        {
            throw new ArgumentException("Must be not null, empty or whitespace", nameof(optionsSelectorKey));
        }

        services.AddTransient(_ => new JsonSerializerConfigurator(optionsConfigureAction, optionsSelectorKey));

        return services;
    }

}
