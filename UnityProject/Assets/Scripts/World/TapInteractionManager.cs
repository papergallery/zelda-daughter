using UnityEngine;
using ZeldaDaughter.Input;

namespace ZeldaDaughter.World
{
    public class TapInteractionManager : MonoBehaviour
    {
        [SerializeField] private CharacterAutoMove _autoMove;
        [SerializeField] private GameObject _player;

        private IInteractable _currentTarget;

        private void Awake()
        {
            if (_autoMove == null && _player != null)
                _player.TryGetComponent(out _autoMove);
        }

        private void OnEnable()
        {
            GestureDispatcher.OnTap += HandleTap;
            CharacterAutoMove.OnAutoMoveCancelled += HandleAutoMoveCancelled;
        }

        private void OnDisable()
        {
            GestureDispatcher.OnTap -= HandleTap;
            CharacterAutoMove.OnAutoMoveCancelled -= HandleAutoMoveCancelled;
        }

        private void HandleTap(Vector2 screenPos)
        {
            var ray = Camera.main.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, 100f))
                return;

            var interactable = hit.collider.GetComponent<IInteractable>()
                               ?? hit.collider.GetComponentInParent<IInteractable>();

            if (interactable == null || !interactable.CanInteract())
                return;

            // Отменить текущее движение к предыдущей цели
            if (_currentTarget != null)
                _autoMove.Cancel();

            _currentTarget = interactable;
            _autoMove.MoveTo(
                interactable.InteractionPoint.position,
                interactable.InteractionRange,
                OnReachedInteractable
            );
        }

        private void OnReachedInteractable()
        {
            if (_currentTarget == null)
                return;

            _currentTarget.Interact(_player);
            _currentTarget = null;
        }

        private void HandleAutoMoveCancelled()
        {
            _currentTarget = null;
        }
    }
}
