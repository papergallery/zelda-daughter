using UnityEngine;
using ZeldaDaughter.UI;

namespace ZeldaDaughter.Progression
{
    public class ProgressionFeedback : MonoBehaviour
    {
        [SerializeField] private StatEffectConfig _effectConfig;
        [SerializeField] private Animator _animator;

        [SerializeField] private string[] _tierReplicas = new string[]
        {
            // Strength (0–3)
            "",
            "Удары стали увереннее...",
            "Чувствую силу в руках",
            "Эта мощь... она моя",

            // Toughness (4–7)
            "",
            "Боль уже не так пугает...",
            "Тело привыкает к ударам",
            "Я выдержу что угодно",

            // Agility (8–11)
            "",
            "Руки двигаются быстрее...",
            "Движения становятся плавнее",
            "Клинок словно часть меня",

            // Accuracy (12–15)
            "",
            "Рука уже не дрожит...",
            "Вижу куда целить",
            "Каждый удар находит цель",

            // Endurance (16–19)
            "",
            "Дорога уже не так утомляет...",
            "Ноги несут легко",
            "Могу идти бесконечно",

            // CarryCapacity (20–23)
            "",
            "Привыкаю к тяжести...",
            "Ноша кажется легче",
            "Тащу как вол и не устаю",
        };

        private float _lastReplicaTime = -999f;

        private void OnEnable()
        {
            PlayerStats.OnTierReached += HandleTierReached;
            PlayerStats.OnStatChanged += HandleStatChanged;
        }

        private void OnDisable()
        {
            PlayerStats.OnTierReached -= HandleTierReached;
            PlayerStats.OnStatChanged -= HandleStatChanged;
        }

        private void HandleTierReached(StatType type, int tierIndex)
        {
            if (Time.time - _lastReplicaTime < _effectConfig.TierReplicaCooldown) return;

            int index = (int)type * 4 + tierIndex;
            if (index < 0 || index >= _tierReplicas.Length) return;

            string replica = _tierReplicas[index];
            if (string.IsNullOrEmpty(replica)) return;

            SpeechBubbleManager.Say(replica);
            _lastReplicaTime = Time.time;
        }

        private void HandleStatChanged(StatType type, float oldVal, float newVal)
        {
            if (type != StatType.Agility || _animator == null) return;

            _animator.SetFloat("AttackSpeed", 1f + (newVal / 100f) * _effectConfig.MaxAttackSpeedBonus);
        }
    }
}
