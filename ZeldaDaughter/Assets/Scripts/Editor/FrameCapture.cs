using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
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
            => Capture(scenePath, pngPath, daylight, null, 0f, width, height);

        /// <summary>
        /// As above, from a given place: the hero is put at <paramref name="heroAt"/> (the camera follows; the change is not saved)
        /// and the orthographic size can be widened (0 = the scene's own) — frames of the places of a region (D-10).
        /// <paramref name="prepare"/> runs on the opened scene just before the render (the scene is reopened from disk afterwards).
        /// </summary>
        public static string Capture(string scenePath, string pngPath, float daylight, Vector3? heroAt, float orthoSize, int width = 1080, int height = 2340, System.Action<Camera> prepare = null)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("[ZD:Frame] the editor is in Play Mode — stop it first");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new System.InvalidOperationException($"[ZD:Frame] '{SceneManager.GetSceneAt(i).name}' has unsaved changes — save or revert it first");

            string previous = SceneManager.GetActiveScene().path; // reopened at the end; empty for an untitled scene
            RenderTexture rt = null;
            Texture2D tex = null;
            Camera cam = null;
            RenderTexture prevTarget = null;
            float prevAspect = 0f;
            try
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var sun = Object.FindFirstObjectByType<SunController>();
                if (sun != null) sun.Apply(daylight);
                cam = Camera.main;
                if (heroAt.HasValue) Object.FindFirstObjectByType<ZeldaDaughter.Hero.HeroController>().Teleport(heroAt.Value, 0f);
                if (orthoSize > 0f && cam.orthographic) cam.orthographicSize = orthoSize;
                var iso = cam.GetComponent<IsoCamera>();
                if (iso != null) iso.SnapToTarget();
                prepare?.Invoke(cam); // D-08: scene dressing for a reference frame (a fire light, a probe sprite); never saved
                rt = new RenderTexture(width, height, 24);
                prevTarget = cam.targetTexture;
                prevAspect = cam.aspect;
                cam.targetTexture = rt;
                cam.aspect = (float)width / height;
                cam.Render();
                RenderTexture.active = rt;
                tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(pngPath));
                File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = null;
                if (cam != null) { cam.targetTexture = prevTarget; cam.aspect = prevAspect; }
                if (tex != null) Object.DestroyImmediate(tex);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                // Reopen from disk: the sun change above must not be saved into the scene — and the author gets their scene back.
                if (string.IsNullOrEmpty(previous)) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                else EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            }
            string line = $"[ZD:Frame] {Path.GetFileNameWithoutExtension(scenePath)} daylight={daylight:0.00} → {pngPath}";
            Debug.Log(line);
            return line;
        }
    }
}
