using System;

namespace ZeldaDaughter.UI
{
    public enum NotebookCategory
    {
        Quest,
        Recipe,
        Medicine,
        Lore
    }

    [Serializable]
    public class NotebookEntryData
    {
        public NotebookCategory Category;
        public string Text;
        public float GameTime;
    }
}
