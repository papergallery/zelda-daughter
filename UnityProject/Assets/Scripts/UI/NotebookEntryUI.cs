using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZeldaDaughter.UI
{
    public class NotebookEntryUI : MonoBehaviour
    {
        [SerializeField] private Image _categoryIcon;
        [SerializeField] private TextMeshProUGUI _text;

        public void Setup(NotebookEntryData data, NotebookConfig config)
        {
            if (_categoryIcon != null && config != null)
            {
                Sprite icon = config.GetCategoryIcon(data.Category);
                if (icon != null)
                {
                    _categoryIcon.sprite = icon;
                    _categoryIcon.gameObject.SetActive(true);
                }
                else
                {
                    _categoryIcon.gameObject.SetActive(false);
                }
            }

            if (_text != null)
                _text.text = data.Text;
        }
    }
}
