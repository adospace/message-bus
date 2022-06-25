using MessageBus;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MessageBus.Serializer.Implementation;

internal class JsonMessageSerializer : IMessageSerializer
{
    private readonly JsonSerializerOptions? _serializerOptions;

    public JsonMessageSerializer(JsonSerializerOptions? serializerOptions = null)
    {
        _serializerOptions = serializerOptions;
    }

    public Message Deserialize(ReadOnlyMemory<byte> bytes, Type targetType)
    {
        var messageSpan = bytes.Span;

        int valuesLength = BitConverter.ToInt32(messageSpan.Slice(0, 4));

        if (valuesLength > 0)
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, byte[]>>(messageSpan.Slice(4, valuesLength)) ?? throw new InvalidOperationException();

            var messageContext = new MessageContext(values, _serializerOptions);

            var objectModel = JsonSerializer.Deserialize(messageSpan.Slice(4 + valuesLength), targetType, _serializerOptions) ?? throw new InvalidOperationException();

            return new Message(objectModel, messageContext);
        }
        else
        {
            var objectModel = JsonSerializer.Deserialize(messageSpan.Slice(4), targetType, _serializerOptions) ?? throw new InvalidOperationException();

            return new Message(objectModel);
        }
    }

    public byte[] Serialize(Message message)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);

        if (message.Context != null)
        {
            var serializedValues = JsonSerializer.SerializeToUtf8Bytes(((MessageContext)message.Context).Values, _serializerOptions);

            binaryWriter.Write(serializedValues.Length);

            binaryWriter.Write(serializedValues);
        }
        else
        {
            binaryWriter.Write(0);
        }

        binaryWriter.Write(JsonSerializer.SerializeToUtf8Bytes(message.Model, _serializerOptions));

        binaryWriter.Flush();

        return memoryStream.ToArray();
    }
}

internal class MessageContext : IMessageContext
{
    private readonly JsonSerializerOptions? _serializerOptions;
    
    public MessageContext(JsonSerializerOptions? serializerOptions)
    {
        _serializerOptions = serializerOptions;
    }

    public MessageContext(Dictionary<string, byte[]> values, JsonSerializerOptions? serializerOptions)
    {
        Values = values;
        _serializerOptions = serializerOptions;
    }

    public Dictionary<string, byte[]> Values { get; set; } = new();
    
    public int PropertyCount => Values?.Count ?? 0;

    public void SetValue(string key, object value)
    {
        Values[key] = JsonSerializer.SerializeToUtf8Bytes(value, _serializerOptions);
    }

    public bool TryGetValue<T>(string key, out T? value)
    {
        if (!Values.TryGetValue(key, out byte[]? valueSerializedBytes))
        {
            value = default;
            return false;
        }

        value = JsonSerializer.Deserialize<T>(valueSerializedBytes);
        return true;
    }
}

//internal class MessageContextProvider : IMessageContextProvider
//{
//    public IMessageContext Context { get; set; } = new MessageContext();
//}
