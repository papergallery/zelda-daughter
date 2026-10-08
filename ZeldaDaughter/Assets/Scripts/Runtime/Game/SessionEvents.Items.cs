using System;
using ZeldaDaughter.Core.Journal;

namespace ZeldaDaughter.Game
{
    /// <summary>D-14: what the radial menu and the item drag tell the other packages (docs/demo/decisions.md).</summary>
    public sealed partial class SessionEvents
    {
        /// <summary>The hero chose a sector of the radial menu: <c>bag</c>, <c>map</c> or <c>notebook</c>. The bag opens by itself; the map and notebook windows (D-15) listen.</summary>
        public event Action<string> RadialChosen;
        public void RaiseRadialChosen(string id) => RadialChosen?.Invoke(id);

        /// <summary>An item was handed to a resident who asked for it (<c>g.Quests.Give</c> returned Done). The talk presenter (D-12) may answer with the thanks node (<c>QuestResult.Thanks</c>).</summary>
        public event Action<string, QuestResult> QuestGiven;
        public void RaiseQuestGiven(string npcId, QuestResult result) => QuestGiven?.Invoke(npcId, result);
    }
}
