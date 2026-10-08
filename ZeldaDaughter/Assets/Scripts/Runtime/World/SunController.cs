using UnityEngine;

namespace ZeldaDaughter.World
{
    /// <summary>
    /// Light of the day (D-08), driven by the core's daylight share (WorldClock.Daylight, 0 night … 1 day) and by whether it is
    /// the evening ramp or the morning one (the same share, different colours): morning — rosy gold → white; day — warm white;
    /// evening — white → orange → dusk purple; night — a blue moon that is dim but never black, so the road and silhouettes read.
    /// Also ambient, fog (linear by eye depth — the toon shader reads it) and the camera background follow. The constants are
    /// the look, not balance; they are judged on the reference frames docs/demo/frames/D-08-*.png.
    /// </summary>
    public sealed class SunController : MonoBehaviour
    {
        [SerializeField] private Light _sun;
        [SerializeField] private Vector3 _dayRotation = new Vector3(50f, -30f, 0f);
        [SerializeField] private float _dayIntensity = 1f;
        [SerializeField] private Color _dayAmbient = Color.gray;
        [SerializeField] private Camera _camera;

        // Stops at daylight 0, 0.15, 0.4, 0.7, 1.
        private static readonly Color[] MorningSun =
        {
            new Color(0.50f, 0.60f, 0.95f), new Color(1.00f, 0.62f, 0.42f), new Color(1.00f, 0.78f, 0.55f),
            new Color(1.00f, 0.92f, 0.78f), new Color(1.00f, 0.96f, 0.88f),
        };
        private static readonly Color[] EveningSun =
        {
            new Color(0.50f, 0.60f, 0.95f), new Color(0.90f, 0.42f, 0.35f), new Color(1.00f, 0.58f, 0.28f),
            new Color(1.00f, 0.84f, 0.62f), new Color(1.00f, 0.96f, 0.88f),
        };
        private static readonly float[] IntensityShare = { 0.35f, 0.50f, 0.80f, 0.95f, 1f };
        private static readonly float[] Pitch_ = { 14f, 8f, 18f, 30f, 1f }; // last = the scene's day pitch (see Apply)
        private static readonly Color NightAmbient = new Color(0.30f, 0.36f, 0.58f);
        private static readonly Color MorningAmbientTint = new Color(1.10f, 0.95f, 0.92f);
        private static readonly Color EveningAmbientTint = new Color(1.10f, 0.88f, 0.90f);
        private static readonly Color NightFog = new Color(0.12f, 0.16f, 0.28f);
        private static readonly Color DayFog = new Color(0.80f, 0.76f, 0.64f);
        private static readonly Color MorningFog = new Color(0.88f, 0.74f, 0.64f);
        private static readonly Color EveningFog = new Color(0.82f, 0.58f, 0.46f);
        private const float FogStartDay = 34f, FogEndDay = 78f, FogStartNight = 26f, FogEndNight = 56f;

        private const float ShadowStrength = 0.8f;
        private static readonly int NightId = Shader.PropertyToID("_ZD_Night");

        /// <summary>Sun elevation angle, degrees (low at night).</summary>
        public float Pitch => _sun != null ? _sun.transform.eulerAngles.x : 0f;
        public Color Tint => _sun != null ? _sun.color : Color.black;
        public float Intensity => _sun != null ? _sun.intensity : 0f;

        public void Configure(Light sun, Vector3 dayRotation, float dayIntensity, Color dayAmbient)
        {
            _sun = sun;
            _dayRotation = dayRotation;
            _dayIntensity = dayIntensity;
            _dayAmbient = dayAmbient;
        }

        /// <summary>The camera whose background follows the fog colour (set by the scene builder).</summary>
        public void SetCamera(Camera camera) => _camera = camera;

        public void Apply(float daylight) => Apply(daylight, false);

        /// <param name="evening">true on the way down (dusk), false on the way up (dawn) — colours differ, the share does not.</param>
        public void Apply(float daylight, bool evening)
        {
            if (_sun == null) return;
            float d = Mathf.Clamp01(daylight);
            // D-21: the sun's shadows are soft (PCF) and not fully black whatever the scene config says ("hard" in region.json is the D-08 look).
            if (_sun.shadows != LightShadows.None) { _sun.shadows = LightShadows.Soft; _sun.shadowStrength = ShadowStrength; }
            // The post pass turns grade / paper / vignette blue with this (0 day … 1 night).
            Shader.SetGlobalFloat(NightId, 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(d / 0.3f)));
            var sunColors = evening ? EveningSun : MorningSun;

            _sun.color = Sample(sunColors, d);
            _sun.intensity = _dayIntensity * SampleF(IntensityShare, d);
            float dayPitch = _dayRotation.x;
            _sun.transform.rotation = Quaternion.Euler(PitchAt(d, dayPitch), _dayRotation.y, _dayRotation.z);

            float bell = 4f * d * (1f - d); // 0 at night and noon, 1 in the middle of dawn / dusk
            var tint = Color.Lerp(Color.white, evening ? EveningAmbientTint : MorningAmbientTint, bell);
            var ambient = Color.Lerp(NightAmbient, _dayAmbient, Mathf.SmoothStep(0f, 1f, d));
            RenderSettings.ambientLight = new Color(ambient.r * tint.r, ambient.g * tint.g, ambient.b * tint.b);

            var twilightFog = evening ? EveningFog : MorningFog;
            var fog = Color.Lerp(Color.Lerp(NightFog, twilightFog, Mathf.Clamp01(d * 4f)), DayFog, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.5f) * 2f)));
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fog;
            RenderSettings.fogStartDistance = Mathf.Lerp(FogStartNight, FogStartDay, d);
            RenderSettings.fogEndDistance = Mathf.Lerp(FogEndNight, FogEndDay, d);
            if (_camera != null) _camera.backgroundColor = fog;
        }

        private static float PitchAt(float d, float dayPitch)
        {
            int i = 0;
            while (i < 3 && d > StopAt[i + 1]) i++;
            float a = Pitch_[i];
            float b = i + 1 == 4 ? dayPitch : Pitch_[i + 1]; // the last stop is the scene's own day pitch
            return Mathf.Lerp(a, b, Mathf.InverseLerp(StopAt[i], StopAt[i + 1], d));
        }

        // Stops are at 0, 0.15, 0.4, 0.7, 1 — uneven on purpose (the interesting colours live near the horizon).
        private static readonly float[] StopAt = { 0f, 0.15f, 0.4f, 0.7f, 1f };

        private static Color Sample(Color[] stops, float d)
        {
            int i = 0;
            while (i < 3 && d > StopAt[i + 1]) i++;
            return Color.Lerp(stops[i], stops[i + 1], Mathf.InverseLerp(StopAt[i], StopAt[i + 1], d));
        }

        private static float SampleF(float[] stops, float d)
        {
            int i = 0;
            while (i < 3 && d > StopAt[i + 1]) i++;
            return Mathf.Lerp(stops[i], stops[i + 1], Mathf.InverseLerp(StopAt[i], StopAt[i + 1], d));
        }
    }
}
