using System;
using System.Globalization;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Triggers;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Data.Building;
using UIFramework.Data.Expressions;
using UIFramework.Data.State;
using UIFramework.Hosting;
using UIFramework.Rendering;

namespace UIFramework.Data.Actions
{
    /// <summary>
    /// The v1.4 trigger actions (all prefixed <c>6135.UIFramework_</c>), usable from data UIs and from anywhere the
    /// game runs trigger actions. Keys are state keys (<c>menu.x</c> inside a UI; <c>menu[owner/menu].x</c>,
    /// <c>session[owner].x</c>, <c>player[owner].x</c>, <c>config[owner].x</c>, <c>stat.x</c> from anywhere); menus are
    /// <c>owner/menu</c> (default: the UI the action runs in, else the topmost framework menu).
    /// <list type="bullet">
    ///   <item>state: <c>SetState &lt;key&gt; &lt;value...&gt;</c>, <c>AddState &lt;key&gt; &lt;amount&gt;</c>, <c>ToggleState &lt;key&gt;</c>, <c>ResetState &lt;key&gt;</c> (or <c>ResetState menu</c> for every value of the menu); in a template / data composite body, <c>args.&lt;name&gt;</c> writes through a two-way argument (v1.7);</item>
    ///   <item>feedback: <c>ShowToast &lt;text&gt; [ms] [image ref]</c>, <c>Announce &lt;text&gt;</c>, <c>SetTheme &lt;name&gt;</c>;</item>
    ///   <item>navigation / layout: <c>Focus &lt;id&gt; [menu]</c>, <c>ScrollTo &lt;id&gt; &lt;offset|top|bottom&gt; [menu]</c> (scroll views in pixels, lists / grids in rows), <c>Refresh [menu]</c>, <c>SetPosition &lt;x&gt; &lt;y&gt; [menu]</c>, <c>ResetLayout [menu]</c>;</item>
    ///   <item>HUDs: <c>ShowHud</c> / <c>HideHud</c> / <c>ToggleHud &lt;owner/hud&gt;</c>.</item>
    /// </list>
    /// </summary>
    internal static class StateActions
    {
        private const string Prefix = FrameworkTriggerActions.Prefix;

        /// <summary>Register the actions (once, from <see cref="ModEntry.Entry"/>).</summary>
        internal static void Register(DataService data)
        {
            Add("SetState", (args, scope) => SetState(data, args, scope));
            Add("AddState", (args, scope) => AddState(data, args, scope));
            Add("ToggleState", (args, scope) => ToggleState(data, args, scope));
            Add("ResetState", (args, scope) => ResetState(data, args, scope));
            Add("ShowToast", (args, scope) => ShowToast(data, args, scope));
            Add("Announce", (args, _) => Announce(args));
            Add("SetTheme", (args, scope) => SetTheme(args, scope));
            Add("Focus", (args, scope) => Focus(data, args, scope));
            Add("ScrollTo", (args, scope) => ScrollTo(data, args, scope));
            Add("Refresh", (args, scope) => Refresh(data, args, scope));
            Add("SetPosition", (args, scope) => SetPosition(data, args, scope));
            Add("ResetLayout", (args, scope) => ResetLayout(data, args, scope));
            Add("ShowHud", (args, _) => Hud(data, args, true));
            Add("HideHud", (args, _) => Hud(data, args, false));
            Add("ToggleHud", (args, _) => Hud(data, args, null));
        }

        private delegate string? ActionBody(string[] args, DataScope? scope);

        /// <summary>Register <paramref name="name"/>; the body returns an error message or null.</summary>
        private static void Add(string name, ActionBody body)
        {
            TriggerActionManager.RegisterAction(Prefix + name, (string[] args, TriggerActionContext context, out string error) =>
            {
                string? failure = body(args, DataActionRunner.ScopeOf(context));
                error = failure!; // null on success: the game treats any non-null error as a failure
                return failure == null;
            });
        }

        // ---------------------------------------------------------------------------------------------------------
        //  State
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// v1.7: <c>args.&lt;name&gt;</c> inside a template / data composite body writes through the two-way argument
        /// (the caller's state value). True when the key is an argument (<paramref name="target"/> set, or an error).
        /// </summary>
        private static bool TryArgument(string[] args, DataScope? scope, out Bridge.BindTarget? target, out string? error)
        {
            target = null;
            error = null;
            if (args.Length < 2 || !args[1].StartsWith("args.", StringComparison.Ordinal) || TemplateArgs.Of(scope) is not { } templateArgs)
            {
                return false;
            }

            if (!templateArgs.TryBind(args[1].Substring(5).Trim(), out target, out string bindError))
            {
                error = bindError;
            }

            return true;
        }

