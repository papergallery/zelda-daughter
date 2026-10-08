using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using ZeldaDaughter.UI;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// D-14: the radial menu, the bag, the item drag and the camp presenter (docs/demo/unity-architecture.md §5). One object «Items» under Game; the models of
    /// things lying in the world come from the model catalog (data/models.json) and are referenced from the presenter, so a build has them. Ids in
    /// <c>ctx.ById</c>: <c>radial_menu</c>, <c>inventory_window</c>, <c>item_drag</c>, <c>camp</c>.
    /// </summary>
    public static partial class SceneBuilder
    {
        /// <summary>Item → (model id, scale) for what lies on the ground; an item without a row lies as a small box.</summary>
        static readonly (string item, string model, float scale)[] PlacedModels =
        {
            ("firewood", "log", 0.7f),
            ("planks", "log_stack", 0.5f),
            ("stick", "log", 0.35f),
            ("short_stick", "log", 0.25f),
            ("sharpened_stick", "log", 0.35f),
            ("torch", "log", 0.3f),
            ("torch_unlit", "log", 0.3f),
            ("stone", "stone_small_a", 1f),
            ("flint", "stone_small_flat_a", 1f),
            ("ore", "stone_tall_g", 0.35f),
            ("metal", "stone_small_b", 1f),
        };

        static partial void AddItemsUi(BuildContext ctx)
        {
            var go = new GameObject("Items");
            go.transform.SetParent(ctx.Game.transform, false);
            ctx.ById["items"] = go;

            var zones = Object.FindFirstObjectByType<TerrainZones>();
            var drag = go.AddComponent<ItemDrag>();
            var inventory = go.AddComponent<InventoryWindow>();
            var radial = go.AddComponent<RadialMenu>();
            var camp = go.AddComponent<CampPresenter>();

            drag.Configure(ctx.Session, ctx.UI, ctx.Windows, ctx.HeroCtl, ctx.Picker, inventory, zones, ctx.Art.ItemIcons);
            inventory.Configure(ctx.Session, ctx.UI, ctx.Windows, drag, ctx.Art.ItemIcons);
            radial.Configure(ctx.Session, ctx.UI, ctx.HeroCtl, inventory, ctx.Windows, ctx.UI.TalkIcons);

            var ids = new List<string>();
            var models = new List<GameObject>();
            var scales = new List<float>();
            foreach (var (item, model, scale) in PlacedModels)
            {
                var prefab = ModelPrefab(ctx, model);
                if (prefab == null) continue;
                ids.Add(item);
                models.Add(prefab);
                scales.Add(scale);
            }
            camp.Configure(ctx.Session, ctx.Art, ids.ToArray(), models.ToArray(), scales.ToArray(),
                ModelPrefab(ctx, "campfire_logs"), MaterialFor("#b59d6e"), MaterialFor("#ff9a3c"));

            ctx.ById["item_drag"] = go;
            ctx.ById["inventory_window"] = go;
            ctx.ById["radial_menu"] = go;
            ctx.ById["camp"] = go;
            Debug.Log($"[ZD:Scene] items ui: radial, bag, drag, camp (models {ids.Count}/{PlacedModels.Length})");
        }

        static GameObject ModelPrefab(BuildContext ctx, string modelId)
        {
            if (!ctx.Catalog.Models.TryGetValue(modelId, out var def) || def.IsComposite) { Debug.LogWarning($"[ZD:Scene] model {modelId} is not in the catalog"); return null; }
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(def.Path);
            if (prefab == null) Debug.LogWarning($"[ZD:Scene] model {modelId}: no asset at {def.Path}");
            return prefab;
        }
    }
}
