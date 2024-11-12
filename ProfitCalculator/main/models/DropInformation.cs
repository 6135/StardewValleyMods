using StardewValley;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using static ProfitCalculator.Utils;

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
            public Drop(Item item, int quantity, double chance, Season? season = null)
            {
                Item = item;
                Quantity = quantity;
                Chance = chance;
                Season = season.HasValue ? season : null;
            }

            /// <summary>
            /// Calculates the price of the item based on the season.
            /// </summary>
            /// <param name="season">The current season.</param>
            /// <returns>The price of the item.</returns>
            public int Price(UtilsSeason season)
            {
                // If drop season is null or the season is Greenhouse, return the item's store price.
                if (Season is null || season == UtilsSeason.Greenhouse)
                {
                    return Item.sellToStorePrice();
                }
                // If the season does not match the drop season, return 0.
                if (Season != Utils.SeasonFromUtilsSeason(season))
                {
                    return 0;
                }

                return Item.sellToStorePrice();
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
        public double AveragePrice(UtilsSeason season)
        {
            return Drops.Sum(drop => drop.Price(season) * drop.Chance * drop.Quantity);
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