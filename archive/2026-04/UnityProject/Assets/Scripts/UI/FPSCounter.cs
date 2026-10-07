using UnityEngine;
using UnityEngine.UI;

namespace ZeldaDaughter.UI
{
    /// <summary>
    /// Small FPS counter in top-left corner.
    /// </summary>
    public class FPSCounter : MonoBehaviour
    {
        private Text _text;
        private float _deltaTime;

        private void Start()
        {
            var canvasGo = new GameObject("FPSCanvas");
            DontDestroyOnLoad(canvasGo);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 998;
            canvasGo.AddComponent<CanvasScaler>();

            var textGo = new GameObject("FPSText");
            textGo.transform.SetParent(canvasGo.transform, false);
            var rt = textGo.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            // Account for iPhone notch/safe area
            var safeArea = Screen.safeArea;
            float topInset = Screen.height - safeArea.yMax;
            rt.anchoredPosition = new Vector2(safeArea.x + 10, -(topInset + 10));
            rt.sizeDelta = new Vector2(80, 25);

            _text = textGo.AddComponent<Text>();
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _text.fontSize = 14;
            _text.color = new Color(0, 1, 0, 0.8f); // green
            _text.alignment = TextAnchor.UpperLeft;

            var bg = textGo.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.3f);
            bg.raycastTarget = false;
        }

        private void Update()
        {
            _deltaTime += (Time.unscaledDeltaTime - _deltaTime) * 0.1f;
            if (_text != null)
                _text.text = $"{1f / _deltaTime:F0} FPS";
        }
    }
}
