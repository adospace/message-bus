using System.Text.Json;

namespace MessageBus.Serializer.Implementation;

internal class JsonSerializerConfigurator
{
    public JsonSerializerConfigurator(Action<JsonSerializerOptions> configuratorAction, string? optionsSelectorKey = null)
    {
        ConfiguratorAction = configuratorAction;
        OptionsSelectorKey = optionsSelectorKey;
    }

    public Action<JsonSerializerOptions> ConfiguratorAction { get; }
    public string? OptionsSelectorKey { get; }
}
