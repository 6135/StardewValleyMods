using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using UIFramework.Core;

namespace UIFramework.Hosting
{
    /// <summary>
    /// The framework's SMAPI console commands (architecture.md §16.2 "Debug console"): <c>ui_list</c>, <c>ui_dump</c>,
    /// <c>ui_find</c>, <c>ui_perf</c>, <c>ui_inspect</c> and <c>ui_export</c>.
    /// </summary>
    internal sealed class DebugConsole
    {
        private readonly MenuRegistry menus;
        private readonly IMonitor monitor;

        internal DebugConsole(MenuRegistry menus, IMonitor monitor)
        {
            this.menus = menus;
            this.monitor = monitor;
        }

        internal void Register(ICommandHelper commands)
        {
            commands.Add("ui_list", "List the open UI Framework menus: consumer, menu id, host type and element count.", (_, _) => List());
            commands.Add("ui_dump", "Print the element tree of a menu.\n\nUsage: ui_dump [<consumerId> <menuId>]\nWithout arguments the most recently opened menu is dumped.", (_, args) => Dump(args));
            commands.Add("ui_find", "List every element of the open menus whose id contains the text.\n\nUsage: ui_find <text>", (_, args) => Find(args));
            commands.Add("ui_perf", "Per-menu timing of the last 60 frames.\n\nUsage: ui_perf on|off|show", (_, args) => Perf(args));
            commands.Add("ui_inspect", "Toggle the in-game inspector (hover elements to see their layout; the info panel lists the editing keys).", (_, _) => Inspector.Toggle());
            commands.Add("ui_export", "Export a menu as C# builder code to the log, Mods/UIFramework/export and the clipboard.\n\nUsage: ui_export [<consumerId> <menuId>]", (_, args) => Export(args));
        }

        private void Log(string message) => monitor.Log(message, LogLevel.Info);

        /// <summary>The menu named by <c>&lt;consumerId&gt; &lt;menuId&gt;</c>, or the most recently opened one; null (logged) when there is none.</summary>
        private UIMenu? Resolve(string[] args)
        {
            if (args.Length >= 2)
            {
                UIMenu? named = menus.Get(args[0], args[1]);
                if (named == null)
                {
                    Log($"No menu '{args[1]}' is registered by '{args[0]}'.");
                }

                return named;
            }

            if (menus.OpenMenus.Count == 0)
            {
                Log("No framework menu is open.");
                return null;
            }

            return menus.OpenMenus[^1];
        }

        private void List()
        {
            IReadOnlyList<UIMenu> open = menus.OpenMenus;
            if (open.Count == 0)
            {
                Log("No framework menus are open.");
                return;
            }

            var lines = new List<string>();
            foreach (UIMenu menu in open)
            {
                string host = menu.Host?.IsActiveMenu == true ? "active" : "child";
                int count = menu.Root.SelfAndDescendants().Count();
                lines.Add($"  {menu.Consumer.ModId}  {menu.Id}  [{host}]  {count} element(s)");
            }
            Log("Open menus (consumer, menu id, host, elements):\n" + string.Join("\n", lines));
        }

        private void Dump(string[] args)
        {
            UIMenu? menu = Resolve(args);
            if (menu != null)
            {
                Log($"{menu} bounds [{menu.Bounds.X},{menu.Bounds.Y} {menu.Bounds.Width}x{menu.Bounds.Height}]:\n{TreeDump.Render(menu.Root, includeState: true)}");
            }
        }

        private void Find(string[] args)
        {
            if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            {
                Log("Usage: ui_find <text>");
                return;
            }

            string text = args[0];
            var lines = new List<string>();
            foreach (UIMenu menu in menus.OpenMenus)
            {
                foreach (UIElement element in menu.Root.SelfAndDescendants())
                {
                    if (element.Id.Contains(text, StringComparison.OrdinalIgnoreCase))
                    {
                        lines.Add($"  {menu.Consumer.ModId}/{menu.Id}: {TreeDump.Line(element, includeState: true)}");
                    }
                }
            }
            Log(lines.Count == 0 ? $"No element id contains '{text}' in the open menus." : $"Elements matching '{text}':\n{string.Join("\n", lines)}");
        }

        private void Perf(string[] args)
        {
            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "show";
            switch (mode)
            {
                case "on":
                    PerfCounters.Enabled = true;
                    Log("Perf counters enabled; run 'ui_perf show' after a few frames.");
                    return;
                case "off":
                    PerfCounters.Enabled = false;
                    Log("Perf counters disabled.");
                    return;
                default:
                    ShowPerf();
                    return;
            }
        }

        private void ShowPerf()
        {
            if (!PerfCounters.Enabled)
            {
                Log("Perf counters are off; run 'ui_perf on' first.");
                return;
            }

            var lines = new List<string>();
            foreach (UIMenu menu in menus.OpenMenus)
            {
                lines.Add($"  {menu.Consumer.ModId}/{menu.Id}: {PerfCounters.Describe(menu) ?? "no frames sampled yet"}");
            }
            Log(lines.Count == 0 ? "No framework menu is open." : "Perf (last 60 frames):\n" + string.Join("\n", lines));
        }

        private void Export(string[] args)
        {
            UIMenu? menu = Resolve(args);
            if (menu != null)
            {
                Inspector.Export(menu);
            }
        }
    }
}
