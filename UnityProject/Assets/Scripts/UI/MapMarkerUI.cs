using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ZeldaDaughter.World;

namespace ZeldaDaughter.UI
{
    public class MapMarkerUI : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _label;

        public void Setup(MapMarkerData data)
        {
            if (_icon != null)
            {
                if (data.icon != null)
                {
                    _icon.sprite = data.icon;
                    _icon.gameObject.SetActive(true);
                }
                else
                {
                    _icon.gameObject.SetActive(false);
                }
            }

            if (_label != null)
            {
                if (!string.IsNullOrEmpty(data.label))
                {
                    _label.text = data.label;
                    _label.gameObject.SetActive(true);
                }
                else
                {
                    _label.gameObject.SetActive(false);
                }
            }
        }
    }
}
