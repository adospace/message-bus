namespace MessageBus
{
    public interface IMessageContext<out T>
    {
        T Model { get; }
    }

}