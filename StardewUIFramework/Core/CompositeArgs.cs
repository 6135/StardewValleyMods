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

        /// <summary>Store a data argument (v1.6): typed getters convert it on each read (see <see cref="Data.Bridge.DataArgument"/>).</summary>
        internal void SetData(string key, Data.Bridge.DataArgument value) => Set(key, value);

        /// <summary>
        /// The data instance this bag belongs to (v1.7): the builder's context, scope and children of a data composite
        /// element, so a data composite's body can route those children into its Outlets on every (re)build. Null for
        /// bags created in C#.
        /// </summary>
        internal object? DataPayload { get; set; }

        /// <summary>The stored value of <paramref name="key"/> as is (exact key first, then case-insensitively), without conversion.</summary>
        internal bool TryGetRaw(string? key, out object? value)
        {
            if (TryGetValue(key, out value))
            {
                return true;
            }

            if (key != null)
            {
                foreach ((string candidate, object? candidateValue) in values)
                {
                    if (string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase))
                    {
                        value = candidateValue;
                        return true;
                    }
                }
            }

            value = null;
            return false;
        }

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
            if (!TryGetValue(key, out object? value))
            {
                return default;
            }

            // a data literal or reference converts to whatever the builder asks for (text, number, bool, getter, setter, action, object)
            if (value is Data.Bridge.DataArgument argument)
            {
                return argument.TryConvert(typeof(T), out object? converted) && converted is T convertedTyped ? convertedTyped : default;
            }

            return value is T typed ? typed : default;
        }

        /// <summary>The value of <paramref name="key"/>: exact key first, then (for data-written keys such as <c>Value</c> vs <c>value</c>) case-insensitively.</summary>
        private bool TryGetValue(string? key, out object? value)
        {
            value = null;
            if (key == null)
            {
                return false;
            }

            if (values.TryGetValue(key, out value))
            {
                return true;
            }

            foreach ((string candidate, object? candidateValue) in values)
            {
                if (candidateValue is Data.Bridge.DataArgument && string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase))
                {
                    value = candidateValue;
                    return true;
                }
            }

            return false;
        }

        public bool Has(string key) => TryGetValue(key, out _);

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
