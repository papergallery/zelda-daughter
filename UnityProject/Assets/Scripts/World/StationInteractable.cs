using UnityEngine;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.World
{
    public class StationInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField] private StationType _stationType = StationType.Anvil;
        [SerializeField] private Transform _interactionPoint;
        [SerializeField] private float _interactionRange = 2f;

        public InteractionType Type => InteractionType.Station;
        public Transform InteractionPoint => _interactionPoint != null ? _interactionPoint : transform;
        public float InteractionRange => _interactionRange;
        public StationType StationType => _stationType;

        public string InteractionPrompt => _stationType switch
        {
            StationType.Smelter => "Плавильня",
            StationType.Anvil   => "Наковальня",
            _                   => "Станок"
        };

        public bool CanInteract() => true;

        public void Interact(GameObject actor)
        {
            ZeldaDaughter.UI.StationUI.Open(_stationType);
        }
    }
}
