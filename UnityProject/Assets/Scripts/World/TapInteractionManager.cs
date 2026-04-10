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

        private void Start()
        {
            // Debug: log screen positions of all interactable objects
            var cam = Camera.main;
            if (cam == null) return;
            var interactables = FindObjectsOfType<MonoBehaviour>();
            foreach (var mb in interactables)
            {
                if (mb is IInteractable interactable)
                {
                    Vector3 screenPos = cam.WorldToScreenPoint(mb.transform.position);
                    float ax = screenPos.x * 1080f / Screen.width;
                    float ay = (Screen.height - screenPos.y) * 2340f / Screen.height;
                    ZeldaDaughter.Debugging.ZDLog.Log("Interact",
                        $"Interactable {mb.gameObject.name} world=({mb.transform.position.x:F1},{mb.transform.position.y:F1},{mb.transform.position.z:F1}) " +
                        $"screen=({screenPos.x:F0},{screenPos.y:F0}) android=({ax:F0},{ay:F0})");
                }
            }
            // Also log enemies (tag-based, not IInteractable)
            foreach (var enemy in GameObject.FindGameObjectsWithTag("Enemy"))
            {
                Vector3 sp = cam.WorldToScreenPoint(enemy.transform.position);
                float eax = sp.x * 1080f / Screen.width;
                float eay = (Screen.height - sp.y) * 2340f / Screen.height;
                ZeldaDaughter.Debugging.ZDLog.Log("Interact",
                    $"Enemy {enemy.name} world=({enemy.transform.position.x:F1},{enemy.transform.position.y:F1},{enemy.transform.position.z:F1}) " +
                    $"screen=({sp.x:F0},{sp.y:F0}) android=({eax:F0},{eay:F0})");
            }
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

            // Try precise raycast first, then widen with SphereCast for small objects
            RaycastHit hit = default;
            bool found = false;

            // 1. Precise RaycastAll — check for direct hits on interactables/enemies
            var hits = Physics.RaycastAll(ray, 100f);
            for (int i = 0; i < hits.Length; i++)
            {
                var go = hits[i].collider.gameObject;
                if (go.CompareTag("Enemy")
                    || go.GetComponent<IInteractable>() != null
                    || go.GetComponentInParent<IInteractable>() != null)
                {
                    hit = hits[i];
                    found = true;
                    break;
                }
            }

            // 2. If no interactable found, widen search with SphereCast
            if (!found)
            {
                var sphereHits = Physics.SphereCastAll(ray, 1.2f, 100f);
                for (int i = 0; i < sphereHits.Length; i++)
                {
                    var go = sphereHits[i].collider.gameObject;
                    if (go.CompareTag("Enemy")
                        || go.GetComponent<IInteractable>() != null
                        || go.GetComponentInParent<IInteractable>() != null)
                    {
                        hit = sphereHits[i];
                        found = true;
                        break;
                    }
                }
            }

            // 3. Fallback to ground/nearest from original raycast
            if (!found)
            {
                if (hits.Length > 0)
                {
                    hit = hits[0];
                }
                else
                {
                    ZeldaDaughter.Debugging.ZDLog.Log("Interact", $"HandleTap: Raycast miss at ({screenPos.x:F0},{screenPos.y:F0})");
                    return;
                }
            }

            ZeldaDaughter.Debugging.ZDLog.Log("Interact", $"HandleTap: Hit {hit.collider.gameObject.name} at ({screenPos.x:F0},{screenPos.y:F0})");

            // Враг (tag=Enemy) — передаём управление CombatController напрямую
            if (hit.collider.CompareTag("Enemy"))
            {
                ZeldaDaughter.Debugging.ZDLog.Log("Interact", $"Tap target={hit.collider.gameObject.name} (Enemy)");
                if (_combatController != null)
                    _combatController.AttackTarget(hit.collider.gameObject);
                return;
            }

            var interactable = hit.collider.GetComponent<IInteractable>()
                               ?? hit.collider.GetComponentInParent<IInteractable>();

            if (interactable == null || !interactable.CanInteract())
            {
                ZeldaDaughter.Debugging.ZDLog.Log("Interact", $"HandleTap: {hit.collider.gameObject.name} is not interactable");
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
