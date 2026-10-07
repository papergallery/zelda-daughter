using UnityEngine;
using ZeldaDaughter.Input;

namespace ZeldaDaughter.Rendering
{
    /// <summary>
    /// Resolves the 4-directional facing of a billboard sprite based on movement
    /// projected into isometric camera space.
    /// </summary>
    public class BillboardDirectionResolver : MonoBehaviour
    {
        // Minimum movement speed before direction updates (avoids flicker when nearly stopped)
        [SerializeField] private float _directionDeadzone = 0.15f;

        private SpriteDirection _currentDirection = SpriteDirection.Front;
        private Camera _cam;

        public event System.Action<SpriteDirection> OnDirectionChanged;

        private void Awake()
        {
            _cam = Camera.main;
        }

        private void OnEnable()
        {
            CharacterMovement.OnSpeedChanged += HandleSpeedChanged;
        }

        private void OnDisable()
        {
            CharacterMovement.OnSpeedChanged -= HandleSpeedChanged;
        }

        private void HandleSpeedChanged(float speed)
        {
            if (speed < _directionDeadzone) return;

            // Sample the character's actual world movement direction from its transform
            UpdateDirectionFromWorldForward(transform.parent != null
                ? transform.parent.forward
                : transform.forward);
        }

        /// <summary>
        /// Resolves direction every frame from the owner's forward vector.
        /// Called by CharacterSpriteController from its Update.
        /// </summary>
        public void Tick(Vector3 worldMoveDirection)
        {
            if (worldMoveDirection.sqrMagnitude < _directionDeadzone * _directionDeadzone) return;
            UpdateDirectionFromWorldForward(worldMoveDirection);
        }

        private void UpdateDirectionFromWorldForward(Vector3 worldDir)
        {
            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null) return;
            }

            // Project the world direction into camera-local 2D space to get
            // screen-relative facing (needed for isometric projection).
            Vector3 camRight   = _cam.transform.right;
            Vector3 camForward = _cam.transform.forward;

            // Flatten to ignore vertical component
            camRight.y   = 0f;
            camForward.y = 0f;
            camRight.Normalize();
            camForward.Normalize();

            float horizontal = Vector3.Dot(worldDir, camRight);
            float vertical   = Vector3.Dot(worldDir, camForward);

            SpriteDirection resolved = ResolveQuadrant(horizontal, vertical);

            if (resolved != _currentDirection)
            {
                _currentDirection = resolved;
                OnDirectionChanged?.Invoke(_currentDirection);
            }
        }

        private static SpriteDirection ResolveQuadrant(float h, float v)
        {
            // Dominant axis wins; 4-way directions only
            if (Mathf.Abs(h) >= Mathf.Abs(v))
                return h > 0f ? SpriteDirection.Right : SpriteDirection.Left;
            else
                return v > 0f ? SpriteDirection.Back : SpriteDirection.Front;
        }

        public SpriteDirection CurrentDirection => _currentDirection;
    }
}
