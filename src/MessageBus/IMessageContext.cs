namespace MessageBus
{
    public interface IMessageContext
    {
        bool KeyExists(string key);

        void AddOrReplace<T>(string key, T value);

        void Add<T>(string key, T value);

        bool Remove(string key);

        bool TryGetValue<T>(string key, out T? value);

        int PropertyCount { get; }
    }

    //public class MessageContext : IMessageContext
    //{
    //    private readonly IDictionary<string, object?> _values = new Dictionary<string, object?>();

    //    public MessageContext()
    //    {
    //    }

    //    public MessageContext(IDictionary<string, object?> values)
    //    {
    //        _values = values;
    //    }

    //    public IReadOnlyDictionary<string, object?> Values => (IReadOnlyDictionary<string, object?>)_values;

    //    public int PropertyCount => Values?.Count ?? 0;

    //    public void Add<T>(string key, T value)
    //    {
    //        _values.Add(key, value);
    //    }

    //    public void AddOrReplace<T>(string key, T value)
    //    {
    //        _values[key] = value;
    //    }

    //    public bool KeyExists(string key)
    //    {
    //        return _values.ContainsKey(key);
    //    }

    //    public bool Remove(string key)
    //    {
    //        return _values.Remove(key);
    //    }

    //    public bool TryGetValue<T>(string key, out T? value)
    //    {
    //        value = default;
    //        if (!_values.TryGetValue(key, out var valueAsObject))
    //        {
    //            return false;
    //        }

    //        value = (T?)valueAsObject;

    //        return true;
    //    }
    //}
}