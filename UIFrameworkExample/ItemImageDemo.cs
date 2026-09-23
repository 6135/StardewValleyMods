using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using UIFramework.Api;
using SObject = StardewValley.Object;

namespace UIFrameworkExample
{
    /// <summary>
    /// Test screen for the v1.2 item elements (<see cref="IStardewUIApi.AddItemImage"/> and
    /// <see cref="IUITooltip.ItemInstance"/>): press F7 or run <c>ui_items</c>. It shows
    /// <list type="bullet">
    /// <item>flavored goods drawn by id (<c>AddImage</c>, no tint) next to the same instance drawn with <c>AddItemImage</c>;</item>
    /// <item>every <see cref="UIItemStack"/> overlay at scales 1 to 4, each framed by a boxed panel (the inspector, F10, outlines the exact bounds);</item>
    /// <item>drop shadow, alpha, tint, disabled, an explicit (non-square) size and an item that changes every second;</item>
    /// <item>a tooltip with <c>ItemInstance</c> rows next to a plain <c>Item(id)</c> row.</item>
    /// </list>
    /// Items are created on first draw, so the menu can be built at game launch.
    /// </summary>
    internal sealed class ItemImageDemo
    {
        private static readonly float[] Scales = { 1f, 2f, 3f, 4f };

        /// <summary>Room between the box frame and the item, so the frame surrounds the item instead of covering it (it is thicker than a scale 1 item).</summary>
        private const int BoxPadding = 8;
        private static readonly UIItemStack[] StackModes = { UIItemStack.Hide, UIItemStack.Quality, UIItemStack.NumberAndQuality };

        private readonly IStardewUIApi api;

        // flavored products: (label, base id drawn untinted for comparison, instance); instances are created lazily
        private readonly (string Label, string BaseId, Lazy<Item> Item)[] products =
        {
            ("Starfruit Wine", "(O)348", new Lazy<Item>(() => Flavored(d => d.CreateFlavoredWine, "(O)268"))),
            ("Blueberry Jelly", "(O)344", new Lazy<Item>(() => Flavored(d => d.CreateFlavoredJelly, "(O)258"))),
            ("Pickled Potato", "(O)342", new Lazy<Item>(() => Flavored(d => d.CreateFlavoredPickle, "(O)192"))),
            ("Dried Starfruit", "(O)DriedFruit", new Lazy<Item>(() => Flavored(d => d.CreateFlavoredDriedFruit, "(O)268")))
        };

        /// <summary>A stack of 12 gold-quality parsnips: shows the stack number and the quality star.</summary>
        private readonly Lazy<Item> goldParsnips = new(() => Create("(O)24", 12, SObject.highQuality));

        /// <summary>A single iridium-quality parsnip: the star shows, the number doesn't (stack of 1).</summary>
        private readonly Lazy<Item> iridiumParsnip = new(() => Create("(O)24", 1, SObject.bestQuality));

        internal IUIMenu Menu { get; }

        internal ItemImageDemo(IStardewUIApi api)
        {
            this.api = api;

            IUIMenuOptions options = api.CreateMenuOptions();
            options.Title = () => "Item images (v1.2)";
            options.Width = 760;
            Menu = api.CreateMenu("items", options);

            BuildTintSection(Menu.Root);
            Divider(Menu.Root, "tint.divider");
            BuildOverlaySection(Menu.Root);
            Divider(Menu.Root, "overlay.divider");
            BuildOptionsSection(Menu.Root);
            Divider(Menu.Root, "options.divider");
            BuildTooltipSection(Menu.Root);
        }

        // -------------------------------------------------------------------------------------------------------------
        //  Sections
        // -------------------------------------------------------------------------------------------------------------

        /// <summary>One row per product: its name, the sprite by id (untinted) and the instance (tinted).</summary>
        private void BuildTintSection(IUIContainer parent)
        {
            Heading(parent, "tint.heading", "Tint: AddImage by id (left) vs AddItemImage (right)");
            IUIGrid grid = api.AddGrid(parent, "tint.grid", "auto,auto,auto", Rows(products.Length));
            grid.ColumnSpacing = 24;
            grid.RowSpacing = 8;
            for (int i = 0; i < products.Length; i++)
            {
                (string label, string baseId, Lazy<Item> item) = products[i];
                Place(api.AddLabel(grid, $"tint.{i}.name", () => label), i, 0);

                // the "before": the id alone loses the flavor color, just like the tooltip's Item(id) row
                ParsedItemData data = ItemRegistry.GetDataOrErrorItem(baseId);
                Place(api.AddImage(grid, $"tint.{i}.byId", data.GetTexture(), data.GetSourceRect(), 3f), i, 1);

                IUIItemImage instance = api.AddItemImage(grid, $"tint.{i}.instance", () => item.Value, 3f);
                instance.RichTooltip = api.CreateTooltip().ItemInstance(() => item.Value);
                Place(instance, i, 2);
            }
        }

