using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using UIFramework.Components;
using UIFramework.Core;
using UIFramework.Rendering;

namespace UIFramework.Hosting
{
    /// <summary>
    /// The in-game inspector (architecture.md §16.2), toggled by <c>ui_inspect</c> or <see cref="ModConfig.InspectorHotkey"/>.
    /// While it is on, <see cref="MenuHost"/> hands the mouse and keyboard here instead of routing them: the element
    /// under the cursor (or the pinned one) is highlighted by <see cref="InspectorRenderer"/> with an info panel, and
    /// the keys nudge its margins / size, toggle its visibility, pin it or export the menu as builder code
    /// (<see cref="TreeExporter"/>).
    /// </summary>
    internal static class Inspector
    {
        private const int SmallStep = 1;
        private const int LargeStep = 8;

        private static KeybindList hotkey = new();
        private static string hotkeySource = string.Empty;

        /// <summary>Whether the inspector is on (menus then suppress their normal input).</summary>
        internal static bool Enabled { get; private set; }

        /// <summary>Element the user pinned (click / P) so the panel stays on it while the cursor moves; null = follow the cursor.</summary>
        internal static UIElement? Pinned { get; private set; }

        /// <summary>Folder that receives <c>&lt;consumer&gt;-&lt;menu&gt;.cs</c> exports (empty = no file).</summary>
        internal static string ExportDirectory { get; set; } = string.Empty;

        /// <summary>Key help shown at the bottom of the info panel.</summary>
        internal const string KeyHelp = "arrows: margin (Shift x8)  +/-: width (Shift: height, Ctrl x8)  V: visible  P: pin  E: export  Esc: off";

        internal static void Toggle() => SetEnabled(!Enabled);

        internal static void SetEnabled(bool enabled)
        {
            Enabled = enabled;
            Pinned = null;
            UIServices.Log($"Inspector {(enabled ? "enabled" : "disabled")}.", LogLevel.Info);
        }

        /// <summary>Parse a keybind list string, falling back to an unbound list when it is invalid.</summary>
        internal static KeybindList ParseHotkey(string? text)
        {
            return KeybindList.TryParse(text ?? string.Empty, out KeybindList? parsed, out _) ? parsed : new KeybindList();
        }

