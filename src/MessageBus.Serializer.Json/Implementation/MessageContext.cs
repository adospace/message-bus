using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MessageBus.Serializer.Implementation
{
    internal class MessageContext : IMessageContext
    {
        public class ContextPropertyValue
        {
            private readonly byte[]? _serializedValue;
            private readonly object? _value;

            public ContextPropertyValue(string key, byte[] serializedValue)
            {
                Key = key;
                _serializedValue = serializedValue;
            }

            public ContextPropertyValue(string key, object? value)
            {
                Key = key;
                _value = value;
            }

            public string Key { get; }

            public T? GetValueAs<T>(JsonSerializerOptions? serializerOptions = null)
            {
                if (_serializedValue != null)
                {
                    return JsonSerializer.Deserialize<T>(_serializedValue, serializerOptions);
                }

                return (T?)_value;
            }

            public byte[] Serialize(JsonSerializerOptions? serializerOptions = null)
            {
                if (_serializedValue != null)
                {
                    return _serializedValue;
                }

                return JsonSerializer.SerializeToUtf8Bytes(_value, serializerOptions) ?? throw new InvalidOperationException();
            }
        }

        private readonly Dictionary<string, ContextPropertyValue> _values = new();
        private readonly JsonSerializerOptions? _serializerOptions;

        public MessageContext()
        {

        }

        public MessageContext(IReadOnlyDictionary<string, byte[]> values, JsonSerializerOptions? serializerOptions = null)
        {
            _values = new Dictionary<string, ContextPropertyValue>(values
                .Select(_ => new KeyValuePair<string, ContextPropertyValue>(_.Key, new ContextPropertyValue(_.Key, _.Value))));
            _serializerOptions = serializerOptions;
        }

        public int PropertyCount => throw new NotImplementedException();

        public void Add<T>(string key, T value)
        {
            _values.Add(key, new ContextPropertyValue(key, value));
        }

        public void AddOrReplace<T>(string key, T value)
        {
            _values[key] = new ContextPropertyValue(key, value);
        }

        public bool KeyExists(string key)
        {
            return _values.ContainsKey(key);
        }

        public bool Remove(string key)
        {
            return _values.Remove(key);
        }

        public bool TryGetValue<T>(string key, out T? value)
        {
            if (_values.TryGetValue(key, out var propertyValue))
            {
                value = propertyValue.GetValueAs<T>(_serializerOptions);
                return true;
            }

            value = default;
            return false;
        }

        public Dictionary<string, byte[]> ToSerializedValues()
        {
            return _values.ToDictionary(_ => _.Key, _ => _.Value.Serialize());
        }
    }
}
