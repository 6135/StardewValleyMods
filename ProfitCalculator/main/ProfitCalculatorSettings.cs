using StardewModdingAPI;
using StardewValley;
using System;
using static ProfitCalculator.Utils;

#nullable enable

namespace ProfitCalculator.main
{
    /// <summary>
    /// The settings a calculation runs with (see <see cref="Calculator.SetSettings(ProfitCalculatorSettings)"/>). The UI Framework
    /// main screen edits an instance of it through delegates; the legacy <see cref="ui.menus.ProfitCalculatorMainMenu"/> mirrors
    /// the same properties and builds one when it calculates.
    /// </summary>
    public class ProfitCalculatorSettings
    {
        /// <summary> The day for planting. </summary>
        public uint Day { get; set; } = 1;

        /// <summary> The amount of days a Season can have. </summary>
        public uint MaxDay { get; set; } = 28;

        /// <summary> The minimum day a Season can have. </summary>
        public uint MinDay { get; set; } = 1;

        /// <summary> The Season for planting. </summary>
        public UtilsSeason Season { get; set; } = UtilsSeason.Spring;

        /// <summary> The type of produce to calculate with, for now only raw works. </summary>
        public ProduceType ProduceType { get; set; } = ProduceType.Raw;

        /// <summary> The quality of fertilizer to use. </summary>
        public FertilizerQuality FertilizerQuality { get; set; } = FertilizerQuality.None;

        /// <summary> Whether the player wants to check which plants he can purchase with available cash. </summary>
        public bool PayForSeeds { get; set; } = true;

        /// <summary> Whether the player pays for fertilizer. </summary>
        public bool PayForFertilizer { get; set; }

        /// <summary> The maximum amount of money the player wants to spend on seeds. </summary>
        public uint MaxMoney { get; set; }

        /// <summary> Whether the player wants to use base stats or not. </summary>
        public bool UseBaseStats { get; set; }

        /// <summary>
        /// Creates the settings, taking the defaults from the loaded save when there is one.
        /// </summary>
        public ProfitCalculatorSettings()
        {
            Reset();
        }

        /// <summary>
        /// Resets every value to its default (the same defaults as the legacy main menu): day, season and money come
        /// from the current save; before a save is loaded the constant defaults are kept.
        /// </summary>
        public void Reset()
        {
            ProduceType = ProduceType.Raw;
            FertilizerQuality = FertilizerQuality.None;
            PayForSeeds = true;
            PayForFertilizer = false;
            UseBaseStats = false;
            if (!Context.IsWorldReady)
            {
                Day = 1;
                Season = UtilsSeason.Spring;
                MaxMoney = 0;
                return;
            }
            Day = (uint)Math.Clamp(Game1.dayOfMonth, (int)MinDay, (int)MaxDay);
            Season = Enum.TryParse(Game1.currentSeason, true, out UtilsSeason season) ? season : UtilsSeason.Spring;
            MaxMoney = (uint)Math.Max(0, Game1.player.team.money.Value);
        }
    }
}
