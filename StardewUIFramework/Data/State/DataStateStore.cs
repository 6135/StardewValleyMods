using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using UIFramework.Core;
using UIFramework.Data.Expressions;

namespace UIFramework.Data.State
{
    /// <summary>A state value changed (screen, address, old and new value).</summary>
    internal delegate void StateChangedHandler(int screen, StateAddress address, DataValue oldValue, DataValue newValue);

    /// <summary>
    /// The named state of data UIs. <c>menu.*</c> and <c>session.*</c> values are existing <see cref="Signal"/> cells
    /// keyed by <b>(screen, scope, container, name)</b>, never by the <see cref="UIMenu"/> object, so every split-screen
    /// player has its own values and state survives menu replacement and hot reload. <c>player.*</c> lives in the
    /// current player's <c>modData</c>, <c>stat.*</c> in their stats and <c>config.*</c> in the <see cref="ConfigStore"/>.
    /// Value names are case-insensitive in every scope (<c>player.Gold</c> and <c>player.gold</c> are one value);
    /// owner and menu keys are matched as written.
    /// <para>
    /// Defaults (menu <c>State</c>, input <c>Value</c>s) are registered once per build and only ever fill values that do
    /// not exist yet, lazily on the screen that reads them. Every write bumps the screen's epoch (the key of the
    /// expression caches) and raises <see cref="Changed"/> (menu <c>Watch</c>es).
    /// </para>
    /// </summary>
    internal sealed class DataStateStore
    {
        private readonly Func<string, ConsumerContext> contexts;
        private readonly Dictionary<CellKey, Cell> cells = new();
        private readonly Dictionary<DefaultKey, Func<DataValue>> defaults = new();
        private readonly Dictionary<DefaultKey, string> modDataKeys = new();
        private readonly HashSet<int> screensWithCells = new();
        private long[] epochs = new long[4];
        private long globalEpoch;

        internal DataStateStore(Func<string, ConsumerContext> contexts, ConfigStore config)
        {
            this.contexts = contexts;
            Config = config;
        }

        /// <summary>The store the data layer uses (null until <see cref="DataService"/> created it).</summary>
        internal static DataStateStore? Active { get; set; }

        /// <summary>The <c>config.*</c> backing store.</summary>
        internal ConfigStore Config { get; }

        /// <summary>Raised after a value changed through the store (or a C# write to one of its signals).</summary>
        internal event StateChangedHandler? Changed;

        /// <summary>Whether an owner lets other owners' data write its <c>config.*</c> and <c>player.*</c> values (its <c>Owners</c> entry's <c>SharedState</c>).</summary>
        internal Func<string, bool>? SharesState { get; set; }

        /// <summary>
        /// Whether data of <paramref name="writer"/> may write <paramref name="address"/>: another owner's persisted
        /// values (<c>config.*</c>, <c>player.*</c>) only when that owner shares them. Null writers (C#, the console,
        /// trigger actions without a UI) are not restricted.
        /// </summary>
        internal bool CanWrite(StateAddress address, string? writer, out string error)
        {
            error = string.Empty;
            if (writer == null || address.Scope is not (StateScope.Config or StateScope.Player)
                || string.Equals(address.Container, writer, StringComparison.OrdinalIgnoreCase) || SharesState?.Invoke(address.Container) == true)
            {
                return true;
            }

            error = $"'{writer}' may not write {address}: it belongs to '{address.Container}', whose Owners entry does not set \"SharedState\": \"true\".";
            return false;
        }

        /// <summary>The current screen (split-screen player index).</summary>
        internal static int Screen => Context.ScreenId;

        /// <summary>The cache epoch of <paramref name="screen"/>: bumped by every state change on it and by global changes (config, reloads).</summary>
        internal long Epoch(int screen)
        {
            long own = screen >= 0 && screen < epochs.Length ? epochs[screen] : 0;
            return own + globalEpoch + Config.Version;
        }

        /// <summary>Invalidate every cached expression result on every screen (theme or asset changes).</summary>
        internal void BumpGlobal() => globalEpoch++;