        private static string? Write(Bridge.BindTarget target, DataValue value) => target.Write(value, out string error) ? null : error;

        private static string? SetState(DataService data, string[] args, DataScope? scope)
        {
            if (TryArgument(args, scope, out Bridge.BindTarget? argument, out string? argumentError))
            {
                return argument == null ? argumentError : Write(argument, StateAddress.Infer(args.Length > 2 ? string.Join(" ", args, 2, args.Length - 2) : string.Empty));
            }

            if (!TryKey(data, args, scope, out StateAddress address, out string? error))
            {
                return error;
            }

            string text = args.Length > 2 ? string.Join(" ", args, 2, args.Length - 2) : string.Empty;
            return data.State.Write(address, StateAddress.Infer(text), out string writeError) ? null : writeError;
        }

        private static string? AddState(DataService data, string[] args, DataScope? scope)
        {
            if (TryArgument(args, scope, out Bridge.BindTarget? argument, out string? argumentError))
            {
                if (argument == null)
                {
                    return argumentError;
                }

                return ArgUtility.TryGetFloat(args, 2, out float delta, out string deltaError, "float amount") ? Write(argument, DataValue.FromNumber(argument.Read().AsNumber() + delta)) : deltaError;
            }

            if (!TryKey(data, args, scope, out StateAddress address, out string? error))
            {
                return error;
            }

            if (!ArgUtility.TryGetFloat(args, 2, out float amount, out string parseError, "float amount"))
            {
                return parseError;
            }

            double current = data.State.Read(address).AsNumber();
            return data.State.Write(address, DataValue.FromNumber(current + amount), out string writeError) ? null : writeError;
        }

        private static string? ToggleState(DataService data, string[] args, DataScope? scope)
        {
            if (TryArgument(args, scope, out Bridge.BindTarget? argument, out string? argumentError))
            {
                return argument == null ? argumentError : Write(argument, DataValue.FromBool(!argument.Read().AsBool()));
            }

            if (!TryKey(data, args, scope, out StateAddress address, out string? error))
            {
                return error;
            }

            bool current = data.State.Read(address).AsBool();
            return data.State.Write(address, DataValue.FromBool(!current), out string writeError) ? null : writeError;
        }

        private static string? ResetState(DataService data, string[] args, DataScope? scope)
        {
            if (args.Length > 1 && args[1].Equals("menu", StringComparison.OrdinalIgnoreCase))
            {
                string? container = args.Length > 2 ? args[2] : scope?.StateKey;
                if (container == null)
                {
                    return "ResetState menu needs a menu (ResetState menu <owner/menu> outside a UI).";
                }

                data.State.ResetContainer(StateScope.Menu, container);
                return null;
            }

            if (!TryKey(data, args, scope, out StateAddress address, out string? error))
            {
                return error;
            }

            return data.State.Reset(address, out string resetError) ? null : resetError;
        }

