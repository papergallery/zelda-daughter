using UnityEngine;

namespace ZeldaDaughter.NPC
{
    [System.Obsolete("Use NPCProfile instead")]
    [CreateAssetMenu(menuName = "ZeldaDaughter/NPC Data", fileName = "NewNPCData")]
    public class NPCData : ScriptableObject
    {
        [SerializeField] private string _npcName;
        [SerializeField] private Sprite[] _iconSequence;
        [SerializeField] private float _iconDisplayInterval = 1.5f;

        public string NpcName => _npcName;
        public Sprite[] IconSequence => _iconSequence;
        public float IconDisplayInterval => _iconDisplayInterval;
    }
}
