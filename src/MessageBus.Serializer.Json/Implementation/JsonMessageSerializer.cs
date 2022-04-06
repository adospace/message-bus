using MessageBus;
using System.Text.Json;

namespace MessageBus.Serializer.Implementation;

internal class JsonMessageSerializer : IMessageSerializer
{
    private readonly JsonSerializerOptions? _serializerOptions;

    public JsonMessageSerializer(JsonSerializerOptions? serializerOptions = null)
    {
        _serializerOptions = serializerOptions;
    }

    public object Deserialize(ReadOnlyMemory<byte> message, Type targetType)
    {
        return JsonSerializer.Deserialize(message.Span, targetType, _serializerOptions) ?? throw new InvalidOperationException();
    }

    public byte[] Serialize(object model)
    {
        return JsonSerializer.SerializeToUtf8Bytes(model, _serializerOptions);
    }
}
