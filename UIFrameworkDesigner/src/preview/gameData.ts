// Display names of common vanilla objects for the preview's itemName() (GameFunctions.cs resolves them from
// ItemRegistry in game). Written by hand from the vanilla Data/Objects (English); only the usual crops, forage, fish,
// minerals, artisan goods and resources, so the preview reads naturally. Any other id resolves to the id itself.
const objectNames: Record<string, string> = {
  16: 'Wild Horseradish', 18: 'Daffodil', 20: 'Leek', 22: 'Dandelion', 24: 'Parsnip',
  60: 'Emerald', 62: 'Aquamarine', 64: 'Ruby', 66: 'Amethyst', 68: 'Topaz', 70: 'Jade', 72: 'Diamond', 74: 'Prismatic Shard',
  78: 'Cave Carrot', 80: 'Quartz', 82: 'Fire Quartz', 84: 'Frozen Tear', 86: 'Earth Crystal', 88: 'Coconut', 90: 'Cactus Fruit',
  91: 'Banana', 92: 'Sap', 107: 'Dinosaur Egg',
  128: 'Pufferfish', 129: 'Anchovy', 130: 'Tuna', 131: 'Sardine', 132: 'Bream', 136: 'Largemouth Bass', 137: 'Smallmouth Bass',
  138: 'Rainbow Trout', 139: 'Salmon', 140: 'Walleye', 141: 'Perch', 142: 'Carp', 143: 'Catfish', 144: 'Pike', 145: 'Sunfish',
  147: 'Herring', 148: 'Eel', 150: 'Red Snapper', 152: 'Seaweed', 153: 'Green Algae', 154: 'Sea Cucumber', 157: 'White Algae',
  167: 'Joja Cola', 168: 'Trash', 172: 'Soggy Newspaper',
  174: 'Large Egg', 176: 'Egg', 180: 'Egg', 182: 'Large Egg', 184: 'Milk', 186: 'Large Milk',
  188: 'Green Bean', 190: 'Cauliflower', 192: 'Potato', 194: 'Fried Egg', 196: 'Salad', 216: 'Bread',
  248: 'Garlic', 250: 'Kale', 252: 'Rhubarb', 254: 'Melon', 256: 'Tomato', 257: 'Morel', 258: 'Blueberry', 259: 'Fiddlehead Fern',
  260: 'Hot Pepper', 262: 'Wheat', 264: 'Radish', 266: 'Red Cabbage', 268: 'Starfruit', 270: 'Corn', 272: 'Eggplant',
  274: 'Artichoke', 276: 'Pumpkin', 278: 'Bok Choy', 280: 'Yam', 281: 'Chanterelle', 282: 'Cranberries', 283: 'Holly', 284: 'Beet',
  296: 'Salmonberry', 300: 'Amaranth', 303: 'Pale Ale', 304: 'Hops', 305: 'Void Egg', 306: 'Mayonnaise', 307: 'Duck Mayonnaise',
  308: 'Void Mayonnaise', 330: 'Clay', 334: 'Copper Bar', 335: 'Iron Bar', 336: 'Gold Bar', 337: 'Iridium Bar',
  338: 'Refined Quartz', 340: 'Honey', 342: 'Pickles', 344: 'Jelly', 346: 'Beer', 347: 'Rare Seed', 348: 'Wine', 350: 'Juice',
  376: 'Poppy', 378: 'Copper Ore', 380: 'Iron Ore', 382: 'Coal', 384: 'Gold Ore', 386: 'Iridium Ore', 388: 'Wood', 390: 'Stone',
  392: 'Nautilus Shell', 393: 'Coral', 394: 'Rainbow Shell', 395: 'Coffee', 396: 'Spice Berry', 398: 'Grape', 400: 'Strawberry',
  402: 'Sweet Pea', 404: 'Common Mushroom', 406: 'Wild Plum', 408: 'Hazelnut', 410: 'Blackberry', 412: 'Winter Root',
  414: 'Crystal Fruit', 416: 'Snow Yam', 417: 'Sweet Gem Berry', 418: 'Crocus', 420: 'Red Mushroom', 421: 'Sunflower',
  422: 'Purple Mushroom', 424: 'Cheese', 426: 'Goat Cheese', 428: 'Cloth', 430: 'Truffle', 432: 'Truffle Oil', 433: 'Coffee Bean',
  436: 'Goat Milk', 438: 'L. Goat Milk', 440: 'Wool', 442: 'Duck Egg', 444: 'Duck Feather', 446: "Rabbit's Foot",
  454: 'Ancient Fruit', 459: 'Mead', 472: 'Parsnip Seeds', 499: 'Ancient Seeds',
  591: 'Tulip', 593: 'Summer Spangle', 595: 'Fairy Rose', 597: 'Blue Jazz', 613: 'Apple', 614: 'Green Tea',
  634: 'Apricot', 635: 'Orange', 636: 'Peach', 637: 'Pomegranate', 638: 'Cherry',
  724: 'Maple Syrup', 725: 'Oak Resin', 726: 'Pine Tar', 766: 'Slime', 767: 'Bat Wing', 768: 'Solar Essence', 769: 'Void Essence',
  771: 'Fiber', 787: 'Battery Pack', 807: 'Dinosaur Mayonnaise', 815: 'Tea Leaves', 829: 'Ginger', 830: 'Taro Root',
  832: 'Pineapple', 834: 'Mango', 851: 'Magma Cap', 852: 'Dragon Tooth'
};

/** The object sprite index of a vanilla object id: `(O)24` or the unqualified `24`; undefined for anything else. */
export function objectIndex(id: string): number | undefined {
  const m = /^(?:\(O\))?(\d+)$/i.exec(id.trim());
  return m ? Number(m[1]) : undefined;
}

/** itemName(): the English display name of a common vanilla object, else the id itself. */
export function itemDisplayName(id: string): string {
  const index = objectIndex(id);
  return (index !== undefined ? objectNames[index] : undefined) ?? id.trim();
}
