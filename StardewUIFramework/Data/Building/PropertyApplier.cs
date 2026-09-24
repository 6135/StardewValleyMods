using System;
using System.Collections.Generic;
using UIFramework.Data.Loading;

namespace UIFramework.Data.Building
{
    /// <summary>
    /// The single path every data value takes into an element or menu. It asks the <see cref="IValueResolver"/> for a
    /// <see cref="ValueSource{T}"/>, applies it once, and when the source is dynamic (v1.4 expressions) registers it
    /// with the current refresh group (the menu's, or an If / Switch page subtree's) so it is re-applied every tick. The builder never looks at raw values itself, so
    /// making a property dynamic needs no builder change.
    /// </summary>
    internal sealed class PropertyApplier
    {
        private readonly IValueResolver resolver;
        private readonly RefresherGroup group;
        private readonly DataMessageLog log;

        internal PropertyApplier(IValueResolver resolver, RefresherGroup group, DataMessageLog log)
        {
            this.resolver = resolver;
            this.group = group;
            this.log = log;
        }

        /// <summary>The messages of the build.</summary>
        internal DataMessageLog Log => log;

        /// <summary>The value resolver.</summary>
        internal IValueResolver Resolver => resolver;

        /// <summary>The refresh list dynamic values are registered in.</summary>
        internal RefresherGroup Group => group;

        /// <summary>An applier that registers into <paramref name="other"/> (the subtree of an If or a Switch page).</summary>
        internal PropertyApplier WithGroup(RefresherGroup other) => new(resolver, other, log);

        /// <summary>The source of a field (null when not set or invalid), for callers that read it themselves.</summary>
        internal ValueSource<T>? Source<T>(string? raw, ValueKind<T> kind, DataPath path)
        {
            return raw == null ? null : resolver.Resolve(raw, kind, path, log);
        }

        /// <summary>Apply a field when it is set; an invalid value is reported and skipped.</summary>
        internal void Apply<T>(string? raw, ValueKind<T> kind, DataScope scope, DataPath path, Action<T> apply)
        {
            if (raw == null)
            {
                return;
            }

            ValueSource<T>? source = resolver.Resolve(raw, kind, path, log);
            if (source != null)
            {
                Bind(source, scope, apply);
            }
        }

        /// <summary>Apply a field, or <paramref name="fallback"/> when it is not set or invalid (menu options, which a rebuild must reset).</summary>
        internal void ApplyOr<T>(string? raw, ValueKind<T> kind, T fallback, DataScope scope, DataPath path, Action<T> apply)
        {
            ValueSource<T>? source = raw == null ? null : resolver.Resolve(raw, kind, path, log);
            if (source == null)
            {
                apply(fallback);
                return;
            }

            Bind(source, scope, apply);
        }

        /// <summary>The value of a field needed up front (constructor arguments); dynamic sources give their current value.</summary>
        internal T Initial<T>(string? raw, ValueKind<T> kind, T fallback, DataScope scope, DataPath path)
        {
            if (raw == null)
            {
                return fallback;
            }

            ValueSource<T>? source = resolver.Resolve(raw, kind, path, log);
            return source == null ? fallback : source.Get(scope);
        }

        /// <summary>A text getter for a field (null when the field is not set). Dynamic text is read on every call.</summary>
        internal Func<string>? Text(string? raw, DataScope scope, DataPath path)
        {
            if (raw == null)
            {
                return null;
            }

            ValueSource<string>? source = resolver.Resolve(raw, ValueParsers.Text, path, log);
            if (source == null)
            {
                return null;
            }

            if (!source.IsDynamic)
            {
                string text = source.Get(scope);
                return () => text;
            }

            return () => source.Get(scope);
        }

        /// <summary>Register an arbitrary refresher with the menu (element conditions, v1.4 output bindings...).</summary>
        internal void AddRefresher(IDataRefresher refresher) => group.Add(refresher);

        private void Bind<T>(ValueSource<T> source, DataScope scope, Action<T> apply)
        {
            apply(source.Get(scope));
            if (source.IsDynamic)
            {
                group.Add(new ValueRefresher<T>(source, scope, apply));
            }
        }

        /// <summary>Re-applies a dynamic value when it changed.</summary>
        private sealed class ValueRefresher<T> : IDataRefresher
        {
            private readonly ValueSource<T> source;
            private readonly DataScope scope;
            private readonly Action<T> apply;
            private T last;

            internal ValueRefresher(ValueSource<T> source, DataScope scope, Action<T> apply)
            {
                this.source = source;
                this.scope = scope;
                this.apply = apply;
                last = source.Get(scope);
            }

            public void Refresh(bool opening)
            {
                T value = source.Get(scope);
                if (!opening && EqualityComparer<T>.Default.Equals(value, last))
                {
                    return;
                }

                last = value;
                apply(value);
            }
        }
    }
}
