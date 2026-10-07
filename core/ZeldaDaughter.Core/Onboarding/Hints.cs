#nullable enable
using System;
using System.Collections.Generic;

namespace ZeldaDaughter.Core.Onboarding
{
    /// <summary>data/onboarding.json (C-14).</summary>
    public sealed class OnboardingSettings
    {
        public List<HintDef> Hints { get; set; } = new List<HintDef>();
    }

    public sealed class HintDef
    {
        public string Id { get; set; } = "";
        /// <summary>"start" or a condition the game sets (tappable_nearby, has_item).</summary>
        public string ShowWhen { get; set; } = "start";
        /// <summary>The action that completes it (swipe, tap, long_press_hero).</summary>
        public string DoneBy { get; set; } = "";
    }

    /// <summary>
    /// Non-blocking hints (project-design.md §6): one at a time, in order, shown while its condition holds and gone for good
    /// once the action is done — even if done before the hint ever appeared.
    /// </summary>
    public sealed class Hints
    {
        readonly OnboardingSettings _s;
        readonly HashSet<string> _done = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _conditions = new HashSet<string>(StringComparer.Ordinal) { "start" };

        public Hints(OnboardingSettings settings) { _s = settings ?? throw new ArgumentNullException(nameof(settings)); }

        /// <summary>Id of the hint to show now, or null.</summary>
        public string? Visible
        {
            get
            {
                foreach (var h in _s.Hints)
                {
                    if (_done.Contains(h.Id)) continue;
                    return _conditions.Contains(h.ShowWhen) ? h.Id : null; // strictly in order
                }
                return null;
            }
        }

        public bool AllDone => _done.Count >= _s.Hints.Count;
        public IReadOnlyCollection<string> Done => _done;

        /// <summary>The player did an action (swipe, tap, long_press_hero).</summary>
        public void Did(string action)
        {
            foreach (var h in _s.Hints) if (h.DoneBy == action) _done.Add(h.Id);
        }

        /// <summary>The game reports a condition (tappable_nearby, has_item).</summary>
        public void Set(string condition, bool value)
        {
            if (value) _conditions.Add(condition); else _conditions.Remove(condition);
        }

        public void Restore(IEnumerable<string> done)
        {
            _done.Clear();
            foreach (var d in done) _done.Add(d);
        }
    }
}
