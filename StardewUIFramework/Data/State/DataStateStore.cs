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
    /// <para>
    /// Defaults (menu <c>State</c>, input <c>Value</c>s) are registered once per build and only ever fill values that do
    /// not exist yet, lazily on the screen that reads them. Every write bumps the screen's epoch (the key of the
    /// expression caches) and raises <see cref="Changed"/> (menu <c>Watch</c>es, the Content Patcher token).
    /// </para>
    /// </summary>
    internal sealed class DataStateStore
    {
        private readonly Func<string, ConsumerContext> contexts;
        private readonly Dictionary<CellKey, Cell> cells = new();
        private readonly Dictionary<Signal, Cell> bySignal = new();
        private readonly Dictionary<DefaultKey, Func<DataValue>> defaults = new();
        private long[] epochs = new long[4];
        private long globalEpoch;
        [ThreadStatic] private static int silentDepth;

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

        /// <summary>Incremented on every change on any screen (the Content Patcher token's <c>UpdateContext</c>).</summary>
        internal long ChangeCount { get; private set; }

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
                    if (Game1.player?.modData != null && Game1.player.modData.TryGetValue(ModDataKey(address), out string? text))
                    {
                        value = StateAddress.Infer(text);
                        return true;
                    }

                    return TryDefault(address, out value);
                }

                case StateScope.Stat:
                    isVolatile = true;
                    if (Game1.player?.stats != null)
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
                        value = StateAddress.Infer(text);
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
                    if (Game1.player?.modData == null)
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
                    if (Game1.player?.stats == null)
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
                        bySignal.Remove(cell.Signal);
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
            foreach ((CellKey key, Cell cell) in cells.Where(p => p.Key.Screen == screen && p.Key.Scope == scope && p.Key.Container == container).ToArray())
            {
                cells.Remove(key);
                bySignal.Remove(cell.Signal);
            }

            BumpScreen(screen);
        }

        /// <summary>Drop every menu and session value (return to title).</summary>
        internal void ClearSession()
        {
            cells.Clear();
            bySignal.Clear();
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
            bySignal[signal] = cell;
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
                    yield return (new StateAddress(StateScope.Config, owner, name), StateAddress.Infer(value));
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Notification
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Run <paramref name="action"/> without raising <see cref="Changed"/> (epochs are still bumped).</summary>
        internal void Silently(Action action)
        {
            silentDepth++;
            try
            {
                action();
            }
            finally
            {
                silentDepth--;
            }
        }

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
            ChangeCount++;
            if (silentDepth > 0 || Changed == null)
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

        private static string ModDataKey(StateAddress address) => address.Container + "/" + address.Name;

        // ---------------------------------------------------------------------------------------------------------
        //  Keys
        // ---------------------------------------------------------------------------------------------------------

        private readonly record struct CellKey(int Screen, StateScope Scope, string Container, string Name)
        {
            internal CellKey(int screen, StateAddress address) : this(screen, address.Scope, address.Container, address.Name.ToLowerInvariant())
            {
            }
        }

        private readonly record struct DefaultKey(StateScope Scope, string Container, string Name)
        {
            internal DefaultKey(StateAddress address) : this(address.Scope, address.Container, address.Name.ToLowerInvariant())
            {
            }
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
