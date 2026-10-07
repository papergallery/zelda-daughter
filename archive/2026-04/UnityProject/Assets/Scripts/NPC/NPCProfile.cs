using UnityEngine;

namespace ZeldaDaughter.NPC
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/NPC/NPC Profile", fileName = "NewNPCProfile")]
    public class NPCProfile : ScriptableObject
    {
        [SerializeField] private string _npcId;
        [SerializeField] private string _npcName;
        [SerializeField] private NPCRole _role;
        [SerializeField] private Sprite _portrait;

        [Header("Behaviour")]
        [SerializeField] private NPCScheduleData _schedule;
        [SerializeField] private DialogueTree _dialogueTree;

        /// <summary>Null для NPC без торговли.</summary>
        [SerializeField] private TradeInventoryData _tradeInventory;

        [Header("Legacy Icon Fallback")]
        [SerializeField] private Sprite[] _iconSequence;
        [SerializeField] private float _iconDisplayInterval = 1f;

        public string NpcId => _npcId;
        public string NpcName => _npcName;
        public NPCRole Role => _role;
        public Sprite Portrait => _portrait;
        public NPCScheduleData Schedule => _schedule;
        public DialogueTree DialogueTree => _dialogueTree;
        public TradeInventoryData TradeInventory => _tradeInventory;
        public Sprite[] IconSequence => _iconSequence;
        public float IconDisplayInterval => _iconDisplayInterval;
    }
}
