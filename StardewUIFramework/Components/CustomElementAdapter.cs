using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Components
{
    /// <summary>
    /// Placeholder for the custom-component tier (architecture.md §7, implementation phase 6 — not built yet).
    /// <see cref="StardewUIApi.AddCustom"/> currently throws instead of creating one of these.
    /// </summary>
    internal sealed class CustomElementAdapter : UIElement
    {
        public IUICustomComponent Implementation { get; }

        public CustomElementAdapter(string id, IUICustomComponent implementation) : base(id)
        {
            Implementation = implementation;
        }

        protected override Vector2 MeasureCore(Vector2 available) => Vector2.Zero;

        protected override void DrawCore(SpriteBatch b) { }
    }
}