        /// <summary>Called on <c>Input.ButtonsChanged</c>: toggles the inspector when the configured hotkey was just pressed.</summary>
        internal static void OnButtonsChanged()
        {
            string source = UIServices.Config.InspectorHotkey ?? string.Empty;
            if (source != hotkeySource)
            {
                hotkeySource = source;
                hotkey = ParseHotkey(source);
            }

            if (hotkey.JustPressed())
            {
                Toggle();
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Subject
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The element the panel describes: the pinned element (if it belongs to <paramref name="menu"/>), else the one under the cursor.</summary>
        internal static UIElement? Subject(UIMenu menu)
        {
            if (Pinned != null && Pinned.OwnerMenu == menu)
            {
                return Pinned;
            }

            return Pick(menu.Root, menu.CursorX, menu.CursorY);
        }

        /// <summary>
        /// Deepest visible element whose bounds contain the point. Unlike the router's hit-test this ignores
        /// <c>Enabled</c> and hit-test visibility so layout-only containers (stacks, grids, spacers) can be inspected.
        /// </summary>
        internal static UIElement? Pick(UIElement element, int x, int y)
        {
            if (!element.Visible || !element.Bounds.Contains(x, y))
            {
                return null;
            }

            if (element is UIContainer container)
            {
                for (int i = container.Children.Count - 1; i >= 0; i--)
                {
                    UIElement? hit = Pick(container.Children[i], x, y);
                    if (hit != null)
                    {
                        return hit;
                    }
                }
            }
            return element;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Input (from MenuHost while enabled)
        // ---------------------------------------------------------------------------------------------------------

        internal static void HandleHover(UIMenu menu, int x, int y)
        {
            menu.Router.SetHovered(null);
            menu.CursorX = x;
            menu.CursorY = y;
        }

        /// <summary>A click pins the element under the cursor (or unpins the current one).</summary>
        internal static void HandleClick(UIMenu menu, int x, int y)
        {
            menu.CursorX = x;
            menu.CursorY = y;
            TogglePin(menu);
        }

        internal static void HandleKey(UIMenu menu, Keys key, bool shift, bool ctrl)
        {
            switch (key)
            {
                case Keys.Escape:
                    SetEnabled(false);
                    return;
                case Keys.E:
                    Export(menu);
                    return;
                case Keys.P:
                    TogglePin(menu);
                    return;
                default:
                    break;
            }

            UIElement? subject = Subject(menu);
            if (subject != null)
            {
                Edit(subject, key, shift, ctrl);
            }
        }

        /// <summary>Arrows nudge the margins, +/- resize, V toggles visibility.</summary>
        private static void Edit(UIElement element, Keys key, bool shift, bool ctrl)
        {
            int nudge = shift ? LargeStep : SmallStep;
            int resize = ctrl ? LargeStep : SmallStep;
            switch (key)
            {
                case Keys.Left:
                    element.MarginLeft -= nudge;
                    break;
                case Keys.Right:
                    element.MarginLeft += nudge;
                    break;
                case Keys.Up:
                    element.MarginTop -= nudge;
                    break;
                case Keys.Down:
                    element.MarginTop += nudge;
                    break;
                case Keys.OemPlus:
                case Keys.Add:
                    Resize(element, shift, resize);
                    break;
                case Keys.OemMinus:
                case Keys.Subtract:
                    Resize(element, shift, -resize);
                    break;
                case Keys.V:
                    element.Visible = !element.Visible;
                    break;
                default:
                    break;
            }
        }

        /// <summary>Change the explicit width (or height); an element without one starts from its arranged size.</summary>
        private static void Resize(UIElement element, bool height, int delta)
        {
            if (height)
            {
                element.Height = Math.Max(0, (element.Height ?? element.Bounds.Height) + delta);
            }
            else
            {
                element.Width = Math.Max(0, (element.Width ?? element.Bounds.Width) + delta);
            }
        }

        private static void TogglePin(UIMenu menu)
        {
            Pinned = Pinned != null && Pinned.OwnerMenu == menu ? null : Pick(menu.Root, menu.CursorX, menu.CursorY);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Info panel text
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>The info panel lines for <paramref name="e"/>.</summary>
        internal static string[] Describe(UIElement e)
        {
            var lines = new List<string>
            {
                $"{e.GetType().Name} '{e.Id}'{(Pinned == e ? "  (pinned)" : string.Empty)}",
                $"bounds: {e.Bounds.X},{e.Bounds.Y} {e.Bounds.Width}x{e.Bounds.Height}   desired: {Fmt(e.DesiredSize.X)}x{Fmt(e.DesiredSize.Y)}",
                $"margin: {e.MarginLeft} {e.MarginTop} {e.MarginRight} {e.MarginBottom}   size: {SizeText(e.Width)} x {SizeText(e.Height)}",
                $"align: {e.ResolvedHorizontalAlign}{(e.HorizontalAlignSet ? string.Empty : "*")} / {e.ResolvedVerticalAlign}{(e.VerticalAlignSet ? string.Empty : "*")}   (* = parent default)"
            };
            if (e.ParentElement is Grid)
            {
                lines.Add($"cell: row {e.Row}, column {e.Column}, span {e.RowSpan}x{e.ColumnSpan}");
            }

            if (e.ParentElement is Canvas)
            {
                lines.Add($"position: {e.X}, {e.Y}");
            }

            lines.Add("style: " + (e.StyleObject == null ? "(inherited)" : DescribeStyle(e.StyleObject)));
            lines.Add($"state: {(e.Visible ? "visible" : "hidden")}, {(e.Enabled ? "enabled" : "disabled")}{(e.Focusable ? ", focusable" : string.Empty)}{(e.IsFocused ? ", focused" : string.Empty)}");
            lines.Add("parent: " + ParentChain(e));
            lines.Add(KeyHelp);
            return lines.ToArray();
        }

        private static string Fmt(float value) => value.ToString("0.#", CultureInfo.InvariantCulture);

        private static string SizeText(int? size) => size.HasValue ? size.Value.ToString(CultureInfo.InvariantCulture) : "auto";

        /// <summary>Ids from the root down to the element's parent, or "(root)".</summary>
        private static string ParentChain(UIElement e)
        {
            var ids = new List<string>();
            for (UIElement? cur = e.ParentElement; cur != null; cur = cur.ParentElement)
            {
                ids.Insert(0, cur.Id);
            }
            return ids.Count == 0 ? "(root)" : string.Join(" > ", ids);
        }

        /// <summary>The members a style overrides, as <c>name=value</c> pairs.</summary>
        internal static string DescribeStyle(UIStyle s)
        {
            var parts = new List<string>();
            AddIf(parts, "font", s.Font);
            AddIf(parts, "textColor", s.TextColor);
            AddIf(parts, "hoverColor", s.HoverColor);
            if (s.BoxTexture != null)
            {
                parts.Add("boxTexture=" + (string.IsNullOrEmpty(s.BoxTexture.Name) ? "(custom)" : s.BoxTexture.Name));
            }

            AddIf(parts, "boxSource", s.BoxSource);
            AddIf(parts, "boxScale", s.BoxScale);
            AddIf(parts, "padding", s.Padding);
            AddIf(parts, "textShadow", s.TextShadow);
            if (s.ClickSound != null)
            {
                parts.Add("clickSound=" + Quote(s.ClickSound));
            }

            if (s.HoverSound != null)
            {
                parts.Add("hoverSound=" + Quote(s.HoverSound));
            }

            return parts.Count == 0 ? "(empty)" : string.Join(", ", parts);
        }

        private static void AddIf<T>(List<string> parts, string name, T? value) where T : struct
        {
            if (value.HasValue)
            {
                parts.Add(name + "=" + Convert.ToString(value.Value, CultureInfo.InvariantCulture));
            }
        }

        private static string Quote(string s) => "\"" + s + "\"";

        // ---------------------------------------------------------------------------------------------------------
        //  Export
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Write the builder code for <paramref name="menu"/> to the log, the export folder and (when possible) the clipboard.</summary>
        internal static void Export(UIMenu menu)
        {
            string code = TreeExporter.Export(menu);
            UIServices.Log($"Builder code for menu '{menu.Id}' of {menu.Consumer.ModId}:\n{code}", LogLevel.Info);
            string? path = WriteExportFile(menu, code);
            bool clipboard = TryCopyToClipboard(code);
            UIServices.Log($"Export written to {path ?? "the log only"}{(clipboard ? " and copied to the clipboard" : string.Empty)}.", LogLevel.Info);
        }

        private static string? WriteExportFile(UIMenu menu, string code)
        {
            if (ExportDirectory.Length == 0)
            {
                return null;
            }

            try
            {
                Directory.CreateDirectory(ExportDirectory);
                string path = Path.Combine(ExportDirectory, $"{SafeFileName(menu.Consumer.ModId)}-{SafeFileName(menu.Id)}.cs");
                File.WriteAllText(path, code);
                return path;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                UIServices.Log($"Could not write the export file: {ex.Message}", LogLevel.Warn);
                return null;
            }
        }

        /// <summary><c>DesktopClipboard</c> exists on SDV 1.6 but can fail on some platforms; never let that break the export.</summary>
        private static bool TryCopyToClipboard(string code)
        {
            try
            {
                return DesktopClipboard.SetText(code);
            }
            catch (Exception ex)
            {
                UIServices.Log($"Could not copy the export to the clipboard: {ex.Message}", LogLevel.Debug);
                return false;
            }
        }

        private static string SafeFileName(string name)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0)
                {
                    chars[i] = '_';
                }
            }
            return new string(chars);
        }
    }
}
