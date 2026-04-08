using UnityEngine;

namespace ZeldaDaughter.World
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Interaction Config", fileName = "InteractionConfig")]
    public class InteractionConfig : ScriptableObject
    {
        [SerializeField] private float _highlightRange = 5f;
        [SerializeField] private float _autoMoveSpeed = 3f;
        [SerializeField] private float _defaultInteractionRange = 1.5f;
        [SerializeField] private float _speechBubbleDuration = 3f;
        [SerializeField] private Color _highlightColor = Color.yellow;

        public float HighlightRange => _highlightRange;
        public float AutoMoveSpeed => _autoMoveSpeed;
        public float DefaultInteractionRange => _defaultInteractionRange;
        public float SpeechBubbleDuration => _speechBubbleDuration;
        public Color HighlightColor => _highlightColor;
    }
}
