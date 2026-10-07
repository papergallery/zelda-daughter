using ZeldaDaughter.World;
using UnityEditor;
using UnityEngine;

namespace ZeldaDaughter.Editor
{
    public static class WaterSetup
    {
        private const string WaterMaterialPath = "Assets/Materials/Water.mat";

        [MenuItem("ZeldaDaughter/MapGen/Setup Water")]
        public static void SetupWater()
        {
            var markers = Object.FindObjectsOfType<WaterAreaMarker>();
            if (markers.Length == 0)
            {
                Debug.LogWarning("[WaterSetup] No WaterAreaMarker found in scene.");
                return;
            }

            var waterMat = GetOrCreateWaterMaterial();
            int processed = 0;

            foreach (var marker in markers)
            {
                SetupMarker(marker, waterMat);
                processed++;
            }

            Debug.Log($"[WaterSetup] Done. Processed {processed} water area(s).");
        }

        /// <summary>
        /// Настраивает воду внутри конкретного региона. Вызывается из RegionPlacer после размещения.
        /// </summary>
        public static void SetupWaterInRegion(GameObject regionRoot)
        {
            var markers = regionRoot.GetComponentsInChildren<WaterAreaMarker>();
            if (markers.Length == 0)
            {
                Debug.Log($"[WaterSetup] No WaterAreaMarker in '{regionRoot.name}', skipping.");
                return;
            }

            var waterMat = GetOrCreateWaterMaterial();
            foreach (var marker in markers)
                SetupMarker(marker, waterMat);

            Debug.Log($"[WaterSetup] Processed {markers.Length} water area(s) in '{regionRoot.name}'.");
        }

        private static void SetupMarker(WaterAreaMarker marker, Material waterMat)
        {
            var markerGO = marker.gameObject;
            float radius = marker.Radius > 0f ? marker.Radius : 3f;
            float depth = marker.Depth > 0f ? marker.Depth : 0.5f;

            // --- Visual plane ---
            Transform existingPlane = markerGO.transform.Find("WaterSurface");
            if (existingPlane == null)
            {
                var planeGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
                planeGO.name = "WaterSurface";
                planeGO.transform.SetParent(markerGO.transform, false);

                // Plane default size is 10 units; scale to diameter
                float planeDiameter = radius * 2f;
                planeGO.transform.localScale = Vector3.one * (planeDiameter / 10f);
                planeGO.transform.localPosition = Vector3.zero;

                // Remove the default MeshCollider Unity adds to plane primitives
                if (planeGO.TryGetComponent<MeshCollider>(out var meshCol))
                    Object.DestroyImmediate(meshCol);

                if (planeGO.TryGetComponent<MeshRenderer>(out var mr))
                    mr.sharedMaterial = waterMat;

                Undo.RegisterCreatedObjectUndo(planeGO, "WaterSetup: visual plane");
            }

            // --- Trigger collider (zone of effect) ---
            if (!markerGO.TryGetComponent<BoxCollider>(out _))
            {
                var box = Undo.AddComponent<BoxCollider>(markerGO);
                box.isTrigger = true;
                box.size = new Vector3(radius * 2f, depth * 2f, radius * 2f);
                box.center = new Vector3(0f, 0f, 0f);
            }

            // --- WaterZone component ---
            if (!markerGO.TryGetComponent<WaterZone>(out _))
                Undo.AddComponent<WaterZone>(markerGO);
        }

        private static Material GetOrCreateWaterMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(WaterMaterialPath);
            if (existing != null) return existing;

            // Find URP/Lit shader
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
                Debug.LogWarning("[WaterSetup] URP/Lit shader not found, falling back to Standard.");
            }

            var mat = new Material(shader);
            mat.name = "Water";

            // Surface type: Transparent
            // URP/Lit uses _Surface = 1 for transparent
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);      // Alpha blend mode
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // Голубой цвет с alpha 0.4
            mat.SetColor("_BaseColor", new Color(0.2f, 0.65f, 1f, 0.4f));

            // Smoothness — имитация воды
            mat.SetFloat("_Smoothness", 0.85f);

            AssetDatabase.CreateAsset(mat, WaterMaterialPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[WaterSetup] Created water material at '{WaterMaterialPath}'.");
            return mat;
        }
    }
}
