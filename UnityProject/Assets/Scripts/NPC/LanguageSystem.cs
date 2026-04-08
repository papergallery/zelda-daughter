using System;
using UnityEngine;
using ZeldaDaughter.Progression;
using ZeldaDaughter.Save;

namespace ZeldaDaughter.NPC
{
    public class LanguageSystem : MonoBehaviour, ISaveable
    {
        [SerializeField] private LanguageConfig _config;
        [SerializeField] private PlayerStats _playerStats;

        private float _comprehension = 0f;
        private TextScrambler _scrambler;

        public static event Action<float> OnComprehensionChanged;

        public float Comprehension => _comprehension;
        public bool IsIconMode => _comprehension < _config.IconModeThreshold;
        public bool KnowsCurrency => _comprehension >= _config.CurrencyThreshold;

        public string SaveId => "language_system";

        private void Awake()
        {
            _scrambler = new TextScrambler(_config.Glyphs);
        }

        private void OnEnable()
        {
            SaveManager.Register(this);
        }

        private void OnDisable()
        {
            SaveManager.Unregister(this);
        }

        public string ProcessText(string original)
        {
            return _scrambler.Scramble(original, _comprehension, _config.ScrambleThreshold, _config.PartialThreshold);
        }

        public void AddDialogueExperience()
        {
            float oldComp = _comprehension;
            _comprehension = Mathf.Clamp01(_comprehension + _config.ExperiencePerLine);

            if (_playerStats != null)
                _playerStats.AddExperience(StatType.Language, 1f);

            if (!Mathf.Approximately(_comprehension, oldComp))
                OnComprehensionChanged?.Invoke(_comprehension);
        }

        public static void ClearEvents()
        {
            OnComprehensionChanged = null;
        }

        // --- ISaveable ---

        [Serializable]
        private struct SaveData
        {
            public float Comprehension;
        }

        public object CaptureState()
        {
            return new SaveData { Comprehension = _comprehension };
        }

        public void RestoreState(object state)
        {
            if (state is not SaveData data) return;
            _comprehension = Mathf.Clamp01(data.Comprehension);
        }
    }
}
