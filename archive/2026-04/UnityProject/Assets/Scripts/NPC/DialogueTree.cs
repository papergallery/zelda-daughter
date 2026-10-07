using UnityEngine;

namespace ZeldaDaughter.NPC
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/NPC/Dialogue Tree", fileName = "NewDialogueTree")]
    public class DialogueTree : ScriptableObject
    {
        [SerializeField] private string _startNodeId;
        [SerializeField] private DialogueNode[] _nodes;

        public DialogueNode GetStartNode() => GetNode(_startNodeId);

        public DialogueNode GetNode(string id)
        {
            if (_nodes == null) return default;
            foreach (var node in _nodes)
            {
                if (node.id == id)
                    return node;
            }
            return default;
        }

        public bool TryGetNode(string id, out DialogueNode node)
        {
            if (_nodes != null)
            {
                foreach (var n in _nodes)
                {
                    if (n.id == id)
                    {
                        node = n;
                        return true;
                    }
                }
            }
            node = default;
            return false;
        }
    }
}
