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

    public Message Deserialize(ReadOnlyMemory<byte> message, Type targetType)
    {
        throw new NotImplementedException();
    }

    public byte[] Serialize(Message model)
    {
        throw new NotImplementedException();
    }
}
