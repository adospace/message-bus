using MessageBus;
using System.Text.Json;

namespace MessageBus.Serializer.Implementation;

internal class JsonMessageSerializerFactory : IMessageSerializerFactory
{
    private readonly IMessageContextProvider _messageContextProvider;
    private readonly IEnumerable<JsonSerializerConfigurator> _configurators;

    public JsonMessageSerializerFactory(IMessageContextProvider messageContextProvider, IEnumerable<JsonSerializerConfigurator> configurators)
    {
        _messageContextProvider = messageContextProvider;
        _configurators = configurators;
    }

    public IMessageSerializer CreateMessageSerializer()
    {
        JsonSerializerOptions? options = null;
        if (_configurators.Any())
        {
            options = new();
            HashSet<string> alreadyConfigured = new();
            foreach (var configurator in _configurators)
            {
                if (configurator.OptionsSelectorKey != null &&
                    alreadyConfigured.Contains(configurator.OptionsSelectorKey))
                {
                    continue;
                }
                configurator.ConfiguratorAction(options);
            }
        }

        return new JsonMessageSerializer(options);
    }
}

