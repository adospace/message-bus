namespace MessageBus;

public interface IMessageSerializerFactory
{
    IMessageSerializer CreateMessageSerializer();
}
