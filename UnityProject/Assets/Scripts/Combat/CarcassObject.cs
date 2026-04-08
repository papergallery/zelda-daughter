using UnityEngine;
using ZeldaDaughter.World;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.Combat
{
    /// <summary>
    /// Туша врага. Спавнится после смерти. Тап = разделка.
    /// Без ножа — минимальный лут; с ножом — полная разделка.
    /// Исчезает через despawnTime секунд.
    /// </summary>
    public class CarcassObject : MonoBehaviour, IInteractable
    {
        [SerializeField] private LootTable _lootTable;
        [SerializeField] private Transform _interactionPoint;
        [SerializeField] private float _interactionRange = 1.5f;
        [SerializeField] private float _despawnTime = 120f;
        [SerializeField] private string _butcherToolId = "knife";

        private bool _isLooted;
        private float _despawnTimer;

        public string InteractionPrompt => _isLooted ? "" : "Обыскать";
        public Transform InteractionPoint => _interactionPoint != null ? _interactionPoint : transform;
        public float InteractionRange => _interactionRange;
        public InteractionType Type => InteractionType.Carcass;

        private void Awake()
        {
            _despawnTimer = _despawnTime;
        }

        private void Update()
        {
            _despawnTimer -= Time.deltaTime;
            if (_despawnTimer <= 0f)
                Destroy(gameObject);
        }

        public bool CanInteract()
        {
            return !_isLooted;
        }

        public void Interact(GameObject actor)
        {
            if (_isLooted || _lootTable == null)
                return;

            var inventory = actor.GetComponent<PlayerInventory>();
            if (inventory == null)
                inventory = actor.GetComponentInParent<PlayerInventory>();
            if (inventory == null)
                return;

            bool hasTool = HasToolById(inventory, _butcherToolId);
            var loot = _lootTable.RollLoot(hasTool);

            if (loot.Count == 0)
            {
                SpeechBubbleManager.Say("Ничего полезного...");
                _isLooted = true;
                return;
            }

            foreach (var (item, amount) in loot)
                inventory.AddItem(item, amount);

            SpeechBubbleManager.Say(hasTool ? "Разделка завершена." : "Без ножа много не возьмёшь...");
            _isLooted = true;
        }

        /// <summary>Задать таблицу лута извне (например, из EnemySpawnZone).</summary>
        public void Setup(LootTable lootTable)
        {
            _lootTable = lootTable;
        }

        // PlayerInventory.HasItem принимает ItemData, не строку.
        // Ищем наличие предмета по его Id через перебор слотов.
        private static bool HasToolById(PlayerInventory inventory, string toolId)
        {
            if (string.IsNullOrEmpty(toolId))
                return false;

            var items = inventory.Items;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].Item != null && items[i].Item.Id == toolId)
                    return true;
            }
            return false;
        }
    }
}
