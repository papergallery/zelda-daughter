using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ZeldaDaughter.World;

namespace ZeldaDaughter.Editor
{
    /// <summary>
    /// A phone-shaped frame (1080×2340, portrait) of a scene from its game camera, rendered in the editor — for comparing
    /// with concept frames (K-02, Р2) and for the morning notes. Daylight 0..1 is applied through SunController.
    /// Log: "[ZD:Frame] &lt;scene&gt; daylight=… → &lt;png&gt;".
    /// </summary>
    public static class FrameCapture
    {
        public static string Capture(string scenePath, string pngPath, float daylight, int width = 1080, int height = 2340)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var sun = Object.FindFirstObjectByType<SunController>();
            if (sun != null) sun.Apply(daylight);
            var cam = Camera.main;
            var iso = cam.GetComponent<IsoCamera>();
            if (iso != null) iso.SnapToTarget();
            var rt = new RenderTexture(width, height, 24);
            var prevTarget = cam.targetTexture;
            float prevAspect = cam.aspect;
            cam.targetTexture = rt;
            cam.aspect = (float)width / height;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = prevTarget;
            cam.aspect = prevAspect;
            Directory.CreateDirectory(Path.GetDirectoryName(pngPath));
            File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            // Reopen from disk: the sun change above must not be saved into the scene.
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            string line = $"[ZD:Frame] {Path.GetFileNameWithoutExtension(scenePath)} daylight={daylight:0.00} → {pngPath}";
            Debug.Log(line);
            return line;
        }
    }
}
