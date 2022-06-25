namespace MessageBus
{
    public record Message(object Model, IMessageContext? Context = null);
}