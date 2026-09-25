using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using UIFramework.Core;
using UIFramework.Data.Expressions;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;
using UIFramework.Data.State;
using UIFramework.Rendering;

namespace UIFramework.Data.Building
{
    /// <summary>
    /// The rows of one collection (<c>List</c>, <c>DataGrid</c>, <c>Repeat</c>, dropdown <c>ChoicesSource</c>), per
    /// split-screen player. <see cref="Update"/> is called by the collection's refresher at the top of every tick; it
    /// re-reads the source only when the UI opens (or <c>_Refresh</c> / <c>_Rebuild</c>) and when what the source reads
    /// changed (the state value of a <c>State</c> source, the evaluated bounds of a <c>Range</c>, the interpolated text of
    /// an item query), and re-runs <c>Filter</c> / <c>Sort</c> / <c>Limit</c> when the raw rows changed, or when the state
    /// epoch moved and they read more than the row (a <c>Filter</c> of <c>row.price &gt; 100</c> never re-runs for a
    /// state change). It never resolves every frame.
    /// <para>
    /// Kinds: inline <c>Rows</c>, <c>Range</c>, <c>ItemQuery</c> (vanilla <c>ItemQueryResolver</c>), <c>State</c> (a
    /// JSON array held in state), <c>Themes</c>, <c>Asset</c> (a <c>Dictionary&lt;string, string&gt;</c> asset),
    /// <c>Named</c> (an entry of the UI's <c>Sources</c>) and <c>Hook</c> (C# row sources, v1.6: see <see cref="HookResolver"/>).
    /// </para>
    /// </summary>
    internal sealed class SourceBinding
    {
        /// <summary>Hard cap on the rows of one source (item queries such as ALL_ITEMS stay well below it).</summary>
        internal const int MaxRows = 10000;

        private const int MaxNamedDepth = 8;

        private readonly SourceDefinition def;
        private readonly string kind;
        private readonly DataScope scope;
        private readonly string? alias;
        private readonly DataRuntime runtime;
        private readonly DataPath path;
        private readonly SourceBinding? inner;
        private readonly Dictionary<int, Cache> caches = new();
        private readonly HashSet<string> reported = new(StringComparer.Ordinal);
        private int version;
        private DataValue lastValue;
        private bool? rowOnlyShaping;

        private SourceBinding(SourceDefinition def, string kind, DataScope scope, string? alias, DataRuntime runtime, DataPath path, SourceBinding? inner)
        {
            this.def = def;
            this.kind = kind;
            this.scope = scope;
            this.alias = alias;
            this.runtime = runtime;
            this.path = path;
            this.inner = inner;
        }

        /// <summary>
        /// Phase 4 hook: resolves a C# row source (<c>Hook</c> kind / <c>hook:Name</c>) for an owner. Arguments are the
        /// scope's owner, the hook name and the scope; null means "no such source" (reported once). Unset until the
        /// hook registry of v1.6 wires it.
        /// </summary>
        internal static Func<string, string, DataScope, IReadOnlyList<DataValue>?>? HookResolver { get; set; }

        /// <summary>
        /// A number that changes when a C# source's rows may have changed (owner, hook name); -1 when there is no such
        /// source yet. Part of the source's key, so the rows are re-read when it moves.
        /// </summary>
        internal static Func<string, string, long>? HookVersion { get; set; }

