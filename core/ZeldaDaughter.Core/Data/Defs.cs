using System.Collections.Generic;

namespace ZeldaDaughter.Core.Data
{
    /// <summary>data/items.json. Weight is hidden from the player (§7).</summary>
    public sealed class ItemDef
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        /// <summary>material | tool | weapon | food | medicine.</summary>
        public string Kind { get; set; } = "";
        public float Weight { get; set; }
        public int Stack { get; set; } = 1;
        /// <summary>The hero's line on pick-up.</summary>
        public string Pickup { get; set; } = "";
        public bool Placeable { get; set; }
    }

    /// <summary>Field craft: drag one item onto another, in any order (§7).</summary>
    public sealed class FieldRecipe
    {
        public string A { get; set; } = "";
        public string B { get; set; } = "";
        public string Out { get; set; } = "";
        public int Count { get; set; } = 1;
        /// <summary>Tools that stay in the inventory (knife, axe).</summary>
        public List<string> Keep { get; set; } = new List<string>();
    }

    /// <summary>Station craft: smelter, anvil (§7: weapons only on the anvil).</summary>
    public sealed class StationRecipe
    {
        public string Station { get; set; } = "";
        public List<string> In { get; set; } = new List<string>();
        public string Out { get; set; } = "";
        public int Count { get; set; } = 1;
    }

    /// <summary>World chain: bring an item to a world object (§7 «Логические цепочки»).</summary>
    public sealed class WorldRecipe
    {
        public string Target { get; set; } = "";
        public string With { get; set; } = "";
        public string Result { get; set; } = "";
        public List<string> Keep { get; set; } = new List<string>();
    }

    sealed class ItemsFile { public List<ItemDef> Items { get; set; } = new List<ItemDef>(); }

    sealed class RecipesFile
    {
        public List<FieldRecipe> Field { get; set; } = new List<FieldRecipe>();
        public List<StationRecipe> Station { get; set; } = new List<StationRecipe>();
        public List<WorldRecipe> World { get; set; } = new List<WorldRecipe>();
    }
}
