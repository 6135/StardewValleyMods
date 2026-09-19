using System;
using System.Collections.Generic;
using StardewModdingAPI;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Hosting
{
    /// <summary>What the player changed about one window (or HUD widget); serialized into the save data.</summary>
    internal sealed class WindowLayout
    {
        /// <summary>Explicit left edge (menus) or horizontal anchor offset (HUD widgets).</summary>
        public int? X { get; set; }

        /// <summary>Explicit top edge (menus) or vertical anchor offset (HUD widgets).</summary>
        public int? Y { get; set; }

        /// <summary>Resized width, or null when untouched.</summary>
        public int? Width { get; set; }

        /// <summary>Resized height, or null when untouched.</summary>
        public int? Height { get; set; }

        /// <summary>Whether the player rolled the window up to its title strip.</summary>
        public bool Collapsed { get; set; }

        /// <summary>Anchor of a menu at capture time (so a reset can restore it).</summary>
        public UIAnchor Anchor { get; set; } = UIAnchor.Center;
    }

    /// <summary>
    /// The player-owned layouts of the current save: one <see cref="WindowLayout"/> per window keyed by
    /// <c>"&lt;consumerModId&gt;/&lt;menuId&gt;"</c> (menus) or <c>"hud:&lt;consumerModId&gt;/&lt;hudId&gt;"</c> (HUD widgets).
    /// Read from the save data on <c>SaveLoaded</c>, written on <c>Saving</c>; farmhands (who cannot write save data)
    /// keep their layouts for the session only.
    /// </summary>
    internal sealed class WindowLayoutStore
    {
        private const string SaveKey = "layout";

        private readonly IDataHelper data;
        private Dictionary<string, WindowLayout> entries = new();

        internal WindowLayoutStore(IDataHelper data)
        {
            this.data = data;
        }

        /// <summary>Key of a menu's entry.</summary>
        internal static string MenuKey(UIMenu menu) => menu.Consumer.ModId + "/" + menu.Id;

        /// <summary>Key of a HUD widget's entry.</summary>
        internal static string HudKey(UIHud hud) => "hud:" + hud.Consumer.ModId + "/" + hud.Id;

        // ---------------------------------------------------------------------------------------------------------
        //  Persistence
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Replace the in-memory layouts with the ones stored in the loaded save (empty for farmhands).</summary>
        internal void Load()
        {
            entries = new Dictionary<string, WindowLayout>();
            if (!Context.IsMainPlayer)
            {
                return;
            }

            try
            {
                entries = data.ReadSaveData<Dictionary<string, WindowLayout>>(SaveKey) ?? new Dictionary<string, WindowLayout>();
            }
            catch (Exception ex)
            {
                UIServices.Log($"Could not read the saved window layouts; starting from defaults.\n{ex}", LogLevel.Warn);
            }
        }

        /// <summary>Write the in-memory layouts into the save being written (main player only).</summary>
        internal void Save()
        {
            if (!Context.IsMainPlayer)
            {
                return;
            }

            try
            {
                data.WriteSaveData(SaveKey, entries.Count == 0 ? null : entries);
            }
            catch (Exception ex)
            {
                UIServices.Log($"Could not write the window layouts to the save.\n{ex}", LogLevel.Warn);
            }
        }

        /// <summary>Forget every layout (the save data is updated on the next save).</summary>
        internal void Clear() => entries.Clear();

        internal int Count => entries.Count;

        internal WindowLayout? Get(string key) => entries.TryGetValue(key, out WindowLayout? layout) ? layout : null;

        internal void Set(string key, WindowLayout layout) => entries[key] = layout;

        internal void Remove(string key) => entries.Remove(key);

        // ---------------------------------------------------------------------------------------------------------
        //  Menus
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Apply the stored layout to a menu that is opening (captures the consumer's own placement first).</summary>
        internal void Apply(UIMenu menu)
        {
            if (!menu.PlayerLayout || !menu.DrawBox)
            {
                return;
            }

            WindowLayout? layout = Get(MenuKey(menu));
            if (layout == null)
            {
                RestoreConsumerPlacement(menu);
                return;
            }

            menu.ConsumerLayout ??= Capture(menu);
            if (layout.X.HasValue && layout.Y.HasValue)
            {
                menu.SetPosition(layout.X.Value, layout.Y.Value);
            }

            if (menu.IsResizable && layout.Width.HasValue && layout.Height.HasValue)
            {
                menu.Width = layout.Width;
                menu.Height = layout.Height;
            }

            menu.Collapsed = layout.Collapsed;
        }

        /// <summary>Record the menu's current placement as the player's choice.</summary>
        internal void Remember(UIMenu menu)
        {
            if (!menu.PlayerLayout)
            {
                return;
            }

            menu.ConsumerLayout ??= Capture(menu);
            WindowLayout layout = new()
            {
                X = menu.Bounds.X,
                Y = menu.Bounds.Y,
                Width = menu.IsResizable ? menu.Width : null,
                Height = menu.IsResizable ? menu.Height : null,
                Collapsed = menu.Collapsed,
                Anchor = UIAnchor.Explicit
            };
            Set(MenuKey(menu), layout);
        }

        /// <summary>Drop the stored layout and put the menu back where the consumer placed it.</summary>
        internal void Reset(UIMenu menu)
        {
            Remove(MenuKey(menu));
            RestoreConsumerPlacement(menu);
        }

        /// <summary>Undo an applied layout: back to the consumer's anchor / position / size, expanded.</summary>
        private static void RestoreConsumerPlacement(UIMenu menu)
        {
            WindowLayout? original = menu.ConsumerLayout;
            menu.ConsumerLayout = null;
            menu.Collapsed = false;
            if (original == null)
            {
                return;
            }

            menu.Anchor = original.Anchor;
            menu.X = original.X ?? 0;
            menu.Y = original.Y ?? 0;
            if (menu.IsResizable)
            {
                menu.Width = original.Width;
                menu.Height = original.Height;
            }
        }

        private static WindowLayout Capture(UIMenu menu) => new()
        {
            Anchor = menu.Anchor,
            X = menu.X,
            Y = menu.Y,
            Width = menu.Width,
            Height = menu.Height,
            Collapsed = false
        };

        // ---------------------------------------------------------------------------------------------------------
        //  HUD widgets
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Apply the stored anchor offset to a HUD widget, or restore the consumer's offset when the save has none.</summary>
        internal void Apply(UIHud hud)
        {
            WindowLayout? layout = Get(HudKey(hud));
            if (layout == null || !layout.X.HasValue || !layout.Y.HasValue)
            {
                RestoreConsumerOffset(hud);
                return;
            }

            hud.ConsumerLayout ??= new WindowLayout { X = hud.X, Y = hud.Y };
            hud.SetOffset(layout.X.Value, layout.Y.Value);
        }

        private static void RestoreConsumerOffset(UIHud hud)
        {
            WindowLayout? original = hud.ConsumerLayout;
            hud.ConsumerLayout = null;
            if (original != null)
            {
                hud.SetOffset(original.X ?? 0, original.Y ?? 0);
            }
        }

        /// <summary>Record a HUD widget's anchor offset as the player's choice.</summary>
        internal void Remember(UIHud hud)
        {
            hud.ConsumerLayout ??= new WindowLayout { X = hud.X, Y = hud.Y };
            Set(HudKey(hud), new WindowLayout { X = hud.X, Y = hud.Y, Anchor = hud.Anchor });
        }
    }
}