        /// <summary>The binding for <paramref name="def"/> in <paramref name="scope"/>; null (with a message) when it names nothing usable.</summary>
        internal static SourceBinding? Create(SourceDefinition? def, DataScope scope, string? alias, DataRuntime runtime, DataPath path, DataMessageLog log, int depth = 0)
        {
            if (def == null)
            {
                return null;
            }

            // a bare shorthand name: an entry of the UI's Sources wins over a state key
            bool bare = def.Shorthand != null;
            string? kind = bare
                ? runtime.Sources.ContainsKey(def.Name!.Trim()) ? SourceKinds.Named : SourceKinds.State
                : SourceKinds.Canonical(def.Kind);
            if (kind == null)
            {
                string? suggestion = def.Type != null ? DataValidator.Suggest(def.Type, SourceKinds.All) : null;
                log.Error(path, def.Type != null
                    ? $"unknown source Type '{def.Type}'{(suggestion != null ? $" (did you mean '{suggestion}'?)" : string.Empty)}."
                    : "the source says nowhere the rows come from (Rows, From / To, Query, State, Asset, Hook or Name).");
                return null;
            }

            SourceBinding? inner = null;
            if (kind == SourceKinds.Named)
            {
                string name = def.Name?.Trim() ?? string.Empty;
                if (!runtime.Sources.TryGetValue(name, out SourceDefinition? named))
                {
                    string? suggestion = DataValidator.Suggest(name, runtime.Sources.Keys);
                    log.Error(path, $"no entry '{name}' in Sources{(suggestion != null ? $" (did you mean '{suggestion}'?)" : string.Empty)}.");
                    return null;
                }

                if (depth >= MaxNamedDepth)
                {
                    log.Error(path, $"source '{name}' refers to itself through Sources.");
                    return null;
                }

                inner = Create(named, runtime.Scope ?? scope, alias, runtime, DataPath.Entry("Sources", name), log, depth + 1);
                if (inner == null)
                {
                    return null;
                }
            }

            if (kind == SourceKinds.State && !StateAddress.TryParse(def.State, scope, allowBare: true, out _, out string stateError))
            {
                log.Error(path.Field("State"), stateError);
                return null;
            }

            return new SourceBinding(def, kind, scope, alias, runtime, path, inner);
        }

        /// <summary>Incremented whenever the rows of any screen changed (collections rebuild their rows when it moves).</summary>
        internal int Version => version;

        /// <summary>The rows on the current screen (empty until the first <see cref="Update"/>).</summary>
        internal IReadOnlyList<DataValue> Rows => Current?.Rows ?? Array.Empty<DataValue>();

        /// <summary>The number of rows on the current screen.</summary>
        internal int Count => Rows.Count;

        /// <summary>Row <paramref name="index"/> on the current screen, or null when out of range.</summary>
        internal DataValue Row(int index)
        {
            IReadOnlyList<DataValue> rows = Rows;
            return index >= 0 && index < rows.Count ? rows[index] : DataValue.Null;
        }

        /// <summary>The scope of row <paramref name="index"/> (<c>row</c>, the alias, <c>index</c>), cached per rows version.</summary>
        internal DataScope ScopeFor(int index) => ScopeFor(index, scope);

        /// <summary>The scope of row <paramref name="index"/> layered over <paramref name="parent"/> (the parent is only used for uncached scopes).</summary>
        internal DataScope ScopeFor(int index, DataScope parent)
        {
            Cache? cache = Current;
            if (cache == null || index < 0 || index >= cache.Rows.Count)
            {
                return RowScope.For(parent, DataValue.Null, index, alias);
            }

            if (!ReferenceEquals(parent, scope))
            {
                return RowScope.For(parent, cache.Rows[index], index, alias);
            }

            cache.Scopes ??= new DataScope?[cache.Rows.Count];
            return cache.Scopes[index] ??= RowScope.For(scope, cache.Rows[index], index, alias);
        }

        private Cache? Current => caches.TryGetValue(DataStateStore.Screen, out Cache? cache) ? cache : null;

        /// <summary>Force the next <see cref="Update"/> to re-read the source (<c>_Rebuild</c>).</summary>
        internal void Invalidate()
        {
            caches.Clear();
            inner?.Invalidate();
        }

