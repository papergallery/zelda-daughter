using System;
using UnityEngine;

namespace ZeldaDaughter.Game
{
    public sealed partial class SessionEvents
    {
        /// <summary>
        /// D-11: a foot of the hero came down — where, and whether she limps (a fracture). Fired by HeroView once per half-stride of the
        /// walking cycle (the path walked, not the clock), so the footsteps of Audio keep the rhythm of the picture; a limp makes it uneven.
        /// </summary>
        public event Action<Vector3, bool> HeroStep;
        public void RaiseHeroStep(Vector3 position, bool limping) => HeroStep?.Invoke(position, limping);

        /// <summary>D-11: the hero fell (true) or got up (false) — the screen goes dark with glimpses, Audio may play a thud.</summary>
        public event Action<bool> HeroDown;
        public void RaiseHeroDown(bool down) => HeroDown?.Invoke(down);
    }
}
