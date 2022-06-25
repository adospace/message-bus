namespace MessageBus;

public interface IMessageSerializer
{
    byte[] Serialize(Message message);

    Message Deserialize(ReadOnlyMemory<byte> serializedMessage, Type targetModelType);
}

