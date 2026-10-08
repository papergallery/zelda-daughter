using System;

namespace ZeldaDaughter.Game
{
    /// <summary>D-13: what the fight adds to the bus (the rest of it — Enemy, HeroStruck, Looted, CarcassGone — is declared in SessionEvents.cs).</summary>
    public sealed partial class SessionEvents
    {
        /// <summary>The player tapped an enemy that is out of reach: the hero turned to it and did not strike (and does not walk).</summary>
        public event Action<string> EnemyOutOfRange;
        public void RaiseEnemyOutOfRange(string enemyId) => EnemyOutOfRange?.Invoke(enemyId);
    }
}
