using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using UIFramework.Core;
using UIFramework.Data;
using UIFramework.Data.Loading;
using UIFramework.Data.State;

namespace UIFramework.Hosting
{
    /// <summary>
    /// The framework's SMAPI console commands (architecture.md §16.2 "Debug console"): <c>ui_list</c>, <c>ui_dump</c>,
    /// <c>ui_find</c>, <c>ui_perf</c>, <c>ui_inspect</c> and <c>ui_export</c>; with data menus (v1.3) also
    /// <c>ui_open</c>, <c>ui_close</c>, <c>ui_validate</c>, <c>ui_data</c>, <c>ui_schema</c> and <c>ui_reload</c>, and (v1.4)
    /// <c>ui_state</c>.
    /// </summary>
    internal sealed class DebugConsole
    {
        private readonly MenuRegistry menus;
        private readonly IMonitor monitor;
        private readonly DataService? data;
        private readonly string? schemaDirectory;

        internal DebugConsole(MenuRegistry menus, IMonitor monitor, DataService? data = null, string? schemaDirectory = null)
        {
            this.menus = menus;
            this.monitor = monitor;
            this.data = data;
            this.schemaDirectory = schemaDirectory;
        }

        internal void Register(ICommandHelper commands)
        {
            commands.Add("ui_list", "List the open UI Framework menus: consumer, menu id, host type and element count.", (_, _) => List());
            commands.Add("ui_dump", "Print the element tree of a menu.\n\nUsage: ui_dump [<consumerId> <menuId> | <consumerId>/<menuId>]\nWithout arguments the most recently opened menu is dumped.", (_, args) => Dump(args));
            commands.Add("ui_find", "List every element of the open menus whose id contains the text.\n\nUsage: ui_find <text>", (_, args) => Find(args));
            commands.Add("ui_perf", "Per-menu timing of the last 60 frames.\n\nUsage: ui_perf on|off|show", (_, args) => Perf(args));
            commands.Add("ui_inspect", "Toggle the in-game inspector (hover elements to see their layout; the info panel lists the editing keys).", (_, _) => Inspector.Toggle());
            commands.Add("ui_export", "Export a menu as C# builder code (or JSON with 'json') to the log, Mods/UIFramework/export and the clipboard.\n\nUsage: ui_export [<consumerId> <menuId> | <consumerId>/<menuId>] [json]", (_, args) => Export(args));

            // BEGIN DATA console
            if (data != null)
            {
                commands.Add("ui_open", "Open a framework menu (data or C#).\n\nUsage: ui_open <owner/menu> [force]\nWithout 'force' the menu waits until the player is free.", (_, args) => OpenMenu(args));
                commands.Add("ui_close", "Close a framework menu.\n\nUsage: ui_close [<owner/menu>]\nWithout arguments the topmost open menu is closed.", (_, args) => CloseMenu(args));
                commands.Add("ui_validate", $"Re-read {DataAssets.Menus} / {DataAssets.Sprites} and print every validation message.\n\nUsage: ui_validate [<owner/menu>]", (_, args) => Validate(args));
                commands.Add("ui_data", "List the data menus, HUDs, composites, contributions and sprites and their status.", (_, _) => ListData());
                commands.Add("ui_schema", "Write JSON Schemas of the data menu format (for VS Code autocomplete) to Mods/UIFramework/schema.", (_, _) => WriteSchema());
                commands.Add("ui_reload", "Re-read the data menu assets (and files imported with ImportDataFile) now and rebuild the entries that changed.", (_, _) => Reload());
                commands.Add("ui_state", "Inspect or change the named state of data UIs (current screen).\n\nUsage: ui_state [list [text]] | get <key> | set <key> <value...> | reset <key>\nKeys are qualified: menu[owner/menu].x, session[owner].x, player[owner].x, config[owner].x, stat.x.", (_, args) => StateCommand(args));
            }
            // END DATA console
        }

        private void Log(string message) => monitor.Log(message, LogLevel.Info);

        /// <summary>The menu named by <c>&lt;consumerId&gt; &lt;menuId&gt;</c> or <c>&lt;consumerId&gt;/&lt;menuId&gt;</c>, or the most recently opened one; null (logged) when there is none.</summary>
        private UIMenu? Resolve(string[] args)
        {
            if (args.Length == 1 && args[0].Contains('/'))
            {
                int slash = args[0].IndexOf('/');
                return Resolve(new[] { args[0].Substring(0, slash), args[0].Substring(slash + 1) });
            }

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
            bool json = args.Length > 0 && args[^1].Equals("json", StringComparison.OrdinalIgnoreCase);
            UIMenu? menu = Resolve(json ? args[..^1] : args);
            if (menu != null)
            {
                Inspector.Export(menu, json);
            }
        }

        // BEGIN DATA console

        private void OpenMenu(string[] args)
        {
            if (args.Length == 0)
            {
                Log("Usage: ui_open <owner/menu> [force]");
                return;
            }

            bool force = args.Length > 1 && args[1].Equals("force", StringComparison.OrdinalIgnoreCase);
            Log(data!.Open(args[0], force, out string error) ? $"Opening '{args[0]}'." : $"Could not open '{args[0]}': {error}");
        }

        private void CloseMenu(string[] args)
        {
            string? key = args.Length > 0 ? args[0] : null;
            if (!data!.Close(key, out string error))
            {
                Log(error);
            }
        }

