using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// D-10: import settings for everything under Assets/Art/Models, set in code so a fresh clone imports the same way.
    /// No animation, cameras, lights; no Unity colliders (the scene builder adds simple ones from the catalog); materials go
    /// through <see cref="ModelLook"/>. Scale is the file's raw units (useFileScale off); the per-model scale lives in data/models.json.
    /// </summary>
    public sealed class ModelImport : AssetPostprocessor
    {
                static bool Ours(string path) => path.StartsWith(ModelLook.ModelsRoot, System.StringComparison.Ordinal);

        public override uint GetVersion() => 10u;

        void OnPreprocessModel()
        {
            if (!Ours(assetPath)) return;
            var m = (ModelImporter)assetImporter;
            m.useFileScale = false;
            m.globalScale = KitScale(assetPath);
            m.importAnimation = false;
            m.animationType = ModelImporterAnimationType.None;
            m.importCameras = false;
            m.importLights = false;
            m.importBlendShapes = false;
            m.importVisibility = false;
            m.addCollider = false;
            m.isReadable = false;
            m.meshCompression = ModelImporterMeshCompression.Off;
            m.generateSecondaryUV = false;
            m.preserveHierarchy = false;
            m.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            m.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }

        /// <summary>
        /// D-21: small ground cover (grass, flowers, mushrooms, flat plants, small bushes) casts no shadow — hundreds of blade shadows were
        /// the "rubbish" of the D-08 frame; the concept draws shadows only for trees, rocks, logs and houses. By file name, one place.
        /// </summary>
        public static bool CastsNoShadow(string path)
        {
            string n = System.IO.Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            return n.StartsWith("grass") || n.StartsWith("flower_") || n.StartsWith("mushroom") || n.StartsWith("plant_flat")
                || n == "plant_bushsmall" || n.StartsWith("crops_");
        }

        void OnPostprocessModel(GameObject root)
        {
            if (!Ours(assetPath) || !CastsNoShadow(assetPath)) return;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>
        /// Raw file units → metres, baked at import so every model in the catalog is real-size (scale 1). Measured (D-10): the
        /// Kenney kits are 10 (Nature) / 100 (Town) units per tile, Quaternius is in centimetres. One tile = 3 m: the hero is a
        /// 2 m capsule, a Town wall is 3 m high with a ~2.1 m door, a Nature river tile is 3 m wide.
        /// </summary>
        public static float KitScale(string path)
        {
            if (path.Contains("/KenneyNature/")) return 0.3f;
            if (path.Contains("/KenneyTown/")) return 0.03f;
            if (path.Contains("/QuaterniusProps/")) return 0.01f;
            return 1f;
        }

        void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] clips)
        {
            if (!Ours(assetPath)) return;
            Color color = Color.white;
            if (description.TryGetProperty("DiffuseColor", out Vector4 v)) color = new Color(v.x, v.y, v.z, 1f);
            Texture map = null;
            if (description.TryGetProperty("DiffuseColor", out TexturePropertyDescription tex) && tex.texture is Texture2D) map = tex.texture;
            if (map == null) map = FromTrimName(material.name);
            ModelLook.Apply(material, color, map);
        }

        /// <summary>Quaternius props reference an author-machine path; the trim sheets sit in Textures/ next to the models: MI_Trim_Metal → T_Trim_Metal_BaseColor.png.</summary>
        Texture FromTrimName(string materialName)
        {
            if (!materialName.StartsWith("MI_Trim_")) return null;
            string dir = System.IO.Path.GetDirectoryName(assetPath).Replace('\\', '/');
            return AssetDatabase.LoadAssetAtPath<Texture>($"{dir}/Textures/T_{materialName.Substring(3)}_BaseColor.png");
        }

        void OnPreprocessTexture()
        {
            if (!Ours(assetPath)) return;
            var t = (TextureImporter)assetImporter;
            t.textureShape = TextureImporterShape.Texture2D; // wide / square sheets are otherwise guessed to be cubemaps
            // Kenney's colormap is a palette of flat cells: point sampling, no mips — no colour bleeding between cells.
            if (assetPath.EndsWith("colormap.png"))
            {
                t.filterMode = FilterMode.Point;
                t.mipmapEnabled = false;
                t.wrapMode = TextureWrapMode.Clamp;
            }
        }
    }
}
