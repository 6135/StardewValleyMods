using System;

using System.Collections.Generic;

#nullable enable

namespace ProfitCalculator
{
    public class Container
    {
        private static readonly Lazy<Container> _instance = new(() => new Container());

        private readonly Dictionary<Type, object> _instances = new();
        private readonly object _lock = new(); // Lock object for synchronization

        private Container()
        { }

        public static Container Instance => _instance.Value;

        public T? GetInstance<T>()
        {
            var type = typeof(T);
            lock (_lock)
            {
                if (!_instances.ContainsKey(type))
                {
                    return default;
                }
                return (T)_instances[type];
            }
        }

        public void RegisterInstance<T>(T instance)
        {
            var type = typeof(T);
            if (instance is null)
            {
                throw new ArgumentNullException(nameof(instance));
            }
            lock (_lock)
            {
                if (!_instances.ContainsKey(type))
                {
                    _instances[type] = instance;
                }
            }
        }

        public void RegisterInstance<T>() where T : new()
        {
            var type = typeof(T);
            var instance = new T();
            lock (_lock)
            {
                if (!_instances.ContainsKey(type))
                {
                    _instances[type] = instance;
                }
            }
        }

        public void UnregisterInstance<T>()
        {
            var type = typeof(T);
            lock (_lock)
            {
                if (_instances.ContainsKey(type))
                {
                    _instances.Remove(type);
                }
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _instances.Clear();
            }
        }
    }
}