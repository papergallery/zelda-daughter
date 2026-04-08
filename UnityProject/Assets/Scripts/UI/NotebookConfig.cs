using UnityEngine;

namespace ZeldaDaughter.UI
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/UI/Notebook Config", fileName = "NotebookConfig")]
    public class NotebookConfig : ScriptableObject
    {
        [SerializeField] private Sprite _questIcon;
        [SerializeField] private Sprite _recipeIcon;
        [SerializeField] private Sprite _medicineIcon;
        [SerializeField] private Sprite _loreIcon;
        [SerializeField] private int _maxEntries = 100;

        public int MaxEntries => _maxEntries;

        /// <summary>Возвращает иконку для категории блокнота.</summary>
        public Sprite GetCategoryIcon(NotebookCategory cat)
        {
            switch (cat)
            {
                case NotebookCategory.Quest:    return _questIcon;
                case NotebookCategory.Recipe:   return _recipeIcon;
                case NotebookCategory.Medicine: return _medicineIcon;
                case NotebookCategory.Lore:     return _loreIcon;
                default:                        return null;
            }
        }
    }
}
