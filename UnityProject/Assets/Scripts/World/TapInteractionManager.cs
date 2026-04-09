using UnityEngine;
using ZeldaDaughter.Combat;
using ZeldaDaughter.Debugging;
using ZeldaDaughter.Input;

namespace ZeldaDaughter.World
{
    public class TapInteractionManager : MonoBehaviour
    {
        [SerializeField] private CharacterAutoMove _autoMove;
        [SerializeField] private GameObject _player;
        [SerializeField] private CombatController _combatController;

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
            var cam = Camera.main;
            if (cam == null)
            {
                ZeldaDaughter.Debugging.ZDLog.Log("Interact", "HandleTap: Camera.main is null!");
                return;
            }
            var ray = cam.ScreenPointToRay(screenPos);
            if (!Physics.Raycast(ray, out var hit, 100f))
            {
                ZeldaDaughter.Debugging.ZDLog.Log("Interact", $"HandleTap: Raycast miss at ({screenPos.x:F0},{screenPos.y:F0})");
                return;
            }

            ZeldaDaughter.Debugging.ZDLog.Log("Interact", $"HandleTap: Hit {hit.collider.gameObject.name} at ({screenPos.x:F0},{screenPos.y:F0})");

            var interactable = hit.collider.GetComponent<IInteractable>()
                               ?? hit.collider.GetComponentInParent<IInteractable>();

            if (interactable == null || !interactable.CanInteract())
            {
                ZeldaDaughter.Debugging.ZDLog.Log("Interact", $"HandleTap: {hit.collider.gameObject.name} is not interactable");
                return;
            }

            // Враг — передаём управление CombatController, он сам подходит и бьёт
            if (interactable.Type == InteractionType.Enemy)
            {
                if (_combatController != null)
                    _combatController.AttackTarget(hit.collider.gameObject);
                return;
            }

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

            ZDLog.Log("Interact", $"Tap target={_currentTarget}");
            _currentTarget.Interact(_player);
            _currentTarget = null;
        }

        private void HandleAutoMoveCancelled()
        {
            _currentTarget = null;
        }
    }
}
