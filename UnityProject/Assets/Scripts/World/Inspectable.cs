using UnityEngine;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.World
{
    public class Inspectable : MonoBehaviour, IInteractable
    {
        [SerializeField] private string[] _descriptions;
        [SerializeField] private float _interactionRange = 2f;

        private int _descriptionIndex;

        public string InteractionPrompt => "Осмотреть";
        public Transform InteractionPoint => transform;
        public float InteractionRange => _interactionRange;
        public InteractionType Type => InteractionType.Inspect;

        public bool CanInteract() => _descriptions != null && _descriptions.Length > 0;

        public void Interact(GameObject actor)
        {
            if (actor.TryGetComponent<Animator>(out var animator))
                animator.SetTrigger("Interact");

            SpeechBubbleManager.Say(_descriptions[_descriptionIndex]);

            _descriptionIndex = (_descriptionIndex + 1) % _descriptions.Length;
        }
    }
}
