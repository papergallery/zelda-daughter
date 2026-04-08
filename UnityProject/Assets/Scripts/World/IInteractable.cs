using UnityEngine;

namespace ZeldaDaughter.World
{
    public interface IInteractable
    {
        string InteractionPrompt { get; }
        Transform InteractionPoint { get; }
        float InteractionRange { get; }
        bool CanInteract();
        void Interact(GameObject actor);
        InteractionType Type { get; }
    }
}
