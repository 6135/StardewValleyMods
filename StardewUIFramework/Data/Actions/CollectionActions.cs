using System;
using System.Linq;
using StardewValley;
using StardewValley.Delegates;
using StardewValley.Triggers;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Data.Building;

namespace UIFramework.Data.Actions
{
    /// <summary>
    /// The v1.5 trigger actions for collections and forms (prefixed <c>6135.UIFramework_</c>). Elements are ids in the
    /// menu the action runs in, or in the menu named by the last argument (<c>owner/menu</c>); outside a UI the
    /// topmost open framework menu is used.
    /// <list type="bullet">
    ///   <item><c>Sort &lt;grid&gt; &lt;column&gt; [desc] [menu]</c>: sort a DataGrid (an unknown column clears the sort);</item>
    ///   <item><c>ClearSelection &lt;grid|list&gt; [menu]</c>;</item>
    ///   <item><c>FormSave [form] [menu]</c>, <c>FormCancel [form] [menu]</c>, <c>Undo [form] [menu]</c>, <c>Redo [form] [menu]</c>: default form: the one the action runs in, else the menu's first form;</item>
    ///   <item><c>Rebuild &lt;element&gt; [menu]</c>: re-read the source of a List / DataGrid / Repeat and rebuild its rows.</item>
    /// </list>
    /// </summary>
    internal static class CollectionActions
    {
        private const string Prefix = FrameworkTriggerActions.Prefix;

        /// <summary>Register the actions (once, from <see cref="ModEntry.Entry"/>).</summary>
        internal static void Register(DataService data)
        {
            Add("Sort", (args, scope) => Sort(data, args, scope));
            Add("ClearSelection", (args, scope) => ClearSelection(data, args, scope));
            Add("FormSave", (args, scope) => Form(data, args, scope, f => f.Save()));
            Add("FormCancel", (args, scope) => Form(data, args, scope, f => f.Cancel()));
            Add("Undo", (args, scope) => Form(data, args, scope, f => f.Undo()));
            Add("Redo", (args, scope) => Form(data, args, scope, f => f.Redo()));
            Add("Rebuild", (args, scope) => Rebuild(data, args, scope));
        }

        private delegate string? ActionBody(string[] args, DataScope? scope);

        private static void Add(string name, ActionBody body)
        {
            TriggerActionManager.RegisterAction(Prefix + name, (string[] args, TriggerActionContext context, out string error) =>
            {
                string? failure = body(args, DataActionRunner.ScopeOf(context));
                error = failure!; // null on success: the game treats any non-null error as a failure
                return failure == null;
            });
        }

        private static string? Sort(DataService data, string[] args, DataScope? scope)
        {
            if (!ArgUtility.TryGet(args, 1, out string id, out string error, allowBlank: false, "string gridId")
                || !ArgUtility.TryGet(args, 2, out string column, out error, allowBlank: true, "string columnId"))
            {
                return error;
            }

            int next = 3;
            bool descending = false;
            if (args.Length > next && !IsMenuKey(args[next]))
            {
                string flag = args[next].Trim().ToLowerInvariant();
                descending = flag is "desc" or "descending" or "true";
                if (!descending && flag is not ("asc" or "ascending" or "false"))
                {
                    return $"'{args[next]}' is not desc / asc.";
                }

                next++;
            }

            if (!TryElement(data, args, id, next, scope, out UIElement? element, out error))
            {
                return error;
            }

            if (element is not IUIDataGrid grid)
            {
                return $"'{id}' is not a DataGrid.";
            }

            grid.Sort(column, descending);
            return null;
        }

        private static string? ClearSelection(DataService data, string[] args, DataScope? scope)
        {
            if (!ArgUtility.TryGet(args, 1, out string id, out string error, allowBlank: false, "string elementId")
                || !TryElement(data, args, id, 2, scope, out UIElement? element, out error))
            {
                return error;
            }

            switch (element)
            {
                case IUIDataGrid grid:
                    grid.ClearSelection();
                    return null;
                case IUIList list:
                    list.SelectedIndex = -1;
                    return null;
                default:
                    return $"'{id}' is not a DataGrid or List.";
            }
        }

        private static string? Form(DataService data, string[] args, DataScope? scope, Action<IUIForm> apply)
        {
            // [form] [menu]: a first argument with a '/' is the menu
            string? id = args.Length > 1 && !IsMenuKey(args[1]) ? args[1] : null;
            int menuIndex = id != null ? 2 : 1;
            if (!TryMenu(data, args, menuIndex, scope, out UIMenu? menu, out string error))
            {
                return error;
            }

            IUIForm? form;
            if (id != null)
            {
                form = menu!.Root.FindById(id) as IUIForm;
                if (form == null)
                {
                    return $"'{id}' is not a Form in menu '{menu.Id}'.";
                }
            }
            else
            {
                form = EnclosingForm(scope?.Element) ?? FirstForm(menu!.Root);
                if (form == null)
                {
                    return $"menu '{menu!.Id}' has no Form.";
                }
            }

            apply(form);
            return null;
        }

        private static string? Rebuild(DataService data, string[] args, DataScope? scope)
        {
            if (!ArgUtility.TryGet(args, 1, out string id, out string error, allowBlank: false, "string elementId")
                || !TryMenu(data, args, 2, scope, out UIMenu? menu, out error))
            {
                return error;
            }

            DataRuntime? runtime = data.RuntimeOf(menu!);
            if (runtime == null || !runtime.Collections.TryGetValue(id, out IDataCollection? collection))
            {
                return $"'{id}' is not a data List, DataGrid or Repeat in menu '{menu!.Id}'.";
            }

            collection.Rebuild();
            return null;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Helpers
        // ---------------------------------------------------------------------------------------------------------

        private static bool IsMenuKey(string text) => text.Contains('/');

        private static IUIForm? EnclosingForm(UIElement? element)
        {
            for (UIElement? current = element; current != null; current = current.ParentElement)
            {
                if (current is IUIForm form)
                {
                    return form;
                }
            }

            return null;
        }

        private static IUIForm? FirstForm(UIElement root)
        {
            if (root is IUIForm form)
            {
                return form;
            }

            if (root is UIContainer container)
            {
                foreach (UIElement child in container.Children.ToArray())
                {
                    IUIForm? found = FirstForm(child);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        private static bool TryElement(DataService data, string[] args, string id, int menuIndex, DataScope? scope, out UIElement? element, out string error)
        {
            element = null;
            if (!TryMenu(data, args, menuIndex, scope, out UIMenu? menu, out error))
            {
                return false;
            }

            element = menu!.Root.FindById(id);
            if (element == null)
            {
                error = $"no element '{id}' in menu '{menu.Id}'.";
                return false;
            }

            return true;
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
    }
}
