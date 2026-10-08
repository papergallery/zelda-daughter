using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ZeldaDaughter.Core.Scenes;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// D-22b: the painted ground — one material (Zelda/Toon with _ZD_GROUND) over the ground plane instead of road ribbons and discs on top
    /// (their 2 cm steps got a dotted pen line and read as paper cut-outs). The mask is baked from the config by the core
    /// (<see cref="GroundMask"/>: signed distances to roads, paved zones, water, fields) into Assets/Generated/Ground/&lt;scene&gt;_mask.png; the
    /// shader breaks the edges with world noise (Assets/Art/Ground/ground_noise.png, tools/art/ground_noise.py) and tones grass and sand.
    /// The colours below are the single source (the material is rewritten on every build). A scene with no paths, water, paved or field
    /// zones keeps the flat ground colour of its config.
    /// </summary>
    public static partial class SceneBuilder
    {
        const string GroundDir = "Assets/Generated/Ground";
        const string GroundNoisePath = "Assets/Art/Ground/ground_noise.png";
        const float GroundMaskTexel = 0.4f; // m: 900×500 for the 360×200 m region; the stored distances interpolate, so edges stay smooth

        // Albedo, sRGB. Chosen against the D-22 frames: the light, the warm grade and the paper of the post-effect lift and yellow the ground
        // (a flat #7d8a5c came out ≈ #a39a55), so these sit darker and greener than the concept colours they aim at (f1: grass #6c6d49…#968d5b, road #e1c794).
        static readonly (string Name, string Hex)[] GroundColours =
        {
            ("_GrassDark", "#4b5c3e"), ("_GrassMid", "#627b50"), ("_GrassLight", "#82925c"),
            ("_SandDark", "#9b936b"), ("_SandLight", "#b9ad86"),
            ("_CobbleDark", "#85827a"), ("_CobbleLight", "#9d998d"), // D-22b: less contrast (frame of the square read as a mosaic)
            ("_BankColor", "#4d5844"), ("_FieldColor", "#7a6650"), // field / trodden earth: lighter (the camp read as a dark hole)
            ("_WaterShallow", "#8e968a"), ("_WaterDeep", "#45565a"), // concept env-bridge: ≈ #bbb9ad at the bank, ≈ #46514b deep
        };

        /// <summary>The water ribbons' material of a painted scene (the same mask: depth from the bank); null — flat colour ribbons.</summary>
        static Material _paintedWater;

        /// <summary>Paints the ground of the scene when it has something to paint; true = paths are in the ground (no ribbons).</summary>
        static bool PaintGround(SceneConfig config, Renderer ground, ModelCatalog catalog, IReadOnlyList<ScatterPlacement> placements)
        {
            _paintedWater = null;
            if (!GroundMask.Wanted(config)) return false;
            var mask = GroundMask.Bake(config, GroundMaskTexel);
            mask.BakeSpots(GroundMask.SpotsOf(config, catalog, placements));
            EnsureFolder(GroundDir);
            string maskPath = WriteMask($"{GroundDir}/{config.Name}_mask.png", mask, mask.Rgba);
            string spotsPath = WriteMask($"{GroundDir}/{config.Name}_spots.png", mask, mask.Spots);
            TextureSetup(GroundNoisePath, srgb: false, mips: true, TextureWrapMode.Repeat, compressed: false);

            ground.sharedMaterial = PaintedMaterial($"ground_{config.Name}", "_ZD_GROUND", "_Ground", maskPath, spotsPath, mask);
            _paintedWater = PaintedMaterial($"water_{config.Name}", "_ZD_WATER", "_Water", maskPath, spotsPath, mask);
            Debug.Log($"[ZD:Scene] ground {config.Name} painted mask={mask.Width}x{mask.Height} texel={mask.MetresPerTexel}m");
            return true;
        }

        /// <summary>A layer of the mask as a linear, uncompressed, unfiltered-by-mips PNG; rewritten (and reimported) only when its bytes change.</summary>
        static string WriteMask(string path, GroundMask mask, byte[] rgba)
        {
            var tex = new Texture2D(mask.Width, mask.Height, TextureFormat.RGBA32, false, true);
            tex.SetPixelData(rgba, 0);
            tex.Apply(false);
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(png))
            {
                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            TextureSetup(path, srgb: false, mips: false, TextureWrapMode.Clamp, compressed: false);
            return path;
        }

        static Material PaintedMaterial(string name, string keyword, string toggle, string maskPath, string spotsPath, GroundMask mask)
        {
            string matPath = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            bool created = mat == null;
            if (created) mat = new Material(ModelLook.LoadShader());
            mat.shader = ModelLook.LoadShader();
            ModelLook.Style(mat);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Grade", 0f);
            mat.DisableKeyword("_ZD_GRADE");
            mat.SetFloat(toggle, 1f);
            mat.EnableKeyword(keyword);
            mat.SetTexture("_GroundMask", AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath));
            mat.SetTexture("_GroundMask2", AssetDatabase.LoadAssetAtPath<Texture2D>(spotsPath));
            mat.SetTexture("_GroundNoise", AssetDatabase.LoadAssetAtPath<Texture2D>(GroundNoisePath));
            mat.SetVector("_GroundRect", new Vector4(mask.MinX, mask.MinZ, 1f / (mask.Width * mask.MetresPerTexel), 1f / (mask.Height * mask.MetresPerTexel)));
            foreach (var (n, hex) in GroundColours) mat.SetColor(n, ColorOf(hex));
            if (created) { EnsureFolder(MaterialsDir); AssetDatabase.CreateAsset(mat, matPath); } else EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Import settings of a texture made or used by the builder; reimports only when something changes.</summary>
        static void TextureSetup(string path, bool srgb, bool mips, TextureWrapMode wrap, bool compressed, bool alpha = false)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter imp)) { Debug.LogError($"[ZD:Scene] texture {path} not found"); return; }
            bool dirty = false;
            void Set<T>(T now, T want, System.Action<T> set) { if (!Equals(now, want)) { set(want); dirty = true; } }
            Set(imp.textureType, TextureImporterType.Default, v => imp.textureType = v);
            Set(imp.textureShape, TextureImporterShape.Texture2D, v => imp.textureShape = v); // a 4:1 atlas is otherwise guessed to be a cubemap
            Set(imp.sRGBTexture, srgb, v => imp.sRGBTexture = v);
            Set(imp.mipmapEnabled, mips, v => imp.mipmapEnabled = v);
            Set(imp.wrapMode, wrap, v => imp.wrapMode = v);
            Set(imp.filterMode, FilterMode.Bilinear, v => imp.filterMode = v);
            Set(imp.npotScale, TextureImporterNPOTScale.None, v => imp.npotScale = v);
            Set(imp.alphaSource, TextureImporterAlphaSource.FromInput, v => imp.alphaSource = v);
            Set(imp.alphaIsTransparency, alpha, v => imp.alphaIsTransparency = v);
            Set(imp.isReadable, false, v => imp.isReadable = v);
            Set(imp.textureCompression, compressed ? TextureImporterCompression.Compressed : TextureImporterCompression.Uncompressed, v => imp.textureCompression = v);
            if (alpha && mips)
            {
                // alpha-clipped cards keep their coverage in the smaller mips (grass does not thin out with distance)
                Set(imp.mipMapsPreserveCoverage, true, v => imp.mipMapsPreserveCoverage = v);
                Set(imp.alphaTestReferenceValue, 0.5f, v => imp.alphaTestReferenceValue = v);
            }
            if (dirty) imp.SaveAndReimport();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
