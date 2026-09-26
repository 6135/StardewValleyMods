using StardewValley;
using StardewValley.Delegates;
using StardewValley.Triggers;

namespace UIFramework.Data.Actions
{
    /// <summary>
    /// The framework's trigger actions, usable anywhere the game runs trigger actions (<c>Data/TriggerActions</c>,
    /// event <c>action</c> commands, mail and dialogue actions, shop <c>ActionsOnPurchase</c>) and from data UIs:
    /// <list type="bullet">
    ///   <item><c>6135.UIFramework_OpenMenu &lt;owner/menu&gt; [force]</c>: opens a menu (data or C#); without <c>force</c> it waits until the player is free;</item>
    ///   <item><c>6135.UIFramework_OpenMenuAsChild &lt;owner/menu&gt; [parentOwner/menu]</c>: opens it over a parent (default: the topmost open framework menu);</item>
    ///   <item><c>6135.UIFramework_CloseMenu [owner/menu]</c>: closes a menu (default: the topmost one);</item>
    ///   <item><c>6135.UIFramework_ToggleMenu &lt;owner/menu&gt;</c>: closes it when open, else opens it.</item>
    /// </list>
    /// </summary>
    internal static class FrameworkTriggerActions
    {
        internal const string Prefix = "6135.UIFramework_";
        internal const string OpenMenu = Prefix + "OpenMenu";
        internal const string OpenMenuAsChild = Prefix + "OpenMenuAsChild";
        internal const string CloseMenu = Prefix + "CloseMenu";
        internal const string ToggleMenu = Prefix + "ToggleMenu";

        /// <summary>Register the actions (once, from <see cref="ModEntry.Entry"/>).</summary>
        internal static void Register(DataService data)
        {
            TriggerActionManager.RegisterAction(OpenMenu, (string[] args, TriggerActionContext context, out string error) => Succeeded(Open(data, args, out error), ref error));
            TriggerActionManager.RegisterAction(OpenMenuAsChild, (string[] args, TriggerActionContext context, out string error) => Succeeded(OpenAsChild(data, args, out error), ref error));
            TriggerActionManager.RegisterAction(CloseMenu, (string[] args, TriggerActionContext context, out string error) => Succeeded(Close(data, args, out error), ref error));
            TriggerActionManager.RegisterAction(ToggleMenu, (string[] args, TriggerActionContext context, out string error) => Succeeded(Toggle(data, args, out error), ref error));
        }

        /// <summary>Clear the error on success: the game treats any non-null error as a failed action.</summary>
        private static bool Succeeded(bool ok, ref string error)
        {
            if (ok)
            {
                error = null!;
            }

            return ok;
        }

        /// <summary><c>OpenMenu &lt;owner/menu&gt; [force]</c> (shared with the tile / touch action).</summary>
        internal static bool Open(DataService data, string[] args, out string error)
        {
            if (!ArgUtility.TryGet(args, 1, out string key, out error, allowBlank: false, "string menuKey")
                || !ArgUtility.TryGetOptionalBool(args, 2, out bool force, out error, false, "bool force"))
            {
                return false;
            }

            return data.Open(key, force, out error);
        }

        private static bool OpenAsChild(DataService data, string[] args, out string error)
        {
            if (!ArgUtility.TryGet(args, 1, out string key, out error, allowBlank: false, "string menuKey")
                || !ArgUtility.TryGetOptional(args, 2, out string parent, out error, null, allowBlank: true, "string parentKey"))
            {
                return false;
            }

            return data.OpenAsChild(key, parent, out error);
        }

        private static bool Close(DataService data, string[] args, out string error)
        {
            if (!ArgUtility.TryGetOptional(args, 1, out string key, out error, null, allowBlank: true, "string menuKey"))
            {
                return false;
            }

            return data.Close(key, out error);
        }

        private static bool Toggle(DataService data, string[] args, out string error)
        {
            if (!ArgUtility.TryGet(args, 1, out string key, out error, allowBlank: false, "string menuKey"))
            {
                return false;
            }

            return data.Toggle(key, out error);
        }
    }
}
