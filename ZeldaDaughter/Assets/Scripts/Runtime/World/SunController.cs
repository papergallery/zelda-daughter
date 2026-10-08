using UnityEngine;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// The sun follows the core's daylight share (WorldClock.Daylight). Placeholder look until R2-05 (light by style):
    /// night ambient and the low sun angle are constants here on purpose — R2-05 replaces this with data of the chosen style.
    /// </summary>
    public sealed class SunController : MonoBehaviour
    {
        [SerializeField] private Light _sun;
        [SerializeField] private Vector3 _dayRotation = new Vector3(50f, -30f, 0f);
        [SerializeField] private float _dayIntensity = 1f;
        [SerializeField] private Color _dayAmbient = Color.gray;

        private static readonly Color NightAmbient = new Color(0.10f, 0.13f, 0.21f);
        private const float LowSunPitch = 5f;
        private const float NightIntensityShare = 0.03f;

        /// <summary>Sun elevation angle, degrees (low at night).</summary>
        public float Pitch => _sun != null ? _sun.transform.eulerAngles.x : 0f;
        public float Intensity => _sun != null ? _sun.intensity : 0f;

        public void Configure(Light sun, Vector3 dayRotation, float dayIntensity, Color dayAmbient)
        {
            _sun = sun;
            _dayRotation = dayRotation;
            _dayIntensity = dayIntensity;
            _dayAmbient = dayAmbient;
        }

        public void Apply(float daylight)
        {
            if (_sun == null) return;
            daylight = Mathf.Clamp01(daylight);
            _sun.intensity = _dayIntensity * Mathf.Lerp(NightIntensityShare, 1f, daylight);
            _sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(LowSunPitch, _dayRotation.x, daylight), _dayRotation.y, _dayRotation.z);
            RenderSettings.ambientLight = Color.Lerp(NightAmbient, _dayAmbient, daylight);
        }
    }
}
