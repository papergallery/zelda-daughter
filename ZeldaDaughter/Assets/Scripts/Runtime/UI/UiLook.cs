using TMPro;
using UnityEngine;

namespace ZeldaDaughter.UI
{
    /// <summary>
    /// How the interface is drawn (Assets/Art/Registries/UiLook.asset, made by RegistryBuilder): the handwritten font with the runic fallback,
    /// the paper plate (9-slice), ink and paper colours, text sizes. Look, not balance. Medieval Kingdom UI (ThirdParty, not in git) replaces
    /// <see cref="Paper"/> here when it is bought — nothing else mentions a plate.
    /// </summary>
    public sealed class UiLook : ScriptableObject
    {
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private Sprite _paper;
        [SerializeField] private Color _ink = new Color(0.20f, 0.14f, 0.09f, 1f);
        [SerializeField] private Color _paperColor = new Color(0.93f, 0.88f, 0.76f, 0.96f);
        [SerializeField] private Color _shade = new Color(0.05f, 0.04f, 0.03f, 0.55f);
        [SerializeField] private float _textSmall = 34f;
        [SerializeField] private float _textNormal = 44f;
        [SerializeField] private float _textBig = 60f;
        [SerializeField] private float _padding = 24f;

        public TMP_FontAsset Font => _font;
        /// <summary>The paper plate sprite with 9-slice borders; null → the plate drawn in code.</summary>
        public Sprite Paper => _paper;
        public Color Ink => _ink;
        public Color PaperColor => _paperColor;
        /// <summary>The dim behind a window.</summary>
        public Color Shade => _shade;
        public float TextSmall => _textSmall;
        public float TextNormal => _textNormal;
        public float TextBig => _textBig;
        public float Padding => _padding;

        public void Configure(TMP_FontAsset font, Sprite paper)
        {
            _font = font;
            _paper = paper;
        }
    }
}