        /// <summary>
        /// Bring the current screen's rows up to date; true when they changed. <paramref name="opening"/> (the UI opened,
        /// <c>_Refresh</c>) always re-reads the source.
        /// </summary>
        internal bool Update(bool opening)
        {
            int screen = DataStateStore.Screen;
            long epoch = DataStateStore.Active?.Epoch(screen) ?? 0;
            caches.TryGetValue(screen, out Cache? cache);
            bool innerChanged = inner != null && inner.Update(opening);
            if (cache != null && !opening && !innerChanged && cache.Epoch == epoch)
            {
                return false;
            }

            // the raw rows: re-read on open, when the source's key changed, or when a named source changed
            string key = RawKey();
            IReadOnlyList<DataValue> raw;
            bool rawChanged;
            if (cache == null || opening || innerChanged || !string.Equals(key, cache.Key, StringComparison.Ordinal))
            {
                raw = Resolve(key);
                rawChanged = true;
            }
            else
            {
                raw = cache.Raw;
                rawChanged = false;
            }

            bool created = cache == null;
            if (cache == null)
            {
                caches[screen] = cache = new Cache();
            }

            if (rawChanged)
            {
                cache.RawScopes = null;
            }

            // filter / sort / limit (re-run for a state change only when they read more than the row)
            IReadOnlyList<DataValue> rows = rawChanged || (HasShaping && !ShapingReadsOnlyRows) ? Shape(raw, cache) : cache.Rows;
            bool changed = created || !SameRows(cache.Rows, rows);
            cache.Key = key;
            cache.Raw = raw;
            cache.Epoch = epoch;
            if (changed)
            {
                cache.Rows = rows;
                cache.Scopes = null;
                version++;
            }

            return changed;
        }

        private bool HasShaping => def.Filter != null || def.Sort != null || def.Limit != null;

