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

        var values = JsonSerializer.Deserialize<Dictionary<string, byte[]>>(messageSpan.Slice(4, valuesLength)) ?? throw new InvalidOperationException();

        var messageContext = new MessageContext(values, _serializerOptions);

        var objectModel = JsonSerializer.Deserialize(messageSpan.Slice(4 + valuesLength), targetType, _serializerOptions) ?? throw new InvalidOperationException();

        return new Message(objectModel, messageContext);
    }

    public byte[] Serialize(Message message)
    {
        using var memoryStream = new MemoryStream();
        using var binaryWriter = new BinaryWriter(memoryStream);

        byte[] serializedValues;

        var messageContext = (MessageContext)message.Context;
        
        serializedValues = JsonSerializer.SerializeToUtf8Bytes(messageContext.ToSerializedValues(), _serializerOptions);

        binaryWriter.Write(serializedValues.Length);

        binaryWriter.Write(serializedValues);

        binaryWriter.Write(JsonSerializer.SerializeToUtf8Bytes(message.Model, _serializerOptions));

        binaryWriter.Flush();

        return memoryStream.ToArray();
    }
}
