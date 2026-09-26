using StardewModdingAPI;
using UIFramework.Core;

namespace UIFramework
{
    /// <summary>Routes the framework's announcements (<see cref="Accessibility"/>) to Stardew Access when it is installed.</summary>
    internal static class ScreenReader
    {
        private const string StardewAccessId = "shoaib.stardewaccess";

        /// <summary>Connect <see cref="UIServices.Announcer"/> to Stardew Access (nothing happens when it is not installed).</summary>
        internal static void Connect(IModHelper helper, IMonitor monitor)
        {
            IStardewAccessApi? reader = helper.ModRegistry.GetApi<IStardewAccessApi>(StardewAccessId);
            if (reader == null)
            {
                return;
            }

            UIServices.Announcer = text => reader.Say(text, interrupt: true);
            monitor.Log("Stardew Access detected; framework menus will announce titles, focus and value changes.", LogLevel.Info);
        }
    }
}
