using System;
using System.Collections.Generic;
using UIFramework.Api;

namespace UIFramework.Core
{
    /// <summary>
    /// The argument bag behind <see cref="IUICompositeArgs"/>: a dictionary of proxy-safe values keyed by string.
    /// Setting a key replaces whatever type was stored before; a typed getter returns its default when the key is
    /// missing or holds another type, so a builder never has to check types itself.
    /// </summary>
    internal sealed class CompositeArgs : IUICompositeArgs
    {
        private readonly Dictionary<string, object?> values = new(StringComparer.Ordinal);

        // ---------------------------------------------------------------------------------------------------------
        //  Setters
        // ---------------------------------------------------------------------------------------------------------

        public void SetString(string key, string value) => Set(key, value);
        public void SetNumber(string key, double value) => Set(key, value);
        public void SetBool(string key, bool value) => Set(key, value);
        public void SetAction(string key, Action value) => Set(key, value);
        public void SetGetter(string key, Func<string> value) => Set(key, value);
        public void SetSetter(string key, Action<string> value) => Set(key, value);
        public void SetNumberGetter(string key, Func<double> value) => Set(key, value);
        public void SetNumberSetter(string key, Action<double> value) => Set(key, value);
        public void SetObject(string key, object value) => Set(key, value);

        private void Set(string key, object? value)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("An argument key is required.", nameof(key));
            }

            values[key] = value;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Getters
        // ---------------------------------------------------------------------------------------------------------

        public string GetString(string key) => Get<string>(key) ?? string.Empty;
        public double GetNumber(string key) => Get<double?>(key) ?? 0;
        public bool GetBool(string key) => Get<bool?>(key) ?? false;
        public Action GetAction(string key) => Get<Action>(key)!;
        public Func<string> GetGetter(string key) => Get<Func<string>>(key)!;
        public Action<string> GetSetter(string key) => Get<Action<string>>(key)!;
        public Func<double> GetNumberGetter(string key) => Get<Func<double>>(key)!;
        public Action<double> GetNumberSetter(string key) => Get<Action<double>>(key)!;
        public object GetObject(string key) => Get<object>(key)!;

        private T? Get<T>(string key)
        {
            return key != null && values.TryGetValue(key, out object? value) && value is T typed ? typed : default;
        }

        public bool Has(string key) => key != null && values.ContainsKey(key);

        public string[] Keys
        {
            get
            {
                var keys = new string[values.Count];
                values.Keys.CopyTo(keys, 0);
                return keys;
            }
        }
    }
}