        private static bool TryKey(DataService data, string[] args, DataScope? scope, out StateAddress address, out string? error)
        {
            address = default;
            if (!ArgUtility.TryGet(args, 1, out string key, out error, allowBlank: false, "string key"))
            {
                return false;
            }

            if (!StateAddress.TryParse(key, scope, allowBare: scope != null, out address, out string parseError))
            {
                error = parseError;
                return false;
            }

            error = null;
            return true;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Feedback
        // ---------------------------------------------------------------------------------------------------------

        private static string? ShowToast(DataService data, string[] args, DataScope? scope)
        {
            if (!ArgUtility.TryGet(args, 1, out string text, out string error, allowBlank: true, "string text")
                || !ArgUtility.TryGetOptionalInt(args, 2, out int duration, out error, ToastLayer.DefaultDurationMs, "int durationMs")
                || !ArgUtility.TryGetOptional(args, 3, out string icon, out error, null, allowBlank: true, "string icon"))
            {
                return error;
            }

            HudService? hud = UIServices.Hud;
            if (hud == null)
            {
                return "the HUD service is not available.";
            }

            SpriteRef sprite = default;
            if (!string.IsNullOrWhiteSpace(icon) && !data.Sprites.TryResolve(icon, scope?.Owner ?? string.Empty, out sprite, out string iconError))
            {
                return iconError;
            }

            hud.ShowToast(text, sprite.Texture, sprite.Source, duration > 0 ? duration : ToastLayer.DefaultDurationMs);
            return null;
        }

        private static string? Announce(string[] args)
        {
            if (!ArgUtility.TryGetRemainder(args, 1, out string text, out string error, ' ', "string text"))
            {
                return error;
            }

            Accessibility.Announce(text);
            return null;
        }

        private static string? SetTheme(string[] args, DataScope? scope)
        {
            if (!ArgUtility.TryGet(args, 1, out string name, out string error, allowBlank: false, "string theme"))
            {
                return error;
            }

            ThemeSwitcher.Apply(name, scope?.Owner ?? "trigger action");
            DataStateStore.Active?.BumpGlobal();
            return null;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Navigation and layout
        // ---------------------------------------------------------------------------------------------------------

        private static string? Focus(DataService data, string[] args, DataScope? scope)
        {
            if (!ArgUtility.TryGet(args, 1, out string id, out string error, allowBlank: false, "string elementId")
                || !TryMenu(data, args, 2, scope, out UIMenu? menu, out error))
            {
                return error;
            }

            UIElement? element = menu!.Root.FindById(id);
            if (element == null)
            {
                return $"no element '{id}' in menu '{menu.Id}'.";
            }

            ((IUIElement)element).Focus();
            return null;
        }

        private static string? ScrollTo(DataService data, string[] args, DataScope? scope)
        {
            if (!ArgUtility.TryGet(args, 1, out string id, out string error, allowBlank: false, "string elementId")
                || !ArgUtility.TryGet(args, 2, out string where, out error, allowBlank: false, "string offset")
                || !TryMenu(data, args, 3, scope, out UIMenu? menu, out error))
            {
                return error;
            }

            UIElement? element = menu!.Root.FindById(id);
            switch (element)
            {
                case IUIScrollView scroll:
                    scroll.ScrollTo(Offset(where, scroll.MaxScroll));
                    return null;
                case IUIList list:
                    list.ScrollTo(Offset(where, Math.Max(0, list.ItemCount - 1)));
                    return null;
                case IUIDataGrid grid:
                    grid.FirstVisibleIndex = Offset(where, Math.Max(0, grid.RowCount - 1));
                    return null;
                case null:
                    return $"no element '{id}' in menu '{menu.Id}'.";
                default:
                    return $"'{id}' is not a ScrollView, List or DataGrid.";
            }
        }

        private static int Offset(string where, int max)
        {
            return where.ToLowerInvariant() switch
            {
                "top" or "start" => 0,
                "bottom" or "end" => max,
                _ => int.TryParse(where, NumberStyles.Integer, CultureInfo.InvariantCulture, out int offset) ? offset : 0
            };
        }

        private static string? Refresh(DataService data, string[] args, DataScope? scope)
        {
            if (!TryMenu(data, args, 1, scope, out UIMenu? menu, out string error))
            {
                return error;
            }

            data.RefreshMenu(menu!);
            return null;
        }

        private static string? SetPosition(DataService data, string[] args, DataScope? scope)
        {
            if (!ArgUtility.TryGetInt(args, 1, out int x, out string error, "int x")
                || !ArgUtility.TryGetInt(args, 2, out int y, out error, "int y")
                || !TryMenu(data, args, 3, scope, out UIMenu? menu, out error))
            {
                return error;
            }

            menu!.SetPosition(x, y);
            return null;
        }

        private static string? ResetLayout(DataService data, string[] args, DataScope? scope)
        {
            if (!TryMenu(data, args, 1, scope, out UIMenu? menu, out string error))
            {
                return error;
            }

            UIServices.Layouts?.Reset(menu!);
            return null;
        }

        /// <summary>The menu named at <paramref name="index"/>, else the scope's menu, else the topmost open one.</summary>
        private static bool TryMenu(DataService data, string[] args, int index, DataScope? scope, out UIMenu? menu, out string error)
        {
            error = string.Empty;
            if (args.Length > index && !string.IsNullOrWhiteSpace(args[index]))
            {
                menu = data.Find(args[index]);
                if (menu == null)
                {
                    error = $"no menu '{args[index]}' is registered (expected '<owner>/<menu id>').";
                    return false;
                }

                return true;
            }

            menu = scope?.Menu ?? data.Topmost;
            if (menu == null)
            {
                error = "no framework menu is open (name one as '<owner>/<menu id>').";
                return false;
            }

            return true;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  HUDs
        // ---------------------------------------------------------------------------------------------------------

        private static string? Hud(DataService data, string[] args, bool? visible)
        {
            if (!ArgUtility.TryGet(args, 1, out string key, out string error, allowBlank: false, "string hudKey"))
            {
                return error;
            }

            return data.SetHudVisible(key, visible, out error) ? null : error;
        }
    }
}
