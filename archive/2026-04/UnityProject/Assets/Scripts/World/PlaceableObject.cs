using UnityEngine;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.Save;

namespace ZeldaDaughter.World
{
    public class PlaceableObject : MonoBehaviour, IInteractable, ISaveable
    {
        [SerializeField] private ItemData _itemData;
        [SerializeField] private Transform _interactionPoint;
        [SerializeField] private float _interactionRange = 1.5f;

        private string _saveId;

        public InteractionType Type => InteractionType.Pickup;
        public string InteractionPrompt => _itemData != null ? _itemData.name : string.Empty;
        public Transform InteractionPoint => _interactionPoint != null ? _interactionPoint : transform;
        public float InteractionRange => _interactionRange;
        public string SaveId => _saveId;
        public ItemData ItemData => _itemData;

        public void Setup(ItemData item)
        {
            _itemData = item;
            _saveId = $"placed_{item.Id}_{transform.position.GetHashCode()}";
        }

        public bool CanInteract()
        {
            return _itemData != null;
        }

        public void Interact(GameObject actor)
        {
            if (_itemData == null) return;

            var inventory = actor.GetComponent<PlayerInventory>();
            if (inventory == null)
                inventory = actor.GetComponentInParent<PlayerInventory>();

            if (inventory != null && inventory.AddItem(_itemData, 1))
            {
                Destroy(gameObject);
            }
        }

        public object CaptureState()
        {
            return new PlaceableSaveData
            {
                itemId = _itemData != null ? _itemData.Id : string.Empty,
                posX = transform.position.x,
                posY = transform.position.y,
                posZ = transform.position.z,
                rotY = transform.eulerAngles.y
            };
        }

        public void RestoreState(object state)
        {
            if (state is not PlaceableSaveData data) return;

            transform.position = new Vector3(data.posX, data.posY, data.posZ);
            transform.eulerAngles = new Vector3(0f, data.rotY, 0f);

            if (!string.IsNullOrEmpty(data.itemId))
                _itemData = Resources.Load<ItemData>(data.itemId);

            _saveId = $"placed_{data.itemId}_{transform.position.GetHashCode()}";
        }

        [System.Serializable]
        private class PlaceableSaveData
        {
            public string itemId;
            public float posX, posY, posZ, rotY;
        }
    }
}
