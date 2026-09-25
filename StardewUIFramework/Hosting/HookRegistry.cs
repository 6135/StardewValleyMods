using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Data;
using UIFramework.Data.Bridge;
using UIFramework.Data.Expressions;
using UIFramework.Data.State;

namespace UIFramework.Hosting
{
    /// <summary>
    /// The C# bridge of data UIs (v1.6): commands, functions, row sources, draw hooks and exposed values (signals,
    /// computeds, models, rows) registered by C# mods and reached from data by name. Everything is keyed
    /// <c>owner/name</c> (case-insensitive) and runs under the owner's callback guard.
    /// Any pack may use <c>ModId/name</c>; the owner's own data may use the short name. Follows
    /// <see cref="CompositeRegistry"/>: process-wide, the owner is remembered and only it can replace or remove a hook.
    /// </summary>
    internal sealed class HookRegistry
    {
        private readonly Dictionary<string, Hook<Action<IUIDataCall>>> commands = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Hook<Func<string[], string>>> functions = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Hook<Action<SpriteBatch, Rectangle, IUIDataCall>>> draws = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DataSourceHandle> sources = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ExposedValue> values = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>A registered delegate and the mod that registered it.</summary>
        internal sealed record Hook<T>(ConsumerContext Owner, string Name, T Delegate);

        /// <summary>
        /// Raised when something data builds against changed (a model was exposed or replaced, a composite was
        /// defined): the data layer rebuilds its UIs on the next tick.
        /// </summary>
        internal event Action? StructureChanged;

        /// <summary>Incremented with every <see cref="StructureChanged"/> (part of the data entries' hash).</summary>
        internal int StructureVersion { get; private set; }

        /// <summary>Signal a structural change (models, composites).</summary>
        internal void NotifyStructureChanged()
        {
            StructureVersion++;
            StructureChanged?.Invoke();
        }

        /// <summary>Invalidate every cached data expression (a C# value changed).</summary>
        internal static void BumpData() => DataStateStore.Active?.BumpGlobal();

        // ---------------------------------------------------------------------------------------------------------
        //  Names
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The <c>owner/name</c> key of a reference: <c>ModId/name</c> as is, a short name in <paramref name="scopeOwner"/>.</summary>
        internal static string Qualify(string reference, string scopeOwner)
        {
            string text = (reference ?? string.Empty).Trim();
            if (text.StartsWith('@'))
            {
                text = text.Substring(1);
            }

            return text.Contains('/') ? text : scopeOwner + "/" + text;
        }

        /// <summary>Check a hook name (no slash, no spaces); throws for the C# API.</summary>
        internal static string RequireName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A hook name is required.", nameof(name));
            }

            string trimmed = name.Trim();
            if (trimmed.Contains('/') || trimmed.Any(char.IsWhiteSpace))
            {
                throw new ArgumentException($"Hook name '{name}' cannot contain '/' or spaces (data adds the owner: 'ModId/name').", nameof(name));
            }

