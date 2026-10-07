using UnityEngine;

namespace ZeldaDaughter.World
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Campfire Config", fileName = "CampfireConfig")]
    public class CampfireConfig : ScriptableObject
    {
        [Header("Light")]
        [SerializeField] private Color _lightColor = new(1f, 0.7f, 0.3f);
        [SerializeField] private float _lightIntensity = 2f;
        [SerializeField] private float _lightRange = 8f;

        [Header("Rest Zone")]
        [SerializeField] private float _restZoneRadius = 3f;

        public Color LightColor => _lightColor;
        public float LightIntensity => _lightIntensity;
        public float LightRange => _lightRange;
        public float RestZoneRadius => _restZoneRadius;
    }
}
