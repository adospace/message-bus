using System.Text.Json;

namespace MessageBus.Serializer.NewtonsoftJson.Implementation;

internal class JsonSerializerConfigurator
{
    public JsonSerializerConfigurator(Action<SerializerOptions> configuratorAction)
    {
        ConfiguratorAction = configuratorAction;
    }

    public Action<SerializerOptions> ConfiguratorAction { get; }
}
