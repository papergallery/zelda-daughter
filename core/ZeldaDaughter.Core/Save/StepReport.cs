#nullable enable
using System.Collections.Generic;
using ZeldaDaughter.Core.Condition;
using ZeldaDaughter.Core.Progression;
using ZeldaDaughter.Core.World;

namespace ZeldaDaughter.Core.Save
{
    /// <summary>
    /// What one <see cref="GameState.Step"/> did. The view keeps ONE instance and passes it every frame: the lists are cleared by the next
    /// call and reused, so an ordinary frame allocates nothing (C8). Read them at once; do not keep them.
    /// </summary>
    public sealed class StepReport
    {
        public List<ClockEvent> ClockEvents { get; } = new List<ClockEvent>(4);
        public List<ConditionEvent> ConditionEvents { get; } = new List<ConditionEvent>(4);
        public List<SkillChange> SkillChanges { get; } = new List<SkillChange>(4);

        public bool IsEmpty => ClockEvents.Count == 0 && ConditionEvents.Count == 0 && SkillChanges.Count == 0;

        public void Clear()
        {
            ClockEvents.Clear();
            ConditionEvents.Clear();
            SkillChanges.Clear();
        }
    }
}
