using MessageBus.Serializer.NewtonsoftJson.Implementation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
// ReSharper disable MemberCanBePrivate.Global

namespace MessageBus.Serializer.NewtonsoftJson;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection UseJsonSerializer(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<IMessageSerializerFactory, JsonMessageSerializerFactory>();

        return serviceCollection;        
    }

    public static IServiceCollection ConfigureJsonSerializer(this IServiceCollection serviceCollection, Action<SerializerOptions> optionsConfigureAction)
    {
        serviceCollection.AddTransient(_ => new JsonSerializerConfigurator(optionsConfigureAction));

        return serviceCollection;
    }

    public static IHostBuilder UseJsonSerializer(this IHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureServices((_, services) => services.UseJsonSerializer());

        return hostBuilder;
    }

    public static IHostBuilder ConfigureJsonSerializer(this IHostBuilder hostBuilder, Action<SerializerOptions> optionsConfigureAction)
    {
        hostBuilder.ConfigureServices((_, services) => services.ConfigureJsonSerializer(optionsConfigureAction));

        return hostBuilder;
    }
}
