using UnityEngine;

namespace ZeldaDaughter.Rendering
{
    [CreateAssetMenu(fileName = "CharacterVisualConfig", menuName = "Zelda's Daughter/Rendering/Character Visual Config")]
    public class CharacterVisualConfig : ScriptableObject
    {
        [SerializeField] private string _characterId;

        [Header("Idle Sprites")]
        [SerializeField] private Sprite _idleFront;
        [SerializeField] private Sprite _idleBack;
        [SerializeField] private Sprite _idleLeft;
        [SerializeField] private Sprite _idleRight;

        [Header("Walk Sprites")]
        [SerializeField] private Sprite _walkFront;
        [SerializeField] private Sprite _walkBack;
        [SerializeField] private Sprite _walkLeft;
        [SerializeField] private Sprite _walkRight;

        [Header("State Overlays / Replacements")]
        [SerializeField] private Sprite _woundedOverlay;
        [SerializeField] private Sprite _burnedOverlay;
        [SerializeField] private Sprite _poisonedOverlay;
        [SerializeField] private Sprite _overloadedOverlay;

        [Header("Billboard Settings")]
        [SerializeField] private float _billboardScale = 1f;
        [SerializeField] private Vector2 _pivotOffset = Vector2.zero;

        public string CharacterId => _characterId;
        public float BillboardScale => _billboardScale;
        public Vector2 PivotOffset => _pivotOffset;

        public Sprite GetIdleSprite(SpriteDirection direction)
        {
            return direction switch
            {
                SpriteDirection.Front => _idleFront,
                SpriteDirection.Back  => _idleBack,
                SpriteDirection.Left  => _idleLeft,
                SpriteDirection.Right => _idleRight,
                _                     => _idleFront
            };
        }

        public Sprite GetWalkSprite(SpriteDirection direction)
        {
            return direction switch
            {
                SpriteDirection.Front => _walkFront != null ? _walkFront : _idleFront,
                SpriteDirection.Back  => _walkBack  != null ? _walkBack  : _idleBack,
                SpriteDirection.Left  => _walkLeft  != null ? _walkLeft  : _idleLeft,
                SpriteDirection.Right => _walkRight != null ? _walkRight : _idleRight,
                _                     => _idleFront
            };
        }

        /// <summary>Returns overlay sprite for a visual state, or null if none assigned.</summary>
        public Sprite GetStateOverlay(CharacterVisualState state)
        {
            return state switch
            {
                CharacterVisualState.Wounded   => _woundedOverlay,
                CharacterVisualState.Burned    => _burnedOverlay,
                CharacterVisualState.Poisoned  => _poisonedOverlay,
                CharacterVisualState.Overloaded => _overloadedOverlay,
                _                              => null
            };
        }
    }
}
