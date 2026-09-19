using StardewModdingAPI;

namespace ProfitCalculator
{
    /// <summary>
    /// The mod config.
    /// </summary>
    public class ModConfig
    {
        /// <summary> The hotkey to open the calculator. </summary>
        public SButton HotKey { get; set; }

        /// <summary> The delay in frames before the tooltip is shown. </summary>
        public int ToolTipDelay { get; set; }

        /// <summary> Use the UI Framework screens when 6135.UIFramework is installed; otherwise the built-in screens are used. </summary>
        public bool UseUIFramework { get; set; } = true;

        /// <summary>
        ///  Creates a new mod config with default values.
        /// </summary>
        public ModConfig()
        {
            HotKey = SButton.F8;
            ToolTipDelay = 30;
        }
    }
}