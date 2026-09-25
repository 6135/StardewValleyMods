using StardewModdingAPI;
using StardewValley;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using static ProfitCalculator.Utils;

#nullable enable

namespace ProfitCalculator.main
{
    /// <summary>
    /// The settings a calculation runs with. The main screen edits one instance per split-screen player through data
    /// bindings and pushes it to the <see cref="Calculator"/> when the player calculates. Raises
    /// <see cref="PropertyChanged"/> so the screen refreshes only when a value changes.
    /// </summary>
    public class ProfitCalculatorSettings : INotifyPropertyChanged
    {
        private const uint LowestYears = 1;
        private const uint HighestYears = 10;

        private uint day = 1;
        private UtilsSeason season = UtilsSeason.Spring;
        private string produceType = RawProduceType;
        private FertilizerQuality fertilizerQuality = FertilizerQuality.None;
        private bool payForSeeds = true;
        private bool payForFertilizer;
        private uint maxMoney;
        private bool useBaseStats;
        private bool crossSeason;
        private uint years = LowestYears;
        private bool heavyTapper;
        private bool treeFertilizer;

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary> The day for planting. </summary>
        public uint Day { get => day; set => Set(ref day, value); }

        /// <summary> The amount of days a Season can have. </summary>
        public uint MaxDay => 28;

        /// <summary> The minimum day a Season can have. </summary>
        public uint MinDay => 1;

        /// <summary> The Season for planting. </summary>
        public UtilsSeason Season { get => season; set => Set(ref season, value); }

        /// <summary> The produce type id to calculate with: <see cref="RawProduceType"/>, <see cref="FruitTreesProduceType"/>, <see cref="WildTreesProduceType"/>, a machine's qualified id or a chained aging id. </summary>
        public string ProduceType
        {
            get => produceType;
            set
            {
                if (Set(ref produceType, value))
                {
                    OnPropertyChanged(nameof(IsRaw));
                    OnPropertyChanged(nameof(IsWildTrees));
                }
            }
        }

        /// <summary> Whether <see cref="ProduceType"/> is <see cref="RawProduceType"/> (no tree window, no machine). </summary>
        public bool IsRaw => ProduceType == RawProduceType;

        /// <summary> Whether <see cref="ProduceType"/> is <see cref="WildTreesProduceType"/> (the tapper options apply). </summary>
        public bool IsWildTrees => ProduceType == WildTreesProduceType;

        /// <summary> The quality of fertilizer to use. </summary>
        public FertilizerQuality FertilizerQuality { get => fertilizerQuality; set => Set(ref fertilizerQuality, value); }

        /// <summary> Whether the player wants to check which plants he can purchase with available cash. </summary>
        public bool PayForSeeds { get => payForSeeds; set => Set(ref payForSeeds, value); }

        /// <summary> Whether the player pays for fertilizer. </summary>
        public bool PayForFertilizer { get => payForFertilizer; set => Set(ref payForFertilizer, value); }

        /// <summary> The maximum amount of money the player wants to spend on seeds. </summary>
        public uint MaxMoney { get => maxMoney; set => Set(ref maxMoney, value); }

        /// <summary> Whether the player wants to use base stats or not. </summary>
        public bool UseBaseStats { get => useBaseStats; set => Set(ref useBaseStats, value); }

        /// <summary> Whether a crop keeps growing into the following seasons it can grow in. </summary>
        public bool CrossSeason { get => crossSeason; set => Set(ref crossSeason, value); }

        /// <summary> Lowest value of <see cref="Years"/>. </summary>
        public uint MinYears => LowestYears;

        /// <summary> Highest value of <see cref="Years"/>. </summary>
        public uint MaxYears => HighestYears;

        /// <summary> Number of years (112 days each) trees are simulated over, from <see cref="MinYears"/> to <see cref="MaxYears"/>. </summary>
        public uint Years { get => years; set => Set(ref years, value); }

        /// <summary> Whether wild trees are tapped with a heavy tapper (half the time between products). </summary>
        public bool HeavyTapper { get => heavyTapper; set => Set(ref heavyTapper, value); }

        /// <summary> Whether wild trees are grown with tree fertilizer (faster growth). </summary>
        public bool TreeFertilizer { get => treeFertilizer; set => Set(ref treeFertilizer, value); }

        /// <summary>
        /// Creates the settings, taking the defaults from the loaded save when there is one.
        /// </summary>
        public ProfitCalculatorSettings()
        {
            Reset();
        }

        /// <summary>
        /// Resets every value to its default: day, season and money come from the current save; before a save is
        /// loaded the constant defaults are kept.
        /// </summary>
        public void Reset()
        {
            ProduceType = RawProduceType;
            FertilizerQuality = FertilizerQuality.None;
            PayForSeeds = true;
            PayForFertilizer = false;
            UseBaseStats = false;
            CrossSeason = false;
            Years = LowestYears;
            HeavyTapper = false;
            TreeFertilizer = false;
            if (!Context.IsWorldReady)
            {
                Day = 1;
                Season = UtilsSeason.Spring;
                MaxMoney = 0;
                return;
            }
            Day = (uint)Math.Clamp(Game1.dayOfMonth, (int)MinDay, (int)MaxDay);
            Season = Enum.TryParse(Game1.currentSeason, true, out UtilsSeason current) ? current : UtilsSeason.Spring;
            // the player's own wallet, or the shared one when the farm shares money
            MaxMoney = (uint)Math.Max(0, Game1.player.Money);
        }

        /// <summary>
        /// Pushes these settings to <paramref name="calculator"/>.
        /// </summary>
        /// <param name="calculator"> The calculator to configure. </param>
        public void ApplyTo(Calculator calculator)
        {
            calculator.SetSettings(Day, Season, ProduceType, FertilizerQuality, PayForSeeds, PayForFertilizer, MaxMoney, UseBaseStats, CrossSeason, Math.Clamp(Years, MinYears, MaxYears), HeavyTapper, TreeFertilizer);
        }

        /// <summary> Sets <paramref name="field"/> and raises <see cref="PropertyChanged"/> when the value changes. </summary>
        /// <returns> Whether the value changed. </returns>
        private bool Set<T>(ref T field, T value, [CallerMemberName] string property = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }
            field = value;
            OnPropertyChanged(property);
            return true;
        }

        private void OnPropertyChanged(string property)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        }
    }
}
