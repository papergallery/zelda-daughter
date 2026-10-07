using UnityEngine;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.World
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/World Interaction Rule", fileName = "NewWorldInteractionRule")]
    public class WorldInteractionRule : ScriptableObject
    {
        [SerializeField] private ItemData _requiredItem;
        [SerializeField] private string _targetTag;
        [SerializeField] private InteractionResult _result;
        [SerializeField] private ItemData _resultItem;
        [SerializeField] private bool _consumeItem;

        public ItemData RequiredItem => _requiredItem;
        public string TargetTag => _targetTag;
        public InteractionResult Result => _result;
        public ItemData ResultItem => _resultItem;
        public bool ConsumeItem => _consumeItem;
    }

    public enum InteractionResult
    {
        LightFire,
        TransformItem
    }
}
