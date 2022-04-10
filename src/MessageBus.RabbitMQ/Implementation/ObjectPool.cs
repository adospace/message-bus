using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageBus.RabbitMQ.Implementation
{
    public class ObjectPool<T>
    {
        public sealed class ObjectPoolRent : IDisposable
        {
            private readonly ObjectPool<T> _owner;

            public ObjectPoolRent(ObjectPool<T> owner, T value)
            {
                _owner = owner;
                Value = value;
            }

            public T Value { get; }

            public void Dispose()
            {
                _owner.Return(Value);
            }
        }

        private readonly ConcurrentBag<T> _objects;
        private readonly Func<T> _objectGenerator;

        public ObjectPool(Func<T> objectGenerator)
        {
            _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
            _objects = new ConcurrentBag<T>();
        }

        public ObjectPoolRent Get() => new(this, _objects.TryTake(out var item) ? item : _objectGenerator());

        private void Return(T item) => _objects.Add(item);
    }
}
