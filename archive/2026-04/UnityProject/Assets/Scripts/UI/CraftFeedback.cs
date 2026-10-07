using UnityEngine;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.UI
{
    public class CraftFeedback : MonoBehaviour
    {
        [SerializeField] private string[] _failReplies = new[]
        {
            "Это не сработает...",
            "Нет, не подходит.",
            "Бесполезно..."
        };

        [SerializeField] private string[] _successReplies = new[]
        {
            "Получилось!",
            "Отлично!",
            "Сгодится."
        };

        private void OnEnable()
        {
            CraftingSystem.OnCraftSuccess += HandleSuccess;
            CraftingSystem.OnCraftFailed += HandleFailed;
        }

        private void OnDisable()
        {
            CraftingSystem.OnCraftSuccess -= HandleSuccess;
            CraftingSystem.OnCraftFailed -= HandleFailed;
        }

        private void HandleSuccess(CraftRecipe recipe)
        {
            if (_successReplies.Length > 0)
            {
                string reply = _successReplies[Random.Range(0, _successReplies.Length)];
                SpeechBubbleManager.Say(reply);
            }
        }

        private void HandleFailed(ItemData a, ItemData b)
        {
            if (_failReplies.Length > 0)
            {
                string reply = _failReplies[Random.Range(0, _failReplies.Length)];
                SpeechBubbleManager.Say(reply);
            }
        }
    }
}