        /// <summary>
        /// Every overlay mode at every scale for a stack of gold parsnips, plus the single iridium parsnip at scale 4.
        /// Each item is framed by a padded box; use the inspector (F10) to see the exact element bounds.
        /// </summary>
        private void BuildOverlaySection(IUIContainer parent)
        {
            Heading(parent, "overlay.heading", "Overlays: 12 gold parsnips at scales 1-4 (F10 inspector shows the bounds)");
            IUIGrid grid = api.AddGrid(parent, "overlay.grid", "auto,auto,auto,auto", Rows(Scales.Length + 2));
            grid.ColumnSpacing = 24;
            grid.RowSpacing = 8;

            for (int c = 0; c < StackModes.Length; c++)
            {
                UIItemStack mode = StackModes[c];
                Place(api.AddLabel(grid, $"overlay.head.{c}", () => mode.ToString()), 0, c + 1);
            }

            for (int r = 0; r < Scales.Length; r++)
            {
                float scale = Scales[r];
                Place(api.AddLabel(grid, $"overlay.scale.{r}", () => $"x{scale:0}"), r + 1, 0);
                for (int c = 0; c < StackModes.Length; c++)
                {
                    IUIPanel box = api.AddPanel(grid, $"overlay.{r}.{c}.box", true, BoxPadding);
                    IUIItemImage image = api.AddItemImage(box, $"overlay.{r}.{c}.item", () => goldParsnips.Value, scale);
                    image.Stack = StackModes[c];
                    Place(box, r + 1, c + 1);
                }
            }

            int last = Scales.Length + 1;
            Place(api.AddLabel(grid, "overlay.iridium.label", () => "1 iridium, x4"), last, 0);
            for (int c = 0; c < StackModes.Length; c++)
            {
                IUIPanel box = api.AddPanel(grid, $"overlay.iridium.{c}.box", true, BoxPadding);
                IUIItemImage image = api.AddItemImage(box, $"overlay.iridium.{c}.item", () => iridiumParsnip.Value, 4f);
                image.Stack = StackModes[c];
                Place(box, last, c + 1);
            }
        }

        /// <summary>The remaining <see cref="IUIItemImage"/> options, each labelled, all at scale 3.</summary>
        private void BuildOptionsSection(IUIContainer parent)
        {
            Heading(parent, "options.heading", "Options (scale 3)");
            Item Wine() => products[0].Item.Value;
            var cases = new List<(string Label, Action<IUIItemImage> Setup, Func<Item> Item)>
            {
                ("default", _ => { }, Wine),
                ("shadow", i => i.DrawShadow = true, Wine),
                ("alpha 0.5", i => i.Alpha = 0.5f, Wine),
                ("tint red", i => i.Tint = Color.Red, Wine),
                ("disabled", i => i.Enabled = false, Wine),
                ("96 x 48 box", i => { i.Width = 96; i.Height = 48; }, Wine),
                // Func<Item> is read every frame: switch product each second
                ("changes 1/s", _ => { }, () => products[(int)(Game1.currentGameTime?.TotalGameTime.TotalSeconds ?? 0) % products.Length].Item.Value)
            };

            // four cases per line (item row + label row) so the grid fits the menu width
            const int perLine = 4;
            int lines = (cases.Count + perLine - 1) / perLine;
            IUIGrid grid = api.AddGrid(parent, "options.grid", Columns(perLine), Rows(lines * 2));
            grid.ColumnSpacing = 24;
            grid.RowSpacing = 4;
            for (int c = 0; c < cases.Count; c++)
            {
                (string label, Action<IUIItemImage> setup, Func<Item> item) = cases[c];
                int row = (c / perLine) * 2;
                int column = c % perLine;
                IUIPanel box = api.AddPanel(grid, $"options.{c}.box", true, BoxPadding);
                IUIItemImage image = api.AddItemImage(box, $"options.{c}.item", item, 3f);
                setup(image);
                Place(box, row, column);
                Place(api.AddLabel(grid, $"options.{c}.label", () => label), row + 1, column);
            }
        }

        /// <summary>A hover target whose tooltip mixes ItemInstance rows (tinted) with a plain Item(id) row.</summary>
        private void BuildTooltipSection(IUIContainer parent)
        {
            IUILabel target = api.AddLabel(parent, "tooltip.target", () => "Hover here: tooltip with ItemInstance rows");
            IUITooltip tip = api.CreateTooltip().Title(() => "ItemInstance rows");
            foreach ((string _, string _, Lazy<Item> item) in products)
            {
                tip.ItemInstance(() => item.Value);
            }
            tip.Divider()
               .Line(() => "Item(\"(O)348\") for comparison (no tint):")
               .Item("(O)348")
               .Divider()
               .Line(() => "A null getter skips its row:")
               .ItemInstance(() => null!);
            target.RichTooltip = tip.MaxWidth(420);
        }

        // -------------------------------------------------------------------------------------------------------------
        //  Helpers
        // -------------------------------------------------------------------------------------------------------------

        private void Heading(IUIContainer parent, string id, string text)
        {
            IUILabel label = api.AddLabel(parent, id, () => text);
            label.Font = UIFont.Dialogue;
            // long headings continue on the next line instead of being cut off with an ellipsis
            label.Wrap = true;
        }

        private void Divider(IUIContainer parent, string id)
        {
            IUISpacer spacer = api.AddSpacer(parent, id, 0, 8);
            spacer.Line = true;
        }

        private static void Place(IUIElement element, int row, int column)
        {
            element.Row = row;
            element.Column = column;
            element.VerticalAlign = UIAlign.Center;
        }

        private static string Rows(int count) => string.Join(",", Repeat("auto", count));

        private static string Columns(int count) => string.Join(",", Repeat("auto", count));

        private static IEnumerable<string> Repeat(string value, int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return value;
            }
        }

        private static Item Flavored(Func<ObjectDataDefinition, Func<SObject, SObject>> factory, string ingredientId)
        {
            SObject ingredient = ItemRegistry.Create<SObject>(ingredientId);
            return factory(ItemRegistry.GetObjectTypeDefinition())(ingredient);
        }

        private static Item Create(string id, int stack, int quality)
        {
            Item item = ItemRegistry.Create(id, stack, quality);
            return item;
        }
    }
}
