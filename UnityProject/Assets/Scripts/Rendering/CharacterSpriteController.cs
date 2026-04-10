using UnityEngine;
using ZeldaDaughter.Combat;
using ZeldaDaughter.Input;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.Rendering
{
    [RequireComponent(typeof(SpriteRenderer), typeof(BillboardRenderer), typeof(BillboardDirectionResolver))]
    public class CharacterSpriteController : MonoBehaviour
    {
        [SerializeField] private CharacterVisualConfig _config;

        [Header("State Colors")]
        [SerializeField] private Color _poisonedTint  = new Color(0.4f, 0.9f, 0.4f, 1f);
        [SerializeField] private Color _burnedTint    = new Color(1f, 0.5f, 0.2f, 1f);
        [SerializeField] private Color _normalTint    = Color.white;

        [Header("Overlay")]
        [SerializeField] private SpriteRenderer _overlayRenderer;

        private SpriteRenderer _spriteRenderer;
        private BillboardDirectionResolver _directionResolver;

        private CharacterVisualState _currentState = CharacterVisualState.Normal;
        private SpriteDirection _currentDirection   = SpriteDirection.Front;
        private bool _isMoving;

        private void Awake()
        {
            _spriteRenderer   = GetComponent<SpriteRenderer>();
            _directionResolver = GetComponent<BillboardDirectionResolver>();

            if (_config != null)
                ApplyScale();
        }

        private void OnEnable()
        {
            _directionResolver.OnDirectionChanged += SetDirection;

            CharacterMovement.OnMovingStateChanged += HandleMovingStateChanged;
            PlayerHealthState.OnWoundAdded         += HandleWoundAdded;
            PlayerHealthState.OnWoundRemoved       += HandleWoundRemoved;
            WeightSystem.OnOverloadChanged         += HandleOverloadChanged;
        }

        private void OnDisable()
        {
            _directionResolver.OnDirectionChanged -= SetDirection;

            CharacterMovement.OnMovingStateChanged -= HandleMovingStateChanged;
            PlayerHealthState.OnWoundAdded         -= HandleWoundAdded;
            PlayerHealthState.OnWoundRemoved       -= HandleWoundRemoved;
            WeightSystem.OnOverloadChanged         -= HandleOverloadChanged;
        }

        private void Update()
        {
            // Feed movement direction to resolver each frame while moving
            if (_isMoving && transform.parent != null)
                _directionResolver.Tick(transform.parent.forward);
        }

        // --- Public API ---

        public void SetState(CharacterVisualState state)
        {
            if (_currentState == state) return;
            _currentState = state;
            RefreshVisual();
        }

        public void SetDirection(SpriteDirection dir)
        {
            if (_currentDirection == dir) return;
            _currentDirection = dir;
            RefreshVisual();
        }

        // --- Event Handlers ---

        private void HandleMovingStateChanged(bool isMoving)
        {
            _isMoving = isMoving;
            RefreshVisual();
        }

        private void HandleWoundAdded(Wound wound)
        {
            CharacterVisualState next = wound.Type switch
            {
                WoundType.Puncture  => CharacterVisualState.Wounded,
                WoundType.Fracture  => CharacterVisualState.Wounded,
                WoundType.Burn      => CharacterVisualState.Burned,
                WoundType.Poison    => CharacterVisualState.Poisoned,
                _                   => _currentState
            };

            // Only escalate, never downgrade while wound is active
            if (next != _currentState)
                SetState(next);
        }

        private void HandleWoundRemoved(WoundType type)
        {
            // Re-evaluate: if no more relevant wounds, return to Normal
            SetState(CharacterVisualState.Normal);
        }

        private void HandleOverloadChanged(bool overloaded)
        {
            if (overloaded && _currentState == CharacterVisualState.Normal)
                SetState(CharacterVisualState.Overloaded);
            else if (!overloaded && _currentState == CharacterVisualState.Overloaded)
                SetState(CharacterVisualState.Normal);
        }

        // --- Visual Update ---

        private void RefreshVisual()
        {
            if (_config == null || _spriteRenderer == null) return;

            Sprite baseSprite = _isMoving
                ? _config.GetWalkSprite(_currentDirection)
                : _config.GetIdleSprite(_currentDirection);

            _spriteRenderer.sprite = baseSprite;
            _spriteRenderer.color  = GetStateTint(_currentState);

            Sprite overlay = _config.GetStateOverlay(_currentState);
            if (_overlayRenderer != null)
            {
                _overlayRenderer.sprite  = overlay;
                _overlayRenderer.enabled = overlay != null;
            }
        }

        private Color GetStateTint(CharacterVisualState state)
        {
            return state switch
            {
                CharacterVisualState.Poisoned => _poisonedTint,
                CharacterVisualState.Burned   => _burnedTint,
                _                             => _normalTint
            };
        }

        private void ApplyScale()
        {
            transform.localScale = Vector3.one * _config.BillboardScale;

            if (_config.PivotOffset != Vector2.zero)
                transform.localPosition = new Vector3(
                    _config.PivotOffset.x,
                    _config.PivotOffset.y,
                    0f);
        }
    }
}
