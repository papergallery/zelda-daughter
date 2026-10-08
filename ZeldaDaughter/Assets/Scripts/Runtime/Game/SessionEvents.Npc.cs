using System;

namespace ZeldaDaughter.Game
{
    public sealed partial class SessionEvents
    {
        /// <summary>
        /// A resident made the gesture of the line she just said (D-12): <c>type</c> is «point», «shrug», «wave»…, <c>targetId</c> the scene object she
        /// points at (may be empty). NpcPresenter turns her toward it; audio or hints may listen.
        /// </summary>
        public event Action<string, string, string> NpcGesture;
        public void RaiseNpcGesture(string npcId, string type, string targetId) => NpcGesture?.Invoke(npcId, type, targetId);
    }
}
