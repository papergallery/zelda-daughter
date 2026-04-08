using UnityEngine;

namespace ZeldaDaughter.Input
{
    /// <summary>
    /// Автоматически ведёт персонажа к целевой позиции через CharacterMovement.
    /// Отменяется при свайпе игрока.
    /// </summary>
    [RequireComponent(typeof(CharacterMovement))]
    public class CharacterAutoMove : MonoBehaviour
    {
        [SerializeField] private float _stoppingDistance = 1.5f;
        [SerializeField] private float _moveIntensity = 0.8f;

        private CharacterMovement _movement;
        private Vector3 _targetPosition;
        private bool _isAutoMoving;
        private System.Action _onReachedCallback;

        public static event System.Action OnReachedTarget;
        public static event System.Action OnAutoMoveCancelled;

        private void Awake()
        {
            _movement = GetComponent<CharacterMovement>();
        }

        private void OnEnable()
        {
            TouchInputManager.OnMoveInput += HandleMoveInput;
        }

        private void OnDisable()
        {
            TouchInputManager.OnMoveInput -= HandleMoveInput;
        }

        private void Update()
        {
            if (!_isAutoMoving) return;

            var flatSelf = new Vector3(transform.position.x, 0f, transform.position.z);
            var flatTarget = new Vector3(_targetPosition.x, 0f, _targetPosition.z);
            float distance = Vector3.Distance(flatSelf, flatTarget);

            if (distance <= _stoppingDistance)
            {
                _isAutoMoving = false;
                _movement.ClearExternalMovement();
                _onReachedCallback?.Invoke();
                OnReachedTarget?.Invoke();
            }
            else
            {
                var direction = (flatTarget - flatSelf).normalized;
                _movement.SetExternalMovement(direction, _moveIntensity);
            }
        }

        /// <summary>
        /// Начать автоматическое движение к точке.
        /// </summary>
        /// <param name="target">Целевая позиция в мировых координатах.</param>
        /// <param name="stopDistance">Расстояние остановки (переопределяет _stoppingDistance).</param>
        /// <param name="onReached">Коллбэк при достижении цели.</param>
        public void MoveTo(Vector3 target, float stopDistance, System.Action onReached = null)
        {
            _targetPosition = target;
            _stoppingDistance = stopDistance;
            _onReachedCallback = onReached;
            _isAutoMoving = true;
        }

        public void Cancel()
        {
            if (!_isAutoMoving) return;

            _isAutoMoving = false;
            _movement.ClearExternalMovement();
            OnAutoMoveCancelled?.Invoke();
        }

        private void HandleMoveInput(Vector2 direction, float intensity)
        {
            if (_isAutoMoving)
                Cancel();
        }
    }
}
