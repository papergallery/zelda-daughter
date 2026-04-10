using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.Save;

namespace ZeldaDaughter.UI
{
    /// <summary>
    /// Debug button to reset save and restart scene.
    /// Placed in top-right corner of screen.
    /// </summary>
    public class DebugResetButton : MonoBehaviour
    {
        private void Start()
        {
            // Create button UI programmatically
            var canvasGo = new GameObject("DebugResetCanvas");
            DontDestroyOnLoad(canvasGo);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            // Button container — top-right
            var btnGo = new GameObject("ResetButton");
            btnGo.transform.SetParent(canvasGo.transform, false);
            var rt = btnGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            // Account for iPhone notch/safe area
            var safeArea = Screen.safeArea;
            float topInset = Screen.height - safeArea.yMax;
            float rightInset = Screen.width - safeArea.xMax;
            rt.anchoredPosition = new Vector2(-(rightInset + 10), -(topInset + 10));
            rt.sizeDelta = new Vector2(120, 40);

            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.8f, 0.2f, 0.2f, 0.7f);

            var btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(OnResetClicked);

            // Label
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(btnGo.transform, false);
            var labelRt = labelGo.AddComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;
            var text = labelGo.AddComponent<Text>();
            text.text = "RESTART";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
        }

        private void OnResetClicked()
        {
            Debug.Log("[DebugResetButton] Resetting save and reloading scene...");

            if (SaveManager.Instance != null)
                SaveManager.Instance.DeleteSave();

            // Reload current scene
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.name);
        }
    }
}
