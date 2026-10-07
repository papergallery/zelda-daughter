using UnityEngine;

namespace ZeldaDaughter.UI
{
    /// <summary>
    /// UI панель карты. Заглушка — будет реализована в этапе 7.
    /// </summary>
    public class MapPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject _panel;

        public void Open()
        {
            if (_panel != null)
                _panel.SetActive(true);
        }

        public void Close()
        {
            if (_panel != null)
                _panel.SetActive(false);
        }
    }
}
