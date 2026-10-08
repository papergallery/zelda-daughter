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

        public override uint GetVersion() => 1u;

        void OnPreprocessModel()
        {
            if (!Ours(assetPath)) return;
            var m = (ModelImporter)assetImporter;
            m.useFileScale = false;
            m.globalScale = 1f;
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

        void OnPreprocessMaterialDescription(MaterialDescription description, Material material, AnimationClip[] clips)
        {
            if (!Ours(assetPath)) return;
            Color color = Color.white;
            if (description.TryGetProperty("DiffuseColor", out Vector4 v)) color = new Color(v.x, v.y, v.z, 1f);
            Texture map = null;
            if (description.TryGetProperty("DiffuseColor", out TexturePropertyDescription tex) && tex.texture != null) map = tex.texture;
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
            // Kenney's colormap is a palette of flat cells: point sampling, no mips — no colour bleeding between cells.
            if (!Ours(assetPath) || !assetPath.EndsWith("colormap.png")) return;
            var t = (TextureImporter)assetImporter;
            t.filterMode = FilterMode.Point;
            t.mipmapEnabled = false;
            t.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
