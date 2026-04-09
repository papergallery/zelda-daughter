using UnityEngine;
using ZeldaDaughter.Debugging;
using ZeldaDaughter.Inventory;
using ZeldaDaughter.Save;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.World
{
    public class Pickupable : MonoBehaviour, IInteractable, ISaveable
    {
        [SerializeField] private ItemData _itemData;
        [SerializeField] private int _amount = 1;
        [SerializeField] private string _saveId;
        [SerializeField] private float _interactionRange = 1.5f;

        private bool _pickedUp;

        public string InteractionPrompt => _itemData != null ? _itemData.DisplayName : "???";
        public Transform InteractionPoint => transform;
        public float InteractionRange => _interactionRange;
        public InteractionType Type => InteractionType.Pickup;

        public bool CanInteract() => !_pickedUp;

        public void Interact(GameObject actor)
        {
            if (actor.TryGetComponent<Animator>(out var animator))
                animator.SetTrigger("PickUp");

            if (actor.TryGetComponent<PlayerInventory>(out var inventory))
                inventory.AddItem(_itemData, _amount);
            // Speech handled by SpeechBubbleManager via PlayerInventory.OnItemAdded

            _pickedUp = true;
            ZDLog.Log("Interact", $"Pickup item={_itemData?.name} amount={_amount}");
            gameObject.SetActive(false);
        }

        // ISaveable

        public string SaveId => string.IsNullOrEmpty(_saveId)
            ? gameObject.name + transform.position.GetHashCode()
            : _saveId;

        public object CaptureState() => _pickedUp;

        public void RestoreState(object state)
        {
            if (state is bool pickedUp && pickedUp)
                gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(_saveId))
                _saveId = gameObject.name + "_" + Mathf.Abs(transform.position.GetHashCode());
        }
#endif
    }
}
