using System;
using System.Collections.Generic;

#nullable enable

namespace ProfitCalculator.main.memory
{
    /// <summary>
    /// The <see cref="Container"/> class is a thread-safe singleton that manages instances of various types.
    /// It provides methods to register, retrieve, and manage these instances.
    /// </summary>
    public class Container
    {
        private static readonly Lazy<Container> _instance = new(() => new Container());

        private readonly Dictionary<string, object> _instances = new();
        private readonly object _lock = new(); // Lock object for synchronization

        /// <summary>
        /// Private constructor to prevent direct instantiation.
        /// </summary>
        private Container()
        { }

        /// <summary>
        /// Gets the singleton instance of the <see cref="Container"/> class.
        /// </summary>
        public static Container Instance => _instance.Value;

        /// <summary>
        /// Retrieves an instance of the specified type <typeparamref name="T"/> from the container.
        /// </summary>
        /// <typeparam name="T">The type of the instance to retrieve.</typeparam>
        /// <param name="key">The unique key associated with the instance.</param>
        /// <returns>The instance of type <typeparamref name="T"/> if found; otherwise, the default value for type <typeparamref name="T"/>.</returns>
        public T? GetInstance<T>(string key)
        {
            var typeKey = GetTypeKey<T>(key);
            lock (_lock)
            {
                if (!_instances.ContainsKey(typeKey))
                {
                    return default;
                }
                return (T)_instances[typeKey];
            }
        }

        /// <summary>
        /// Registers an instance of the specified type <typeparamref name="T"/> in the container.
        /// </summary>
        /// <typeparam name="T">The type of the instance to register.</typeparam>
        /// <param name="instance">The instance to register.</param>
        /// <param name="key">The unique key associated with the instance.</param>
        /// <exception cref="ArgumentNullException">Thrown when the instance is null.</exception>
        public void RegisterInstance<T>(T instance, string key)
        {
            var typeKey = GetTypeKey<T>(key);
            if (instance is null)
            {
                throw new ArgumentNullException(nameof(instance));
            }

            lock (_lock)
            {
                if (!_instances.ContainsKey(typeKey))
                {
                    _instances[typeKey] = instance;
                }
            }
        }

        /// <summary>
        /// Creates and registers a new instance of the specified type <typeparamref name="T"/> in the container.
        /// </summary>
        /// <typeparam name="T">The type of the instance to create and register.</typeparam>
        /// <param name="key">The unique key associated with the instance.</param>
        public void RegisterInstance<T>(string key) where T : new()
        {
            var typeKey = GetTypeKey<T>(key);
            var instance = new T();
            lock (_lock)
            {
                if (!_instances.ContainsKey(typeKey))
                {
                    _instances[typeKey] = instance;
                }
            }
        }

        /// <summary>
        /// Unregisters an instance of the specified type <typeparamref name="T"/> from the container.
        /// </summary>
        /// <typeparam name="T">The type of the instance to unregister.</typeparam>
        /// <param name="key">The unique key associated with the instance.</param>
        public void UnregisterInstance<T>(string key)
        {
            var typeKey = GetTypeKey<T>(key);
            lock (_lock)
            {
                _instances.Remove(typeKey);
            }
        }

        /// <summary>
        /// Clears all instances from the container.
        /// </summary>
        public void Clear()
        {
            lock (_lock)
            {
                _instances.Clear();
            }
        }

        /// <summary>
        /// Generates a unique key for the type and the provided key.
        /// </summary>
        /// <typeparam name="T">The type of the instance.</typeparam>
        /// <param name="key">The unique key associated with the instance.</param>
        /// <returns>A unique string key.</returns>
        private static string GetTypeKey<T>(string key)
        {
            return $"{typeof(T).FullName}_{key}";
        }
    }
}