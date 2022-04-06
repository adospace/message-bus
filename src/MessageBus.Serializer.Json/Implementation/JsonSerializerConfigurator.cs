using System.Text.Json;

namespace MessageBus.Serializer.Implementation;

internal class JsonSerializerConfigurator
{
    public JsonSerializerConfigurator(Action<JsonSerializerOptions> configuratorAction)
    {
        ConfiguratorAction = configuratorAction;
    }

    public Action<JsonSerializerOptions> ConfiguratorAction { get; }
}
