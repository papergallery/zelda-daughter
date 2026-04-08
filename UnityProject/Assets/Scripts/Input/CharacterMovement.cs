using UnityEngine;

namespace ZeldaDaughter.Input
{
    /// <summary>
    /// Moves the character using CharacterController based on input from TouchInputManager.
    /// Walk/run determined by swipe intensity. Smooth rotation toward movement direction.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float _walkSpeed = 2.5f;
        [SerializeField] private float _runSpeed = 5f;
        [SerializeField] private float _rotationSpeed = 10f;
        [SerializeField] private float _gravity = -15f;
        [SerializeField] private float _runThreshold = 0.7f;

        [Header("Water")]
        [SerializeField] private float _waterSpeedMultiplier = 0.4f;

        [Header("Feel")]
        [SerializeField] private float _decelerationTime = 0.15f;

        private CharacterController _controller;
        private Animator _animator;
        private Vector2 _inputDirection;
        private float _inputIntensity;
        private float _verticalVelocity;
        private bool _isMoving;
        private bool _isStopping;
        private bool _inWater;
        private Camera _mainCamera;

        private Vector3 _externalDirection;
        private float _externalIntensity;
        private bool _hasExternalMovement;

        private float _previousSpeed;
        private bool _wasMoving;

        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimIsMoving = Animator.StringToHash("IsMoving");

        public float CurrentSpeed { get; private set; }
        public bool IsRunning => _inputIntensity >= _runThreshold && _isMoving;
        public bool InWater => _inWater;

        public static event System.Action<float> OnSpeedChanged;
        public static event System.Action<bool> OnMovingStateChanged;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _animator = GetComponentInChildren<Animator>();
            _mainCamera = Camera.main;
        }

        private void OnEnable()
        {
            TouchInputManager.OnMoveInput += HandleMoveInput;
            TouchInputManager.OnMoveStop += HandleMoveStop;
        }

        private void OnDisable()
        {
            TouchInputManager.OnMoveInput -= HandleMoveInput;
            TouchInputManager.OnMoveStop -= HandleMoveStop;
        }

        private void Update()
        {
            ApplyGravity();
            ApplyDeceleration();

            if (_isMoving)
            {
                Vector3 worldDir;
                float targetSpeed;

                if (_hasExternalMovement)
                {
                    worldDir = _externalDirection;
                    targetSpeed = _externalIntensity >= _runThreshold ? _runSpeed : _walkSpeed;
                }
                else
                {
                    worldDir = ScreenToWorldDirection(_inputDirection);
                    targetSpeed = _inputIntensity >= _runThreshold ? _runSpeed : _walkSpeed;
                }

                if (_inWater)
                    targetSpeed *= _waterSpeedMultiplier;

                var move = worldDir * targetSpeed + Vector3.up * _verticalVelocity;
                _controller.Move(move * Time.deltaTime);

                // Smooth rotation
                if (worldDir.sqrMagnitude > 0.01f)
                {
                    var targetRotation = Quaternion.LookRotation(worldDir, Vector3.up);
                    transform.rotation = Quaternion.Slerp(
                        transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
                }

                CurrentSpeed = targetSpeed;
            }
            else
            {
                // Apply only gravity when stopped
                _controller.Move(Vector3.up * _verticalVelocity * Time.deltaTime);
                CurrentSpeed = 0f;
            }

            UpdateAnimator();

            if (Mathf.Abs(CurrentSpeed - _previousSpeed) > 0.01f)
            {
                OnSpeedChanged?.Invoke(CurrentSpeed);
                _previousSpeed = CurrentSpeed;
            }

            if (_isMoving != _wasMoving)
            {
                OnMovingStateChanged?.Invoke(_isMoving);
                _wasMoving = _isMoving;
            }
        }

        private void ApplyDeceleration()
        {
            if (!_isStopping) return;

            float decelerationRate = _decelerationTime > 0f
                ? Time.deltaTime / _decelerationTime
                : 1f;

            _inputIntensity = Mathf.MoveTowards(_inputIntensity, 0f, decelerationRate);

            if (_inputIntensity <= 0f)
            {
                _inputIntensity = 0f;
                _isStopping = false;
                _isMoving = false;
            }
        }

        private void HandleMoveInput(Vector2 direction, float intensity)
        {
            ClearExternalMovement();
            _inputDirection = direction;
            _inputIntensity = intensity;
            _isMoving = true;
        }

        private void HandleMoveStop()
        {
            if (_isMoving && _inputIntensity > 0f)
            {
                _isStopping = true;
            }
            else
            {
                _isMoving = false;
                _inputIntensity = 0f;
            }
        }

        private Vector3 ScreenToWorldDirection(Vector2 screenDir)
        {
            if (_mainCamera == null) return Vector3.forward;

            // Convert 2D screen direction to 3D world direction based on camera orientation
            var camForward = _mainCamera.transform.forward;
            var camRight = _mainCamera.transform.right;

            // Flatten to horizontal plane
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();

            return (camRight * screenDir.x + camForward * screenDir.y).normalized;
        }

        private void ApplyGravity()
        {
            if (_controller.isGrounded && _verticalVelocity < 0f)
                _verticalVelocity = -2f;
            else
                _verticalVelocity += _gravity * Time.deltaTime;
        }

        private void UpdateAnimator()
        {
            if (_animator == null) return;

            // Speed: 0 = idle, 0-0.5 = walk, 0.5-1 = run
            float animSpeed = _isMoving
                ? Mathf.Lerp(0.25f, 1f, _inputIntensity)
                : 0f;

            _animator.SetFloat(AnimSpeed, animSpeed, 0.1f, Time.deltaTime);
            _animator.SetBool(AnimIsMoving, _isMoving);
        }

        public void SetExternalMovement(Vector3 worldDirection, float intensity)
        {
            _externalDirection = worldDirection;
            _externalIntensity = intensity;
            _hasExternalMovement = true;
            _isMoving = true;
            _isStopping = false;
        }

        public void ClearExternalMovement()
        {
            _hasExternalMovement = false;
            _externalIntensity = 0f;
        }

        public void SetInWater(bool inWater)
        {
            _inWater = inWater;
        }
    }
}
