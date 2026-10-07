#nullable enable
using System;
using System.Collections.Generic;

namespace ZeldaDaughter.Core.Condition
{
    /// <summary>data/hunger.json (C-08).</summary>
    public sealed class HungerSettings
    {
        public float SecondsToFull { get; set; }
        public float PeckishAt { get; set; }
        public float HungryAt { get; set; }
        public float StarvingAt { get; set; }
        /// <summary>Multiplier of every characteristic when completely starved (§6 «деградация всех характеристик»).</summary>
        public float MultiplierAtFull { get; set; }
        public float Start { get; set; }
        public Dictionary<string, FoodSettings> Food { get; set; } = new Dictionary<string, FoodSettings>();
    }

    public sealed class FoodSettings
    {
        public float Satiety { get; set; }
        public float Heal { get; set; }
    }

    public enum HungerLevel { Fed, Peckish, Hungry, Starving }

    public readonly struct EatEffect
    {
        public readonly bool Eaten;
        public readonly float Heal;

        public EatEffect(bool eaten, float heal) { Eaten = eaten; Heal = heal; }
    }

    /// <summary>Hidden hunger 0 (fed) … 1 (starving); past the threshold everything slowly degrades (project-design.md §6).</summary>
    public sealed class Hunger
    {
        readonly HungerSettings _s;

        public Hunger(HungerSettings settings)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            if (settings.SecondsToFull <= 0) throw new ArgumentException("SecondsToFull must be positive", nameof(settings));
            Value = settings.Start;
        }

        public float Value { get; private set; }

        public HungerLevel Level =>
            Value >= _s.StarvingAt ? HungerLevel.Starving :
            Value >= _s.HungryAt ? HungerLevel.Hungry :
            Value >= _s.PeckishAt ? HungerLevel.Peckish : HungerLevel.Fed;

        /// <summary>Applied to speed, attack, accuracy and healing by the systems that read it.</summary>
        public float Multiplier
        {
            get
            {
                if (Value <= _s.HungryAt) return 1f;
                float t = (Value - _s.HungryAt) / Math.Max(1f - _s.HungryAt, 1e-6f);
                return 1f + (_s.MultiplierAtFull - 1f) * Math.Min(t, 1f);
            }
        }

        public void Advance(float realSeconds)
        {
            if (realSeconds > 0) Value = Math.Min(1f, Value + realSeconds / _s.SecondsToFull);
        }

        /// <summary>Drag food onto the hero (§6). The heal goes to HeroCondition.Heal.</summary>
        public EatEffect Eat(string itemId)
        {
            if (!_s.Food.TryGetValue(itemId, out var food)) return new EatEffect(false, 0);
            Value = Math.Max(0f, Value - food.Satiety);
            return new EatEffect(true, food.Heal);
        }

        public void Restore(float value) => Value = Math.Max(0f, Math.Min(1f, value));
    }
}
