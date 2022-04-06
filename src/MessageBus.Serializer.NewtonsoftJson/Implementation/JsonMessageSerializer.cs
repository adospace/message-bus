using MessageBus;
using Newtonsoft.Json;

namespace MessageBus.Serializer.NewtonsoftJson.Implementation;

internal class JsonMessageSerializer : IMessageSerializer
{
    private readonly SerializerOptions? _serializerOptions;
    private readonly Newtonsoft.Json.JsonSerializer _serializer;

    public JsonMessageSerializer(SerializerOptions? serializerOptions = null)
    {
        _serializerOptions = serializerOptions;
        _serializer = new Newtonsoft.Json.JsonSerializer();       

    }

    public object Deserialize(ReadOnlyMemory<byte> message, Type targetType)
    {
        throw new NotImplementedException();
    }

    public byte[] Serialize(object model)
    {
        throw new NotImplementedException();
    }
}