        private void Validate(string[] args)
        {
            data!.Reload();
            string? filter = args.Length > 0 ? args[0] : null;
            var lines = new List<string>();
            foreach (DataMessage message in data.AssetMessages.Items)
            {
                lines.Add("  " + message);
            }

            int entries = 0;
            foreach (DataService.EntryStatus status in data.Statuses)
            {
                if (filter != null && !status.Key.Equals(filter, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                entries++;
                foreach (DataMessage message in status.Messages.Items)
                {
                    lines.Add("  " + message);
                }
            }

            string checkedText = $"{entries} data entr{(entries == 1 ? "y" : "ies")} checked";
            Log(lines.Count == 0 ? $"{checkedText}; no problems." : $"{checkedText}:\n{string.Join("\n", lines)}");
        }

        private void ListData()
        {
            var lines = new List<string>();
            var compositeLines = new List<string>();
            var contributionLines = new List<string>();
            foreach (DataService.EntryStatus status in data!.Statuses)
            {
                DataMessageLog log = status.Messages;
                string counts = $"{log.Count(DataSeverity.Error)} error(s), {log.Count(DataSeverity.Warning)} warning(s), {log.Count(DataSeverity.Info)} note(s)  hash {status.Hash}";
                if (status.Key.StartsWith("composite:", StringComparison.Ordinal))
                {
                    string name = status.Key.Substring("composite:".Length);
                    string owner = data.Composites.Runtimes.TryGetValue(name, out DataCompositeRuntime? composite) ? $" (owner {composite.Owner})" : string.Empty;
                    compositeLines.Add($"  {name}{owner}  [{status.State}]  {counts}");
                }
                else if (status.Key.StartsWith("contribution:", StringComparison.Ordinal))
                {
                    contributionLines.Add($"  {status.Key.Substring("contribution:".Length)}  [{status.State}]  {counts}");
                }
                else
                {
                    lines.Add($"  {status.Key}  [{status.State}]  {counts}");
                }
            }

            string sprites = data.Sprites.Count == 0 ? "none" : string.Join(", ", data.Sprites.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
            string menusText = (lines.Count == 0 ? $"No entries in {DataAssets.Menus} / {DataAssets.Huds}." : $"Data menus and HUDs ({DataAssets.Menus}, {DataAssets.Huds}):\n{string.Join("\n", lines)}")
                + (compositeLines.Count == 0 ? $"\nNo entries in {DataAssets.Composites}." : $"\nData composites ({DataAssets.Composites}):\n{string.Join("\n", compositeLines)}")
                + (contributionLines.Count == 0 ? $"\nNo entries in {DataAssets.Contributions}." : $"\nContributions ({DataAssets.Contributions}):\n{string.Join("\n", contributionLines)}");
            string hooks = UIServices.Hooks == null ? string.Empty : string.Join("\n  ", UIServices.Hooks.Describe());
            string imports = string.Join("\n  ", Data.Bridge.DataImport.Describe());
            Log($"{menusText}\nSprites ({DataAssets.Sprites}): {sprites}"
                + (hooks.Length > 0 ? $"\nC# hooks:\n  {hooks}" : string.Empty)
                + (imports.Length > 0 ? $"\nImported data:\n  {imports}" : string.Empty));
        }

        private void WriteSchema()
        {
            if (schemaDirectory == null)
            {
                Log("No schema directory is configured.");
                return;
            }

            try
            {
                string[] files = SchemaWriter.Write(schemaDirectory);
                Log($"Wrote {files.Length} schema file(s):\n  {string.Join("\n  ", files)}");
            }
            catch (Exception ex)
            {
                monitor.Log($"Could not write the schemas: {ex}", LogLevel.Error);
            }
        }

        private void Reload()
        {
            int files = Data.Bridge.DataImport.ReloadFiles(); // files imported with ImportDataFile (v1.6)
            int changed = data!.Reload();
            Log($"Data menus reloaded: {changed} built or rebuilt{(files > 0 ? $" ({files} imported file(s) re-read)" : string.Empty)}.");
        }

        private void StateCommand(string[] args)
        {
            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "list";
            switch (mode)
            {
                case "list":
                {
                    string? filter = args.Length > 1 ? args[1] : null;
                    var lines = new List<string>();
                    foreach ((StateAddress address, Data.Expressions.DataValue value) in data!.State.Snapshot())
                    {
                        string key = address.ToString();
                        if (filter == null || key.Contains(filter, StringComparison.OrdinalIgnoreCase))
                        {
                            lines.Add($"  {key} = {Describe(value)}");
                        }
                    }

                    Log(lines.Count == 0 ? "No data UI state on this screen (player.* and stat.* live in the save; use ui_state get)." : $"Data UI state (screen {Context.ScreenId}):\n{string.Join("\n", lines)}");
                    return;
                }

                case "get":
                case "set":
                case "reset":
                {
                    if (args.Length < 2)
                    {
                        Log($"Usage: ui_state {mode} <key>");
                        return;
                    }

                    if (!StateAddress.TryParse(args[1], null, allowBare: false, out StateAddress address, out string error))
                    {
                        Log(error);
                        return;
                    }

                    if (mode == "set")
                    {
                        string text = args.Length > 2 ? string.Join(" ", args, 2, args.Length - 2) : string.Empty;
                        if (!data!.State.Write(address, StateAddress.Infer(text), out error))
                        {
                            Log(error);
                            return;
                        }
                    }
                    else if (mode == "reset" && !data!.State.Reset(address, out error))
                    {
                        Log(error);
                        return;
                    }

                    Log($"{address} = {Describe(data!.State.Read(address))}");
                    return;
                }

                default:
                    Log("Usage: ui_state [list [text]] | get <key> | set <key> <value...> | reset <key>");
                    return;
            }
        }

        private static string Describe(Data.Expressions.DataValue value)
        {
            return value.Kind switch
            {
                Data.Expressions.DataKind.Null => "(none)",
                Data.Expressions.DataKind.String => "'" + value.AsString() + "'",
                _ => value.AsString()
            };
        }

        // END DATA console
    }
}
