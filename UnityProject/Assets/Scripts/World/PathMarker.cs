using UnityEngine;

namespace ZeldaDaughter.World
{
    public class PathMarker : MonoBehaviour
    {
        [SerializeField] private string _pathType;
        [SerializeField] private float _width;

        public string PathType => _pathType;
        public float Width => _width;

        public void Setup(string pathType, float width)
        {
            _pathType = pathType;
            _width = width;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _pathType == "road" ? Color.yellow : Color.green;
            for (int i = 0; i < transform.childCount - 1; i++)
            {
                var a = transform.GetChild(i).position;
                var b = transform.GetChild(i + 1).position;
                Gizmos.DrawLine(a, b);
            }
        }
#endif
    }
}
