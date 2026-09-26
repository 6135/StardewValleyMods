using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using UIFramework.Data.State;

namespace UIFramework.Data.Actions
{
    /// <summary>
    /// The optional Content Patcher token <c>{{6135.UIFramework/State: &lt;owner&gt;/&lt;scope.name&gt;}}</c>, e.g.
    /// <c>{{6135.UIFramework/State: Your.Pack/config.spawnRate}}</c>: patches can use UI state and <c>config.*</c> in
    /// <c>When</c> and text. <c>menu.*</c> needs the qualified form (<c>Your.Pack/menu[Your.Pack/settings].tab</c>).
    /// Values are snapshotted per screen and input: <see cref="GetValues"/> returns the snapshot, and only
    /// <see cref="UpdateContext"/> re-reads it (reporting whether any value changed), as Content Patcher requires; so the
    /// token changes at Content Patcher's normal update points (day start, location change, <c>patch update</c>).
    /// </summary>
    /// <remarks>Content Patcher calls these members by name (advanced token API); they must stay public.</remarks>
    public sealed class ContentPatcherToken
    {
        private readonly Func<DataStateStore?> store;
        private readonly PerScreen<Dictionary<string, string?>> snapshots = new(() => new Dictionary<string, string?>(StringComparer.Ordinal));

        internal ContentPatcherToken(Func<DataStateStore?> store)
        {
            this.store = store;
        }

        /// <summary>Register the token when Content Patcher is installed (from <c>GameLaunched</c>).</summary>
        internal static void Register(IModHelper helper, IManifest manifest, IMonitor monitor)
        {
            IContentPatcherApi? api = helper.ModRegistry.GetApi<IContentPatcherApi>("Pathoschild.ContentPatcher");
            if (api == null)
            {
                return;
            }

            try
            {
                api.RegisterToken(manifest, "State", new ContentPatcherToken(() => DataStateStore.Active));
                monitor.Log("Registered the Content Patcher token 6135.UIFramework/State.", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                monitor.Log($"Could not register the Content Patcher token: {ex.Message}", LogLevel.Warn);
            }
        }

        /// <summary>The token always takes an input.</summary>
        public bool AllowsInput() => true;

        /// <summary>The token always takes an input.</summary>
        public bool RequiresInput() => true;

        /// <summary>One value per input.</summary>
        public bool CanHaveMultipleValues(string? input = null) => false;

        /// <summary>Check an input's syntax (<c>&lt;owner&gt;/&lt;scope.name&gt;</c>).</summary>
        public bool TryValidateInput(string? input, out string error)
        {
            return TryParse(input, out _, out error);
        }

        /// <summary>The token can always be read (values that need a save are empty before one is loaded).</summary>
        public bool IsReady() => store() != null;

        /// <summary>The value (as text) for the current player, as of the last <see cref="UpdateContext"/> (or of its first read).</summary>
        public IEnumerable<string> GetValues(string? input)
        {
            string key = input?.Trim() ?? string.Empty;
            Dictionary<string, string?> snapshot = snapshots.Value;
            if (!snapshot.TryGetValue(key, out string? value))
            {
                snapshot[key] = value = Read(key);
            }

            return value == null ? Array.Empty<string>() : new[] { value };
        }

        /// <summary>Re-read every input used on this screen; true when any value changed.</summary>
        public bool UpdateContext()
        {
            Dictionary<string, string?> snapshot = snapshots.Value;
            bool changed = false;
            foreach (string key in snapshot.Keys.ToArray())
            {
                string? value = Read(key);
                if (value != snapshot[key])
                {
                    snapshot[key] = value;
                    changed = true;
                }
            }

            return changed;
        }

        /// <summary>The current value of an input (as text) for the current player, or null.</summary>
        private string? Read(string input)
        {
            DataStateStore? state = store();
            if (state == null || !TryParse(input, out StateAddress address, out _))
            {
                return null;
            }

            if ((address.Scope is StateScope.Player or StateScope.Stat) && !Context.IsWorldReady)
            {
                return null;
            }

            return state.TryRead(address, out Expressions.DataValue value, out _) && !value.IsNull ? value.AsString() : null;
        }

        /// <summary>Parse <c>&lt;owner&gt;/&lt;scope.name&gt;</c> into a state address.</summary>
        private static bool TryParse(string? input, out StateAddress address, out string error)
        {
            address = default;
            input = input?.Trim();
            int slash = input?.IndexOf('/') ?? -1;
            if (string.IsNullOrEmpty(input) || slash <= 0 || slash == input.Length - 1)
            {
                error = "the input must be '<owner mod id>/<scope>.<name>', e.g. 'Your.Pack/config.volume'.";
                return false;
            }

            string owner = input.Substring(0, slash).Trim();
            string key = input.Substring(slash + 1).Trim();
            return StateAddress.TryParse(key, DataScope.ForOwner(owner), allowBare: false, out address, out error);
        }
    }
}
