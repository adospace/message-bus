using Newtonsoft.Json;

namespace MessageBus.Serializer.NewtonsoftJson.Implementation;

public class SerializerOptions
{
    public List<JsonConverter> Converters { get; } = new();
}
