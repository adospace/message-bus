namespace MessageBus
{
    public interface IMessageContext
    {
        void SetValue(string key, object value);

        bool TryGetValue<T>(string key, out T? value);

        int PropertyCount { get; }
    }

    public interface IMessageContextProvider
    {
        IMessageContext Context { get; }
    }
}