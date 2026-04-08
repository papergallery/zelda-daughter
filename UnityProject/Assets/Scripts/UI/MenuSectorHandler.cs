using UnityEngine;

namespace ZeldaDaughter.UI
{
    /// <summary>
    /// Обрабатывает выбор секторов радиального меню и открывает соответствующие панели.
    /// Сектор 0 (Инвентарь) обрабатывается отдельным компонентом.
    /// </summary>
    public class MenuSectorHandler : MonoBehaviour
    {
        [SerializeField] private MapPanelUI _mapPanel;
        [SerializeField] private NotebookPanelUI _notebookPanel;

        private void OnEnable()
        {
            RadialMenuController.OnSectorSelected += HandleSectorSelected;
        }

        private void OnDisable()
        {
            RadialMenuController.OnSectorSelected -= HandleSectorSelected;
        }

        private void HandleSectorSelected(int index)
        {
            switch (index)
            {
                case 0:
                    // Инвентарь — обрабатывается другим компонентом
                    break;
                case 1:
                    if (_mapPanel != null)
                        _mapPanel.Open();
                    break;
                case 2:
                    if (_notebookPanel != null)
                        _notebookPanel.Open();
                    break;
            }
        }
    }
}
