using UnityEngine;
using UnityEngine.UI;

namespace ZeldaDaughter.UI
{
    public class RadialMenuSector : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Text _label;
        [SerializeField] private Image _background;
        [SerializeField] private Color _normalColor = new(0.2f, 0.2f, 0.2f, 0.7f);
        [SerializeField] private Color _highlightColor = new(0.8f, 0.6f, 0.2f, 0.9f);

        private int _index;

        public int Index => _index;

        public void Setup(int index, string label, Sprite icon)
        {
            _index = index;
            if (_label != null) _label.text = label;
            if (_icon != null && icon != null) _icon.sprite = icon;
            SetHighlight(false);
        }

        public void SetHighlight(bool highlighted)
        {
            if (_background != null)
                _background.color = highlighted ? _highlightColor : _normalColor;
        }
    }
}