        // ---------------------------------------------------------------------------------------------------------
        //  Defaults
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Register the default of a value (replaces an earlier default); existing values are never touched.</summary>
        internal void SetDefault(StateAddress address, Func<DataValue> value) => defaults[new DefaultKey(address)] = value;

        /// <summary>True when a default is registered for <paramref name="address"/>.</summary>
        internal bool HasDefault(StateAddress address) => defaults.ContainsKey(new DefaultKey(address));

        /// <summary>The registered default of <paramref name="address"/>, or null.</summary>
        internal DataValue? DefaultOf(StateAddress address)
        {
            return defaults.TryGetValue(new DefaultKey(address), out Func<DataValue>? factory) ? SafeDefault(address, factory) : null;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Read / write
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Read a value on the current screen; false when it does not exist and has no default.</summary>
        internal bool TryRead(StateAddress address, out DataValue value, out bool isVolatile)
        {
            isVolatile = false;
            switch (address.Scope)
            {
                case StateScope.Menu:
                case StateScope.Session:
                {
                    Signal? signal = CellOf(address, create: true);
                    value = signal == null ? DataValue.Null : DataValue.FromReactive(signal.Current);
                    return signal != null;
                }

                case StateScope.Player:
                {
                    isVolatile = true; // vanilla actions and other mods write modData without telling us
                    if (Context.IsWorldReady && Game1.player.modData.TryGetValue(ModDataKey(address), out string? text))
                    {
                        value = StateAddress.FromStoredText(text);
                        return true;
                    }

                    return TryDefault(address, out value);
                }

                case StateScope.Stat:
                    isVolatile = true;
                    if (Context.IsWorldReady)
                    {
                        value = DataValue.FromNumber(Game1.player.stats.Get(address.Name));
                        return true;
                    }

                    value = DataValue.Null;
                    return false;

                default:
                {
                    string? text = Config.Get(address.Container, address.Name);
                    if (text != null)
                    {
                        value = StateAddress.FromStoredText(text);
                        return true;
                    }

                    return TryDefault(address, out value);
                }
            }
        }

        /// <summary>The value on the current screen, or null.</summary>
        internal DataValue Read(StateAddress address) => TryRead(address, out DataValue value, out _) ? value : DataValue.Null;

        /// <summary>Write a value on the current screen; false (with an error) when the scope cannot hold it now.</summary>
        internal bool Write(StateAddress address, DataValue value, out string error)
        {
            error = string.Empty;
            DataValue old = Read(address);
            switch (address.Scope)
            {
                case StateScope.Menu:
                case StateScope.Session:
                {
                    SetSignal(CreateCell(address), value); // notifies through the cell's subscription
                    return true;
                }

                case StateScope.Player:
                    if (!Context.IsWorldReady)
                    {
                        error = "player.* values need a loaded save.";
                        return false;
                    }

                    if (value.IsNull)
                    {
                        Game1.player.modData.Remove(ModDataKey(address));
                    }
                    else
                    {
                        Game1.player.modData[ModDataKey(address)] = value.AsString();
                    }

                    break;

                case StateScope.Stat:
                    if (!Context.IsWorldReady)
                    {
                        error = "stat.* values need a loaded save.";
                        return false;
                    }

                    Game1.player.stats.Set(address.Name, (uint)Math.Clamp(Math.Round(value.AsNumber()), 0, uint.MaxValue));
                    break;

                default:
                    Config.Set(address.Container, address.Name, value.IsNull ? null : value.AsString());
                    break;
            }

            DataValue now = Read(address);
            if (!now.Equals(old))
            {
                Notify(Screen, address, old, now);
            }

            return true;
        }

        /// <summary>Reset a value to its default (menu / session), or remove it (player, config); stats are set to 0.</summary>
        internal bool Reset(StateAddress address, out string error)
        {
            error = string.Empty;
            switch (address.Scope)
            {
                case StateScope.Menu:
                case StateScope.Session:
                {
                    DataValue? initial = DefaultOf(address);
                    if (initial.HasValue)
                    {
                        return Write(address, initial.Value, out error);
                    }

                    CellKey key = new(Screen, address);
                    if (cells.TryGetValue(key, out Cell? cell))
                    {
                        DataValue old = DataValue.FromReactive(cell.Signal.Current);
                        cells.Remove(key);
                        Notify(Screen, address, old, DataValue.Null);
                    }

                    return true;
                }

                case StateScope.Stat:
                    return Write(address, DataValue.Zero, out error);

                default:
                    return Write(address, DataValue.Null, out error);
            }
        }

        /// <summary>
        /// Forget every value of a menu container on the current screen (<c>StateLifetime: Open</c>, <c>ResetState menu</c>):
        /// the next read recreates it from its default. Nothing is notified (the menu is (re)opening).
        /// </summary>
        internal void ResetContainer(StateScope scope, string container)
        {
            int screen = Screen;
            foreach (CellKey key in cells.Keys.Where(k => k.Screen == screen && k.Scope == scope && k.Container == container).ToArray())
            {
                cells.Remove(key);
            }

            BumpScreen(screen);
        }

        /// <summary>
        /// Drop the menu and session values of split-screen players who left (call once per tick), so a player who
        /// later joins on the same screen id starts empty.
        /// </summary>
        internal void RemoveDeadScreens()
        {
            List<int>? dead = null;
            foreach (int screen in screensWithCells)
            {
                if (!Context.HasScreenId(screen))
                {
                    (dead ??= new List<int>()).Add(screen);
                }
            }

            if (dead == null)
            {
                return;
            }

            foreach (int screen in dead)
            {
                foreach (CellKey key in cells.Keys.Where(k => k.Screen == screen).ToArray())
                {
                    cells.Remove(key);
                }

                screensWithCells.Remove(screen);
                BumpScreen(screen);
            }
        }

        /// <summary>Drop every menu and session value (return to title).</summary>
        internal void ClearSession()
        {
            cells.Clear();
            screensWithCells.Clear();
            globalEpoch++;
        }

        /// <summary>
        /// The signal cell of a menu / session value on the current screen (null for other scopes, or when
        /// <paramref name="create"/> is false and it does not exist). A created cell starts at its default.
        /// </summary>
        internal Signal? CellOf(StateAddress address, bool create)
        {
            if (address.Scope != StateScope.Menu && address.Scope != StateScope.Session)
            {
                return null;
            }

            CellKey key = new(Screen, address);
            if (cells.TryGetValue(key, out Cell? cell))
            {
                return cell.Signal;
            }

            if (!create)
            {
                return null;
            }

            DataValue initial = DefaultOf(address) ?? DataValue.Null;
            if (initial.IsNull && !defaults.ContainsKey(new DefaultKey(address)))
            {
                return null; // reading an unknown value does not create it; writing does (below)
            }

            return Create(key, address, initial);
        }

        /// <summary>The signal of a menu / session value on the current screen, created (at its default, else empty) when missing: what writes and C# bindings use.</summary>
        internal Signal CreateCell(StateAddress address)
        {
            CellKey key = new(Screen, address);
            return cells.TryGetValue(key, out Cell? cell) ? cell.Signal : Create(key, address, DefaultOf(address) ?? DataValue.Null);
        }

        private Signal Create(CellKey key, StateAddress address, DataValue initial)
        {
            var signal = new Signal(contexts(address.Owner), initial.ToReactive());
            var cell = new Cell(key.Screen, address, signal, initial);
            cells[key] = cell;
            screensWithCells.Add(key.Screen);
            signal.SubscribeInternal(() => OnSignalChanged(cell));
            return signal;
        }

        /// <summary>Every value on the current screen (menu and session cells, plus the loaded config), for <c>ui_state</c>.</summary>
        internal IEnumerable<(StateAddress Address, DataValue Value)> Snapshot()
        {
            int screen = Screen;
            foreach (Cell cell in cells.Values.Where(c => c.Screen == screen).OrderBy(c => c.Address.ToString(), StringComparer.OrdinalIgnoreCase))
            {
                yield return (cell.Address, DataValue.FromReactive(cell.Signal.Current));
            }

            foreach (string owner in Config.LoadedOwners)
            {
                foreach ((string name, string value) in Config.All(owner))
                {
                    yield return (new StateAddress(StateScope.Config, owner, name), StateAddress.FromStoredText(value));
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Notification
        // ---------------------------------------------------------------------------------------------------------

        private void OnSignalChanged(Cell cell)
        {
            DataValue now = DataValue.FromReactive(cell.Signal.Current);
            DataValue old = cell.Last;
            if (now.Equals(old))
            {
                return;
            }

            cell.Last = now;
            Notify(cell.Screen, cell.Address, old, now);
        }

        private void Notify(int screen, StateAddress address, DataValue old, DataValue now)
        {
            BumpScreen(screen);
            if (Changed == null)
            {
                return;
            }

            try
            {
                Changed(screen, address, old, now);
            }
            catch (Exception ex)
            {
                UIServices.Log($"A state change handler for {address} failed: {ex}", LogLevel.Error);
            }
        }

        private void BumpScreen(int screen)
        {
            if (screen < 0)
            {
                globalEpoch++;
                return;
            }

            if (screen >= epochs.Length)
            {
                Array.Resize(ref epochs, Math.Max(screen + 1, epochs.Length * 2));
            }

            epochs[screen]++;
        }

        private static void SetSignal(Signal signal, DataValue value)
        {
            switch (value.Kind)
            {
                case DataKind.Bool:
                    signal.Flag = value.AsBool();
                    break;
                case DataKind.Number:
                    signal.Number = value.AsNumber();
                    break;
                default:
                    signal.Value = value.AsString();
                    break;
            }
        }

        private bool TryDefault(StateAddress address, out DataValue value)
        {
            DataValue? initial = DefaultOf(address);
            value = initial ?? DataValue.Null;
            return initial.HasValue;
        }

        private static DataValue SafeDefault(StateAddress address, Func<DataValue> factory)
        {
            try
            {
                return factory();
            }
            catch (Exception ex)
            {
                UIServices.Log($"The default of {address} failed: {ex.Message}", LogLevel.Warn);
                return DataValue.Null;
            }
        }

        /// <summary>The <c>modData</c> key of a <c>player.*</c> value (<c>owner/name</c>, the name lower-case), built once per value.</summary>
        private string ModDataKey(StateAddress address)
        {
            var key = new DefaultKey(address);
            if (!modDataKeys.TryGetValue(key, out string? text))
            {
                modDataKeys[key] = text = address.Container + "/" + address.Name.ToLowerInvariant();
            }

            return text;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Keys
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A cell's key: the name compares case-insensitively (no lower-cased copy per read).</summary>
        private readonly struct CellKey : IEquatable<CellKey>
        {
            internal CellKey(int screen, StateAddress address)
            {
                Screen = screen;
                Scope = address.Scope;
                Container = address.Container;
                Name = address.Name;
            }

            internal int Screen { get; }
            internal StateScope Scope { get; }
            internal string Container { get; }
            internal string Name { get; }

            public bool Equals(CellKey other) => Screen == other.Screen && Scope == other.Scope && string.Equals(Container, other.Container, StringComparison.Ordinal)
                && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

            public override bool Equals(object? obj) => obj is CellKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Screen, Scope, StringComparer.Ordinal.GetHashCode(Container), StringComparer.OrdinalIgnoreCase.GetHashCode(Name));
        }

        /// <summary>A value's key on every screen: the name compares case-insensitively.</summary>
        private readonly struct DefaultKey : IEquatable<DefaultKey>
        {
            internal DefaultKey(StateAddress address)
            {
                Scope = address.Scope;
                Container = address.Container;
                Name = address.Name;
            }

            internal StateScope Scope { get; }
            internal string Container { get; }
            internal string Name { get; }

            public bool Equals(DefaultKey other) => Scope == other.Scope && string.Equals(Container, other.Container, StringComparison.Ordinal)
                && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

            public override bool Equals(object? obj) => obj is DefaultKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Scope, StringComparer.Ordinal.GetHashCode(Container), StringComparer.OrdinalIgnoreCase.GetHashCode(Name));
        }

        private sealed class Cell
        {
            internal Cell(int screen, StateAddress address, Signal signal, DataValue last)
            {
                Screen = screen;
                Address = address;
                Signal = signal;
                Last = last;
            }

            internal int Screen { get; }
            internal StateAddress Address { get; }
            internal Signal Signal { get; }
            internal DataValue Last { get; set; }
        }
    }
}
