using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// D-10: measures every model under Assets/Art/Models (renderer bounds of the imported prefab, metres at import scale)
    /// and writes data/model-bounds.json — the core reads footprints and collider sizes from it. Re-run after changing import
    /// settings or adding models: menu Zelda → Models → Measure bounds. Log: "[ZD:Models] measured N → path".
    /// </summary>
    public static class ModelBoundsDump
    {
        public const string OutPath = "../data/model-bounds.json";

        [MenuItem("Zelda/Models/Measure bounds")]
        public static string Run()
        {
            var paths = AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art/Models" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, System.StringComparer.Ordinal).ToList();
            var sb = new StringBuilder();
            sb.Append("{\n  \"_source\": \"tools: ZeldaDaughter.Editor.ModelBoundsDump (D-10) — границы рендереров импортированных FBX, метры в единицах импорта (до scale из models.json); центр и размер в локальных осях модели. Не править руками.\",\n  \"bounds\": {\n");
            int n = 0;
            foreach (var path in paths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                var go = (GameObject)Object.Instantiate(prefab);
                // The prefab root keeps its own rotation/scale (Quaternius: the mesh node stands the Z-up export up) — do not reset it.
                var rs = go.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) { Object.DestroyImmediate(go); continue; }
                var b = rs[0].bounds;
                foreach (var r in rs.Skip(1)) b.Encapsulate(r.bounds);
                Object.DestroyImmediate(go);
                if (n++ > 0) sb.Append(",\n");
                sb.Append(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "    \"{0}\": {{ \"center\": [{1:0.###}, {2:0.###}, {3:0.###}], \"size\": [{4:0.###}, {5:0.###}, {6:0.###}] }}",
                    path, b.center.x, b.center.y, b.center.z, b.size.x, b.size.y, b.size.z));
            }
            sb.Append("\n  }\n}\n");
            File.WriteAllText(OutPath, sb.ToString().Replace("\r\n", "\n"), new UTF8Encoding(false));
            Debug.Log($"[ZD:Models] measured {n} → {OutPath}");
            return $"measured {n}";
        }
    }
}