        private static bool SameRows(IReadOnlyList<DataValue> a, IReadOnlyList<DataValue> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (!a[i].Equals(b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Raw rows
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A cheap text that changes when the raw rows would (checked every time the state epoch moves).</summary>
        private string RawKey()
        {
            switch (kind)
            {
                case SourceKinds.Range:
                    return $"{Number(def.From, 0)}|{Number(def.To, 0)}|{Number(def.Step, 1)}";
                case SourceKinds.ItemQuery:
                    return Text(def.Query) + "|" + Text(def.PerItemCondition);
                case SourceKinds.State:
                    return StateAddress.TryParse(def.State, scope, allowBare: true, out StateAddress address, out _)
                        ? DataStateStore.Active?.Read(address).AsString() ?? string.Empty
                        : string.Empty;
                case SourceKinds.Value:
                {
                    // the list itself is the key: rows are re-read when the expression gives another list
                    DataValue value = EvaluateValue();
                    lastValue = value;
                    return value.Kind == DataKind.List ? "list:" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value.AsObject()!) + ":" + value.AsList().Count : value.AsString();
                }
                case SourceKinds.Themes:
                    return string.Join("|", Theme.ThemeNames);
                case SourceKinds.Asset:
                    return Text(def.Asset);
                case SourceKinds.Hook:
                {
                    string hook = Text(def.Hook);
                    return hook + "|" + (HookVersion?.Invoke(scope.Owner, hook.Trim()) ?? -1);
                }
                case SourceKinds.Named:
                    return "named:" + inner!.Version;
                default:
                    return "rows";
            }
        }

        private IReadOnlyList<DataValue> Resolve(string key)
        {
            try
            {
                return kind switch
                {
                    SourceKinds.Rows => (def.Rows ?? new List<Newtonsoft.Json.Linq.JToken>()).Take(MaxRows).Select(t => RowScope.FromJson(t)).ToArray(),
                    SourceKinds.Range => ResolveRange(),
                    SourceKinds.ItemQuery => ResolveQuery(),
                    SourceKinds.State => ResolveState(key),
                    SourceKinds.Value => lastValue.AsList().Take(MaxRows).ToArray(),
                    SourceKinds.Themes => Theme.ThemeNames.Select(ThemeRow).ToArray(),
                    SourceKinds.Asset => ResolveAsset(),
                    SourceKinds.Hook => ResolveHook(),
                    SourceKinds.Named => inner!.Rows,
                    _ => Array.Empty<DataValue>()
                };
            }
            catch (Exception ex)
            {
                Report($"could not be read: {ex.Message}");
                return Array.Empty<DataValue>();
            }
        }

        /// <summary>The value of a Value source's expression (null, reported once, when it fails).</summary>
        private DataValue EvaluateValue()
        {
            if (def.Value == null)
            {
                return DataValue.Null;
            }

            DataValue value = ExpressionValueResolver.Instance.Evaluate(def.Value, scope, out string? error);
            if (error != null)
            {
                Report($"'{def.Value}': {error}");
            }

            return value;
        }

        private IReadOnlyList<DataValue> ResolveRange()
        {
            double from = Number(def.From, 0);
            double to = Number(def.To, 0);
            double step = Number(def.Step, from <= to ? 1 : -1);
            if (Math.Abs(step) < 1e-9 || (to - from) * step < 0)
            {
                if (Math.Abs(step) < 1e-9)
                {
                    Report("Step cannot be 0.");
                }

                return Array.Empty<DataValue>();
            }

            var rows = new List<DataValue>();
            for (double value = from; step > 0 ? value <= to + 1e-9 : value >= to - 1e-9; value += step)
            {
                if (rows.Count >= MaxRows)
                {
                    break;
                }

                rows.Add(DataValue.FromNumber(Math.Round(value, 9)));
            }

            return rows;
        }

        private IReadOnlyList<DataValue> ResolveQuery()
        {
            string query = Text(def.Query).Trim();
            if (query.Length == 0)
            {
                return Array.Empty<DataValue>();
            }

            if (!Context.IsWorldReady)
            {
                return Array.Empty<DataValue>(); // item queries need a location and a player
            }

            string? condition = def.PerItemCondition != null ? Text(def.PerItemCondition) : null;
            List<Item> items = RowScope.Query(query, string.IsNullOrWhiteSpace(condition) ? null : condition, MaxRows, out string? error);
            if (error != null)
            {
                Report($"item query: {error}");
            }

            return items.Select(RowScope.ForItem).ToArray();
        }

        private IReadOnlyList<DataValue> ResolveState(string text)
        {
            IReadOnlyList<DataValue> rows = RowScope.ParseList(text, out string? error);
            if (error != null)
            {
                Report($"the value of {def.State} is {error}");
            }

            return rows.Count > MaxRows ? rows.Take(MaxRows).ToArray() : rows;
        }

        private static DataValue ThemeRow(string name)
        {
            return RowScope.Object(new Dictionary<string, DataValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = DataValue.FromString(name),
                ["name"] = DataValue.FromString(name),
                ["value"] = DataValue.FromString(name),
                ["active"] = DataValue.FromBool(string.Equals(name, Theme.ActiveName, StringComparison.OrdinalIgnoreCase))
            });
        }

        private IReadOnlyList<DataValue> ResolveAsset()
        {
            string name = Text(def.Asset).Trim();
            if (name.Length == 0)
            {
                return Array.Empty<DataValue>();
            }

            Dictionary<string, string> asset = Game1.content.Load<Dictionary<string, string>>(name);
            var rows = new List<DataValue>();
            foreach ((string key, string value) in asset)
            {
                if (rows.Count >= MaxRows)
                {
                    break;
                }

                rows.Add(RowScope.Object(new Dictionary<string, DataValue>(StringComparer.OrdinalIgnoreCase)
                {
                    ["key"] = DataValue.FromString(key),
                    ["id"] = DataValue.FromString(key),
                    ["value"] = DataValue.FromString(value),
                    ["fields"] = DataValue.FromList((value ?? string.Empty).Split('/').Select(DataValue.FromString).ToArray())
                }));
            }

            return rows;
        }

        private IReadOnlyList<DataValue> ResolveHook()
        {
            string name = Text(def.Hook).Trim();
            IReadOnlyList<DataValue>? rows = HookResolver?.Invoke(scope.Owner, name, scope);
            if (rows == null)
            {
                Report($"no C# row source '{name}' is registered (DefineDataSource / ExposeRows); it is read again when one is.");
                return Array.Empty<DataValue>();
            }

            return rows;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Filter / sort / limit
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>True when <c>Filter</c>, <c>Sort</c>, <c>SortDescending</c> and <c>Limit</c> read nothing but the row (<c>row</c>, the alias, <c>index</c>) and literals.</summary>
        private bool ShapingReadsOnlyRows => rowOnlyShaping ??= RowScope.ReadsOnlyRow(def.Filter, alias) && RowScope.ReadsOnlyRow(def.Sort, alias)
            && RowScope.ReadsOnlyRow(def.SortDescending, alias) && RowScope.ReadsOnlyRow(def.Limit, alias);

        private IReadOnlyList<DataValue> Shape(IReadOnlyList<DataValue> raw, Cache cache)
        {
            if (!HasShaping)
            {
                return raw;
            }

            ExpressionValueResolver resolver = ExpressionValueResolver.Instance;
            var kept = new List<(DataValue Row, DataValue Key)>(raw.Count);
            cache.RawScopes ??= new DataScope?[raw.Count];
            for (int i = 0; i < raw.Count; i++)
            {
                DataScope rowScope = cache.RawScopes[i] ??= RowScope.For(scope, raw[i], i, alias);
                if (def.Filter != null)
                {
                    DataValue keep = resolver.Evaluate(def.Filter, rowScope, out string? error);
                    if (error != null)
                    {
                        Report($"Filter '{def.Filter}': {error}");
                    }

                    if (!keep.AsBool())
                    {
                        continue;
                    }
                }

                DataValue sortKey = DataValue.Null;
                if (def.Sort != null)
                {
                    sortKey = resolver.Evaluate(def.Sort, rowScope, out string? error);
                    if (error != null)
                    {
                        Report($"Sort '{def.Sort}': {error}");
                    }
                }

                kept.Add((raw[i], sortKey));
            }

            if (def.Sort != null)
            {
                bool descending = Flag(def.SortDescending);
                int sign = descending ? -1 : 1;
                // stable: equal keys keep the source order
                kept = kept.Select((entry, index) => (entry, index))
                    .OrderBy(p => p, Comparer<((DataValue Row, DataValue Key) entry, int index)>.Create((a, b) =>
                    {
                        int c = CompareKeys(a.entry.Key, b.entry.Key) * sign;
                        return c != 0 ? c : a.index.CompareTo(b.index);
                    }))
                    .Select(p => p.entry)
                    .ToList();
            }

            int limit = def.Limit != null ? (int)Math.Max(0, Number(def.Limit, MaxRows)) : MaxRows;
            return kept.Take(limit).Select(k => k.Row).ToArray();
        }

        /// <summary>Numbers (and numeric text) numerically, other text case-insensitively.</summary>
        private static int CompareKeys(DataValue a, DataValue b)
        {
            bool aNumber = a.Kind is DataKind.Number or DataKind.Bool || (a.Kind == DataKind.String && DataValue.TryParseNumber(a.AsString().Trim(), out _));
            bool bNumber = b.Kind is DataKind.Number or DataKind.Bool || (b.Kind == DataKind.String && DataValue.TryParseNumber(b.AsString().Trim(), out _));
            if (aNumber && bNumber)
            {
                return a.AsNumber().CompareTo(b.AsNumber());
            }

            return string.Compare(a.AsString(), b.AsString(), StringComparison.CurrentCultureIgnoreCase);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A field's text with <c>${...}</c> evaluated in the collection's scope.</summary>
        private string Text(string? raw) => raw == null ? string.Empty : ExpressionValueResolver.Instance.Interpolate(raw, scope);

        private double Number(string? raw, double fallback)
        {
            if (raw == null)
            {
                return fallback;
            }

            DataValue value = ExpressionValueResolver.Instance.Evaluate(raw, scope, out string? error);
            if (error != null)
            {
                Report($"'{raw}': {error}");
                return fallback;
            }

            return value.Kind == DataKind.Null ? fallback : value.AsNumber();
        }

        private bool Flag(string? raw)
        {
            if (raw == null)
            {
                return false;
            }

            if (ValueParsers.TryParseBool(raw, out bool literal))
            {
                return literal;
            }

            return ExpressionValueResolver.Instance.Evaluate(raw, scope, out _).AsBool();
        }

        private void Report(string message)
        {
            if (reported.Add(message))
            {
                UIServices.Log($"[{runtime.Owner}] {path}: {message}", LogLevel.Warn);
            }
        }

        private sealed class Cache
        {
            internal string Key = string.Empty;
            internal IReadOnlyList<DataValue> Raw = Array.Empty<DataValue>();
            internal IReadOnlyList<DataValue> Rows = Array.Empty<DataValue>();
            internal long Epoch = -1;
            internal DataScope?[]? Scopes;

            /// <summary>The row scopes of <see cref="Raw"/> that Filter and Sort evaluate in (kept while the raw rows are).</summary>
            internal DataScope?[]? RawScopes;
        }
    }
}
