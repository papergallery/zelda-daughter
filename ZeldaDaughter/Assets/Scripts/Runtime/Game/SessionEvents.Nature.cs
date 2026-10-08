using System;

namespace ZeldaDaughter.Game
{
    /// <summary>D-16: what the elements' presenter tells the others (the audio's rain, a hint…). The core's own world events stay on <c>World</c>.</summary>
    public sealed partial class SessionEvents
    {
        /// <summary>Rain began / stopped as the scene shows it (NatureFx raises it, once per change, also after a load or a time jump).</summary>
        public event Action<bool> RainChanged;
        public void RaiseRainChanged(bool raining) => RainChanged?.Invoke(raining);
    }
}
