#nullable enable
using System;
using ZeldaDaughter.Core.Inventory;

namespace ZeldaDaughter.Core.World
{
    public enum TorchEvent { None, BurntOut }

    /// <summary>
    /// The hero's torch (D-23). The core has no «in hand»: a lit torch in the bag is the torch in hand. It burns for
    /// <see cref="CampSettings.TorchBurnSeconds"/> of real time (faster in the rain), weakens over the last
    /// <see cref="CampSettings.TorchFadeSeconds"/> and is gone: the torch leaves the bag and a burnt stick takes its place.
    /// A new torch (the bag had none) starts full, unless the same one was put down and picked up again — then it goes on from where it was.
    /// </summary>
    public sealed class Torch
    {
        public const string Item = "torch";

        readonly CampSettings _s;
        readonly Bag _bag;
        float _left;

        public Torch(CampSettings settings, Bag bag)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _bag = bag ?? throw new ArgumentNullException(nameof(bag));
        }

        /// <summary>The hero carries a lit torch.</summary>
        public bool IsLit => _bag.Count(Item) > 0;

        /// <summary>Seconds of burning left; for a torch not yet started — the whole time. 0 without a torch.</summary>
        public float Left => !IsLit ? 0f : _left > 0f ? _left : _s.TorchBurnSeconds;

        /// <summary>0..1 for the view's light: full until the last seconds, then fading; 0 with no torch.</summary>
        public float Light
        {
            get
            {
                if (!IsLit) return 0f;
                float left = Left;
                return _s.TorchFadeSeconds <= 0f ? 1f : Math.Min(1f, left / _s.TorchFadeSeconds);
            }
        }

        /// <summary>For a save: the seconds left of the torch that was lit, 0 if none was started.</summary>
        public float Saved => _left;

        public void Restore(float left) => _left = Math.Max(0f, Math.Min(_s.TorchBurnSeconds, left));

        /// <summary>The torch burns for <paramref name="dt"/>; <see cref="TorchEvent.BurntOut"/> when it was the last second.</summary>
        public TorchEvent Tick(float dt, bool raining)
        {
            if (dt <= 0f || !IsLit) return TorchEvent.None;
            if (_left <= 0f) _left = _s.TorchBurnSeconds;
            _left -= dt * (raining ? _s.TorchRainBurnFactor : 1f);
            if (_left > 0f) return TorchEvent.None;
            _left = 0f;
            _bag.Remove(Item);
            if (!string.IsNullOrEmpty(_s.BurntItem)) _bag.Add(_s.BurntItem);
            return TorchEvent.BurntOut;
        }
    }
}