            return trimmed;
        }

        private static string Key(ConsumerContext owner, string name) => owner.ModId + "/" + name;

        /// <summary>Find <paramref name="key"/> (the tables compare case-insensitively).</summary>
        private static bool TryFind<T>(Dictionary<string, T> table, string key, out T value) => table.TryGetValue(key, out value!);

        /// <summary>Add, replace or (with null) remove an entry; keys contain the owner, so a mod only ever touches its own hooks.</summary>
        private static void Set<T>(Dictionary<string, T> table, string key, T? value) where T : class
        {
            if (value == null)
            {
                table.Remove(key);
            }
            else
            {
                table[key] = value;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Registration (from the owner's API facade)
        // ---------------------------------------------------------------------------------------------------------

        internal void RegisterCommand(ConsumerContext owner, string name, Action<IUIDataCall>? run)
        {
            name = RequireName(name);
            Set(commands, Key(owner, name), run == null ? null : new Hook<Action<IUIDataCall>>(owner, name, run));
        }

        internal void RegisterFunction(ConsumerContext owner, string name, Func<string[], string>? function)
        {
            name = RequireName(name);
            if (!name.All(c => char.IsLetterOrDigit(c) || c is '_' or '-'))
            {
                UIServices.Log($"[{owner.ModId}] function '{name}' has characters expressions cannot write after '@' (use letters, digits, _ and -).", LogLevel.Warn);
            }

            Set(functions, Key(owner, name), function == null ? null : new Hook<Func<string[], string>>(owner, name, function));
            BumpData();
        }

        internal void RegisterDrawHook(ConsumerContext owner, string name, Action<SpriteBatch, Rectangle, IUIDataCall>? draw)
        {
            name = RequireName(name);
            Set(draws, Key(owner, name), draw == null ? null : new Hook<Action<SpriteBatch, Rectangle, IUIDataCall>>(owner, name, draw));
        }

        internal IUIDataSource DefineSource(ConsumerContext owner, string name)
        {
            name = RequireName(name);
            string key = Key(owner, name);
            if (!sources.TryGetValue(key, out DataSourceHandle? handle))
            {
                sources[key] = handle = new DataSourceHandle(owner, name);
                BumpData();
            }

            return handle;
        }

        internal void ExposeSignal(ConsumerContext owner, string name, IUISignal? signal) => Expose(owner, name, signal == null ? null : ExposedValue.ForSignal(signal));

        internal void ExposeComputed(ConsumerContext owner, string name, IUIComputed? computed) => Expose(owner, name, computed == null ? null : ExposedValue.ForComputed(computed));

        internal void ExposeModel(ConsumerContext owner, string name, object? model)
        {
            string key = Key(owner, RequireName(name));
            bool changed = !(values.TryGetValue(key, out ExposedValue? previous) && ReferenceEquals(previous.Model, model));
            Expose(owner, name, model == null ? null : ExposedValue.ForModel(model));
            if (changed)
            {
                NotifyStructureChanged(); // forms built over the model (and model binds) are rebuilt
            }
        }

        /// <summary>A model resolved through <paramref name="model"/> at every read (a per-screen model: <c>() =&gt; perScreen.Value</c>).</summary>
        internal void ExposeModelSource(ConsumerContext owner, string name, Func<object>? model)
        {
            Expose(owner, name, model == null ? null : ExposedValue.ForModelSource(model));
            NotifyStructureChanged(); // forms built over the model (and model binds) are rebuilt
        }

        internal void ExposeRows(ConsumerContext owner, string name, Func<object[]>? rows) => Expose(owner, name, rows == null ? null : ExposedValue.ForRows(rows));

        private void Expose(ConsumerContext owner, string name, ExposedValue? value)
        {
            string key = Key(owner, RequireName(name));
            if (values.TryGetValue(key, out ExposedValue? previous))
            {
                previous.Detach();
            }

            if (value != null)
            {
                value.Owner = owner;
                value.Name = name.Trim();
                value.Attach();
            }

            Set(values, key, value);
            BumpData();
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Commands
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>True when <paramref name="key"/> (<c>owner/name</c>) is a registered command.</summary>
        internal bool HasCommand(string key) => TryFind(commands, key, out _);

        /// <summary>
        /// Run the command <paramref name="key"/> (<c>owner/name</c>) under its owner's guard, then refresh data (a
        /// command usually changes what data shows). False (with an error) when there is no such command.
        /// </summary>
        internal bool RunCommand(string key, string[] args, DataScope? scope, string callerOwner, out string error)
        {
            if (!TryFind(commands, key, out Hook<Action<IUIDataCall>> hook))
            {
                string? suggestion = Data.Loading.DataValidator.Suggest(key, commands.Keys);
                error = $"no command '{key}' is registered{(suggestion != null ? $" (did you mean '{suggestion}'?)" : string.Empty)}.";
                return false;
            }

            var call = new DataCall(hook.Name, args, scope, callerOwner);
            hook.Owner.Invoke("hook:" + hook.Name, "command", () => hook.Delegate(call));
            BumpData();
            error = string.Empty;
            return true;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Functions
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Call the function <paramref name="key"/> with text arguments; false when there is no such function. Results are typed like state text.</summary>
        internal bool TryCallFunction(string key, ReadOnlySpan<DataValue> args, out DataValue result)
        {
            if (!TryFind(functions, key, out Hook<Func<string[], string>> hook))
            {
                result = DataValue.Null;
                return false;
            }

            string[] text = new string[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                text[i] = args[i].AsString();
            }

            Func<string[], string> function = hook.Delegate;
            string? value = hook.Owner.Invoke<string?>("hook:" + hook.Name, "function", () => function(text), null);
            result = StateAddress.Infer(value);
            return true;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Draw hooks
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>True when <paramref name="key"/> is a registered draw hook.</summary>
        internal bool HasDrawHook(string key) => TryFind(draws, key, out _);

        /// <summary>
        /// A draw callback for an element: looks the hook up at draw time (so it may be registered after the UI was
        /// built) and runs it under its owner's guard with one <see cref="DataCall"/> per element.
        /// </summary>
        internal Action<SpriteBatch, Rectangle> DrawCallback(string key, DataScope scope)
        {
            DataCall? call = null;
            return (b, bounds) =>
            {
                if (!TryFind(draws, key, out Hook<Action<SpriteBatch, Rectangle, IUIDataCall>> hook))
                {
                    return;
                }

                call ??= new DataCall(hook.Name, Array.Empty<string>(), scope, scope.Owner);
                hook.Owner.Invoke(scope.GuardId, "draw:" + hook.Name, () => hook.Delegate(b, bounds, call));
            };
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Sources, exposed values and models
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The rows of the C# source <paramref name="name"/> for a data UI of <paramref name="scopeOwner"/>: a
        /// <see cref="DefineSource"/> source, else exposed rows (or an exposed list / model that is a list); null when there is none.
        /// </summary>
        internal IReadOnlyList<DataValue>? ResolveRows(string scopeOwner, string name, DataScope scope)
        {
            string key = Qualify(name, scopeOwner);
            if (TryFind(sources, key, out DataSourceHandle handle))
            {
                return handle.Rows();
            }

            if (TryFind(values, key, out ExposedValue value))
            {
                return value.ReadRows();
            }

            return null;
        }

        /// <summary>
        /// A number that changes when the rows of <paramref name="name"/> may have changed: a source's version, or the
        /// state epoch for exposed rows (they are re-read whenever data state moves); -1 when there is no such source.
        /// </summary>
        internal long RowsVersion(string scopeOwner, string name)
        {
            string key = Qualify(name, scopeOwner);
            if (TryFind(sources, key, out DataSourceHandle handle))
            {
                return handle.Version;
            }

            if (TryFind(values, key, out _))
            {
                return DataStateStore.Active?.Epoch(DataStateStore.Screen) ?? 0;
            }

            return -1;
        }

        /// <summary>Read the exposed value <paramref name="key"/> (<c>@owner/name</c>); false when there is none.</summary>
        internal bool TryReadValue(string key, out DataValue value, out bool isVolatile)
        {
            if (TryFind(values, key, out ExposedValue exposed))
            {
                value = exposed.Read(out isVolatile);
                return true;
            }

            if (TryFind(sources, key, out DataSourceHandle handle))
            {
                value = DataValue.FromList(handle.Rows());
                isVolatile = false;
                return true;
            }

            value = DataValue.Null;
            isVolatile = false;
            return false;
        }

        /// <summary>The model exposed as <paramref name="key"/>, or null.</summary>
        internal object? ModelOf(string key) => TryFind(values, key, out ExposedValue exposed) ? exposed.Model : null;

        /// <summary>The exposed signal <paramref name="key"/> (for data writes), or null.</summary>
        internal IUISignal? SignalOf(string key) => TryFind(values, key, out ExposedValue exposed) ? exposed.Signal : null;

        // ---------------------------------------------------------------------------------------------------------
        //  Diagnostics
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>One line per hook for <c>ui_data</c>.</summary>
        internal IEnumerable<string> Describe()
        {
            foreach (string key in commands.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                yield return "command  " + key;
            }

            foreach (string key in functions.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                yield return "function @" + key;
            }

            foreach (string key in sources.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                yield return "source   hook:" + key;
            }

            foreach (string key in draws.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                yield return "draw     " + key;
            }

            foreach ((string key, ExposedValue value) in values.OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase))
            {
                yield return $"{value.KindName,-8} @{key}";
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Exposed values
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A signal, computed, model (or model source) or row list exposed to data under a name.</summary>
        private sealed class ExposedValue
        {
            private readonly IUIComputed? computed;
            private readonly object? model;
            private readonly Func<object>? modelSource;
            private readonly Func<object[]>? rows;
            private readonly Action changed = BumpData;

            private ExposedValue(IUISignal? signal, IUIComputed? computed, object? model, Func<object>? modelSource, Func<object[]>? rows)
            {
                Signal = signal;
                this.computed = computed;
                this.model = model;
                this.modelSource = modelSource;
                this.rows = rows;
            }

            internal static ExposedValue ForSignal(IUISignal signal) => new(signal, null, null, null, null);
            internal static ExposedValue ForComputed(IUIComputed computed) => new(null, computed, null, null, null);
            internal static ExposedValue ForModel(object model) => new(null, null, model, null, null);
            internal static ExposedValue ForModelSource(Func<object> source) => new(null, null, null, source, null);
            internal static ExposedValue ForRows(Func<object[]> rows) => new(null, null, null, null, rows);

            internal ConsumerContext Owner { get; set; } = ConsumerContext.None;
            internal string Name { get; set; } = string.Empty;
            internal IUISignal? Signal { get; }

            /// <summary>The exposed model: the object itself, or what the model source returns now (read under the owner's guard).</summary>
            internal object? Model => modelSource == null ? model : Owner.Invoke<object?>("hook:" + Name, "model", modelSource, null);

            internal string KindName => Signal != null ? "signal" : computed != null ? "computed" : model != null || modelSource != null ? "model" : "rows";

            /// <summary>Subscribe to change notifications (signals, computeds; models are watched when read).</summary>
            internal void Attach()
            {
                Signal?.Subscribe(changed);
                computed?.Subscribe(changed);
            }

            internal void Detach()
            {
                Signal?.Unsubscribe(changed);
                computed?.Unsubscribe(changed);
            }

            internal DataValue Read(out bool isVolatile)
            {
                isVolatile = false;
                if (Signal != null)
                {
                    IUISignal signal = Signal;
                    return StateAddress.Infer(Owner.Invoke<string?>("hook:" + Name, "signal", () => signal.Value, null));
                }

                if (computed != null)
                {
                    IUIComputed value = computed;
                    return StateAddress.Infer(Owner.Invoke<string?>("hook:" + Name, "computed", () => value.Value, null));
                }

                if (model != null || modelSource != null)
                {
                    object? current = Model;
                    if (current == null)
                    {
                        isVolatile = true; // a source may return an object later
                        return DataValue.Null;
                    }

                    isVolatile = !ModelAccessor.Watch(current);
                    return ModelAccessor.ToValue(current);
                }

                return DataValue.FromList(ReadRows());
            }

            internal IReadOnlyList<DataValue> ReadRows()
            {
                if (rows != null)
                {
                    Func<object[]> read = rows;
                    object[]? list = Owner.Invoke<object[]?>("hook:" + Name, "rows", () => read(), null);
                    if (list == null)
                    {
                        return Array.Empty<DataValue>();
                    }

                    var result = new DataValue[Math.Min(list.Length, Data.Building.SourceBinding.MaxRows)];
                    for (int i = 0; i < result.Length; i++)
                    {
                        result[i] = ModelAccessor.ToValue(list[i]);
                    }

                    return result;
                }

                DataValue value = Read(out _);
                return value.Kind == DataKind.List ? value.AsList() : Array.Empty<DataValue>();
            }
        }
    }
}
