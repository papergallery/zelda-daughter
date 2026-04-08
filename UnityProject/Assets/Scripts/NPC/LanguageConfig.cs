using UnityEngine;

namespace ZeldaDaughter.NPC
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/NPC/Language Config", fileName = "LanguageConfig")]
    public class LanguageConfig : ScriptableObject
    {
        [SerializeField] private string[] _glyphs = new[]
        {
            "ᚠ", "ᚢ", "ᚦ", "ᚨ", "ᚱ", "ᚲ", "ᚷ", "ᚹ", "ᚺ", "ᚾ",
            "ᛁ", "ᛃ", "ᛈ", "ᛊ", "ᛏ", "ᛒ", "ᛖ", "ᛗ", "ᛚ", "ᛞ"
        };

        /// <summary>Ниже порога — текст полностью скрэмблирован.</summary>
        [SerializeField] private float _scrambleThreshold = 0.3f;

        /// <summary>Между scramble и partial — частичное понимание.</summary>
        [SerializeField] private float _partialThreshold = 0.7f;

        /// <summary>Прирост понимания языка за каждую реплику NPC.</summary>
        [SerializeField] private float _experiencePerLine = 0.02f;

        /// <summary>Ниже порога — показывать только иконки, без текста.</summary>
        [SerializeField] private float _iconModeThreshold = 0.2f;

        /// <summary>Выше порога — игрок понимает монеты и ценообразование.</summary>
        [SerializeField] private float _currencyThreshold = 0.5f;

        public string[] Glyphs => _glyphs;
        public float ScrambleThreshold => _scrambleThreshold;
        public float PartialThreshold => _partialThreshold;
        public float ExperiencePerLine => _experiencePerLine;
        public float IconModeThreshold => _iconModeThreshold;
        public float CurrencyThreshold => _currencyThreshold;
    }
}
