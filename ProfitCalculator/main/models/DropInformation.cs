using StardewValley;
using System;
using System.Collections.Generic;
using System.Linq;
using static ProfitCalculator.Utils;
using SObject = StardewValley.Object;

#nullable enable

namespace ProfitCalculator.main.models
{
    /// <summary>
    /// Handles Drop information for a specific entity
    /// </summary>
    public class DropInformation
    {
        /// <summary>
        /// Represents a single drop item with its quantity, chance, and season.
        /// </summary>
        public class Drop
        {
            public Item Item { get; set; }
            public int Quantity { get; set; }
            public double Chance { get; set; }
            public Season? Season { get; set; }

            /// <summary>
            /// Initializes a new instance of the Drop class.
            /// </summary>
            /// <param name="item">The item to be dropped.</param>
            /// <param name="quantity">The quantity of the item to be dropped.</param>
            /// <param name="chance">The chance of the item being dropped.</param>
            /// <param name="season">The season in which the item can be dropped.</param>
            public Drop(Item item, int quantity, double chance, Season? season)
            {
                Item = item;
                Quantity = quantity;
                Chance = chance;
                Season = season;
            }

            /// <summary>
            /// Initializes a new instance of the Drop class.
            /// </summary>
            /// <param name="item">The item to be dropped.</param>
            /// <param name="quantity">The quantity of the item to be dropped.</param>
            /// <param name="chance">The chance of the item being dropped.</param>
            public Drop(Item item, int quantity, double chance)
            {
                Item = item;
                Quantity = quantity;
                Chance = chance;
                Season = null;
            }

            /// <summary>
            /// Calculates the base price of the item based on the season, without profession bonuses.
            /// </summary>
            /// <param name="season">The current season.</param>
            /// <returns>The price of the item.</returns>
            public int Price(UtilsSeason season) => Price(season, false);

            /// <summary>
            /// Calculates the price of the item based on the season.
            /// </summary>
            /// <param name="season">The current season.</param>
            /// <param name="tiller">Whether to apply the sale profession bonuses (Tiller, Artisan) through <see cref="Utils.ApplySaleBonuses"/>, which skips them when base stats are used.</param>
            /// <returns>The price of the item.</returns>
            public int Price(UtilsSeason season, bool tiller)
            {
                // If the drop has a season and it does not match (outside the greenhouse), return 0.
                if (!IsInSeason(season))
                {
                    return 0;
                }
                // Use the base price from the item data; sellToStorePrice() would already include the current player's profession bonuses.
                int price = Item is SObject obj ? obj.Price : Item.sellToStorePrice();
                return tiller ? ApplySaleBonuses(Item, price) : price;
            }

            /// <summary>
            /// Whether the drop can happen in <paramref name="season"/>: drops without a season always can, and the greenhouse accepts any season.
            /// </summary>
            /// <param name="season">The current season.</param>
            /// <returns>Whether the drop happens in that season.</returns>
            public bool IsInSeason(UtilsSeason season)
            {
                return Season is null || season == UtilsSeason.Greenhouse || Season == Utils.SeasonFromUtilsSeason(season);
            }

            /// <summary>
            /// Whether the drop counts towards a harvest in <paramref name="season"/>: it is in season and has a positive chance and quantity.
            /// </summary>
            /// <param name="season">The current season.</param>
            /// <returns>Whether the drop counts.</returns>
            public bool CountsIn(UtilsSeason season)
            {
                return IsInSeason(season) && Chance > 0 && Quantity > 0;
            }
        }

        /// <summary>
        /// Initializes a new instance of the DropInformation class.
        /// </summary>
        /// <param name="name">Name of the drop, usually the name of the entity.</param>
        /// <param name="items">List of items that can be dropped.</param>
        /// <param name="quantity">List of quantities of the items that can be dropped.</param>
        /// <param name="chances">List of chances of the items that can be dropped.</param>
        public DropInformation(string name, List<Item> items, List<int> quantity, List<double> chances)
        {
            Name = name;
            Drops = new List<Drop>();
            for (int i = 0; i < items.Count; i++)
            {
                Drops.Add(new Drop(items[i], quantity[i], chances[i]));
            }
        }

        /// <summary>
        /// Initializes a new instance of the DropInformation class with default values.
        /// </summary>
        public DropInformation()
        {
            // Initialize empty
            Drops = new();
            Name = "";
        }

        /// <summary> String that names the dropInfo</summary>
        public string Name { get; set; }

