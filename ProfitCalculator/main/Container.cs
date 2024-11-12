using System;

using System.Collections.Generic;

#nullable enable

namespace ProfitCalculator.main
{
    /// <summary>
    /// The <see cref="Container"/> class is a thread-safe singleton that manages instances of various types.
    /// It provides methods to register, retrieve, and manage these instances.
    /// </summary>
    public class Container
    {
        private static readonly Lazy<Container> _instance = new(() => new Container());

        private readonly Dictionary<Type, object> _instances = new();
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
        /// <returns>The instance of type <typeparamref name="T"/> if found; otherwise, the default value for type <typeparamref name="T"/>.</returns>
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

        /// <summary>
        /// Registers an instance of the specified type <typeparamref name="T"/> in the container.
        /// </summary>
        /// <typeparam name="T">The type of the instance to register.</typeparam>
        /// <param name="instance">The instance to register.</param>
        /// <exception cref="ArgumentNullException">Thrown when the instance is null.</exception>
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

        /// <summary>
        /// Creates and registers a new instance of the specified type <typeparamref name="T"/> in the container.
        /// </summary>
        /// <typeparam name="T">The type of the instance to create and register.</typeparam>
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

        /// <summary>
        /// Unregisters an instance of the specified type <typeparamref name="T"/> from the container.
        /// </summary>
        /// <typeparam name="T">The type of the instance to unregister.</typeparam>
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
    }
}