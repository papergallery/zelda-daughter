using UnityEngine;

namespace ZeldaDaughter.World
{
    public class WaterAreaMarker : MonoBehaviour
    {
        [SerializeField] private string _waterType;
        [SerializeField] private float _radius;
        [SerializeField] private float _depth;

        public string WaterType => _waterType;
        public float Radius => _radius;
        public float Depth => _depth;

        public void Setup(string waterType, float radius, float depth)
        {
            _waterType = waterType;
            _radius = radius;
            _depth = depth;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.5f);
            if (_radius > 0)
                Gizmos.DrawSphere(transform.position, _radius);
        }
#endif
    }
}
