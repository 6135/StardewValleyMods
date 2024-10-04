using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ProfitCalculator.main.accessors;
using ProfitCalculator.main.models;
using StardewModdingAPI;
using StardewValley;
using StardewValley.GameData.Crops;
using System;
using System.Collections.Generic;
using System.Linq;
using static ProfitCalculator.Utils;
using SObject = StardewValley.Object;

#nullable enable

namespace ProfitCalculator.main
{
    /// <summary>
    /// Handles Drop information for a specific entity
    /// </summary>
    public class DropInformations
    {
        private string _name;
        private List<Item> _items;
        private List<int> _quantity;
        private List<Double> _chances;
        /// <summary>
        /// Handles Drop information for a specific entity
        /// </summary>
        /// <param name="name"></param> Name of the drop, usually the name of the entity. Unrelated to the name of the items that can be dropped.
        /// <param name="items"></param> List of items that can be dropped
        /// <param name="quantity"></param> List of quantities of the items that can be dropped
        /// <param name="chances"></param> List of chances of the items that can be dropped
        public DropInformations(string name, List<Item> items, List<int> quantity, List<Double> chances)
        {
            _name = name;
            _items = items;
            _quantity = quantity;
            _chances = chances;
        }

        /// <summary>
        /// Name of the drop
        /// </summary>
        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }
        /// <summary>
        /// List of items that can be dropped
        /// </summary>
        public List<Item> Items
        {
            get { return _items; }
            set { _items = value; }
        }
        /// <summary>
        /// List of quantities of the items that can be dropped
        /// </summary>
        public List<int> Quantity
        {
            get { return _quantity; }
            set { _quantity = value; }
        }
        /// <summary>
        /// List of chances of the items that can be dropped
        /// </summary>
        public List<Double> Chances
        {
            get { return _chances; }
            set { _chances = value; }
        }
        /// <summary>
        /// Add an item to the drop
        /// </summary>
        /// <param name="item"></param> Item to add
        /// <param name="quantity"></param> Quantity of the item to add
        /// <param name="chance"></param> Chance for the item to drop
        public void AddItem(Item item, int quantity, Double chance)
        {
            _items.Add(item);
            _quantity.Add(quantity);
            _chances.Add(chance);
        }
        /// <summary>
        /// Add a range of items to the drop
        /// </summary>
        /// <param name="items"></param> List of items to add
        /// <param name="quantity"></param> List of quantities of the items to add
        /// <param name="chances"></param> List of drop chances of the items to add
        public void AddRange(List<Item> items, List<int> quantity, List<Double> chances)
        {
            _items.AddRange(items);
            _quantity.AddRange(quantity);
            _chances.AddRange(chances);

        }
        /// <summary>
        /// Remove an item from the drop
        /// </summary>
        /// <param name="item"></param> Item to remove
        public void RemoveItem(Item item)
        {
            int index = _items.IndexOf(item);
            _items.RemoveAt(index);
            _quantity.RemoveAt(index);
            _chances.RemoveAt(index);
        }
        /// <summary>
        /// Remove an item from the drop
        /// </summary>
        /// <param name="index"></param> Index of the item to remove
        public void RemoveItem(int index)
        {
            _items.RemoveAt(index);
            _quantity.RemoveAt(index);
            _chances.RemoveAt(index);
        }
        /// <summary>
        /// Clears the lists
        /// </summary>
        public void Clear()
        {
            _items.Clear();
            _quantity.Clear();
            _chances.Clear();
        }
        /// <summary>
        /// Updates a specific item in all lists. Items and their associated quantity and chance are stored at the same index.
        /// </summary>
        /// <param name="item"></param> Item to update
        /// <param name="quantity"></param> New quantity
        /// <param name="chance"></param> New chance
        /// <param name="newItem"></param> New item to replace the old one, if null, the old item is kept
        public void UpdateItem(Item oldItem, int quantity, Double chance, Item? newItem)
        {
            int index = _items.IndexOf(oldItem);
            _quantity[index] = quantity;
            _chances[index] = chance;
            if (newItem != null)
            {
                _items[index] = newItem;
            }

        }
        /// <summary>
        /// Updates a specific item in all lists. Items and their associated quantity and chance are stored at the same index. Update by index instead of item.
        /// </summary>
        /// <param name="index"></param> Index of the item to update
        /// <param name="quantity"></param> New quantity
        /// <param name="chance"></param> New chance
        public void UpdateItem(int index, int quantity, Double chance)
        {
            _quantity[index] = quantity;
            _chances[index] = chance;
        }



    }
}