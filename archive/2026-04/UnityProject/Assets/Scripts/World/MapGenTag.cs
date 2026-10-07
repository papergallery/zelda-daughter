using UnityEngine;

namespace ZeldaDaughter.World
{
    public class MapGenTag : MonoBehaviour
    {
        [SerializeField] private string[] _tags;

        public string[] Tags => _tags;

        public bool HasTag(string tag)
        {
            if (_tags == null) return false;
            foreach (var t in _tags)
                if (t == tag) return true;
            return false;
        }

        public void SetTags(string[] tags) => _tags = tags;
    }
}
