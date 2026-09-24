using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace UIFramework.Data.Actions
{
    /// <summary>
    /// Map tile and touch actions: an <c>Action</c> (or <c>TouchAction</c>) tile property
    /// <c>6135.UIFramework_OpenMenu &lt;owner/menu&gt; [force]</c> opens that menu. Also works in <c>Data/Buildings</c>
    /// <c>ActionTiles</c>.
    /// </summary>
    internal static class TileActions
    {
        /// <summary>Register the tile and touch actions (once, from <see cref="ModEntry.Entry"/>).</summary>
        internal static void Register(DataService data, IMonitor monitor)
        {
            GameLocation.RegisterTileAction(FrameworkTriggerActions.OpenMenu, (GameLocation location, string[] args, Farmer who, Point tile) =>
            {
                if (!FrameworkTriggerActions.Open(data, args, out string error))
                {
                    monitor.Log($"Tile action '{string.Join(" ", args)}' at {location.NameOrUniqueName} ({tile.X}, {tile.Y}) failed: {error}", LogLevel.Warn);
                    return false;
                }

                return true;
            });

            GameLocation.RegisterTouchAction(FrameworkTriggerActions.OpenMenu, (GameLocation location, string[] args, Farmer who, Vector2 tile) =>
            {
                if (!FrameworkTriggerActions.Open(data, args, out string error))
                {
                    monitor.Log($"Touch action '{string.Join(" ", args)}' at {location.NameOrUniqueName} ({tile.X}, {tile.Y}) failed: {error}", LogLevel.Warn);
                }
            });
        }
    }
}