        /// <summary>
        /// List of items that can be dropped
        /// </summary>
        public List<Drop> Drops { get; set; }

        /// <summary>
        /// Add an item to the drop
        /// </summary>
        /// <param name="item">Item to add</param>
        /// <param name="quantity">Quantity of the item to add</param>
        /// <param name="chance">Chance for the item to drop</param>
        public void AddItem(Item item, int quantity, double chance)
        {
            Drops.Add(new Drop(item, quantity, chance));
        }

        /// <summary>
        /// Add a range of items to the drop
        /// </summary>
        /// <param name="items">List of items to add</param>
        /// <param name="quantity">List of quantities of the items to add</param>
        /// <param name="chances">List of drop chances of the items to add</param>
        public void AddRange(List<Item> items, List<int> quantity, List<double> chances)
        {
            for (int i = 0; i < items.Count; i++)
            {
                Drops.Add(new Drop(items[i], quantity[i], chances[i]));
            }
        }

        /// <summary>
        /// Remove an item from the drop
        /// </summary>
        /// <param name="item">Item to remove</param>
        public void RemoveItem(Item item)
        {
            Drops.RemoveAll(drop => drop.Item == item);
        }

        /// <summary>
        /// Remove an item from the drop
        /// </summary>
        /// <param name="index">Index of the item to remove</param>
        public void RemoveItem(int index)
        {
            Drops.RemoveAt(index);
        }

        /// <summary>
        /// Clears the lists
        /// </summary>
        public void Clear()
        {
            Drops.Clear();
        }

        /// <summary>
        /// Updates a specific item in all lists. Items and their associated quantity and chance are stored at the same index.
        /// </summary>
        /// <param name="oldItem">Item to update</param>
        /// <param name="quantity">New quantity</param>
        /// <param name="chance">New chance</param>
        /// <param name="newItem">New item to replace the old one, if null, the old item is kept</param>
        public void UpdateItem(Item oldItem, int quantity, double chance, Item? newItem)
        {
            int index = Drops.FindIndex(drop => drop.Item == oldItem);
            Drops[index].Quantity = quantity;
            Drops[index].Chance = chance;
            if (newItem != null)
            {
                Drops[index].Item = newItem;
            }
        }

        /// <summary>
        /// Updates a specific item in all lists. Items and their associated quantity and chance are stored at the same index. Update by index instead of item.
        /// </summary>
        /// <param name="index">Index of the item to update</param>
        /// <param name="quantity">New quantity</param>
        /// <param name="chance">New chance</param>
        public void UpdateItem(int index, int quantity, double chance)
        {
            Drops[index].Quantity = quantity;
            Drops[index].Chance = chance;
        }

        /// <summary>
        /// Calculates the average price of the drops based on the season.
        /// </summary>
        /// <param name="season">The current season.</param>
        /// <returns>The average price of the drops.</returns>
        public double AveragePrice(UtilsSeason season) => AveragePrice(season, false);

        /// <summary>
        /// Calculates the average price of the drops based on the season.
        /// </summary>
        /// <param name="season">The current season.</param>
        /// <param name="tiller">Whether to apply the sale profession bonuses (Tiller, Artisan).</param>
        /// <returns>The average price of the drops.</returns>
        public double AveragePrice(UtilsSeason season, bool tiller)
        {
            return AverageValue(season, drop => drop.Price(season, tiller));
        }

        /// <summary>
        /// Calculates the average value of the drops, weighted by each drop's chance and quantity. Drops out of
        /// <paramref name="season"/>, and drops for which <paramref name="value"/> returns null, count as 0.
        /// </summary>
        /// <param name="season">The current season.</param>
        /// <param name="value">The value of one dropped item, or null when it has none (for example a machine rejects it).</param>
        /// <returns>The average value of the drops.</returns>
        public double AverageValue(UtilsSeason season, Func<Drop, double?> value)
        {
            return Drops.Where(drop => drop.IsInSeason(season)).Sum(drop => (value(drop) ?? 0) * drop.Chance * drop.Quantity);
        }

        /// <summary>
        /// Returns a string representation of the DropInformation.
        /// </summary>
        /// <returns>A string representation of the DropInformation.</returns>
        public override string ToString()
        {
            var dropDetails = Drops.Select(drop => $"Item: {drop.Item.Name}, Quantity: {drop.Quantity}, Chance: {drop.Chance:P}, Price: {drop.Price(UtilsSeason.Greenhouse)}");
            return $"DropInformation: {Name}\nDrops:\n{string.Join("\n", dropDetails)}";
        }
    }
}