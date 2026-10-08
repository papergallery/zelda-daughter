#nullable enable
using System;
using System.Collections.Generic;
using ZeldaDaughter.Core.World;

namespace ZeldaDaughter.Core.Npcs
{
    /// <summary>Where an NPC is and what she is doing at some moment.</summary>
    public readonly struct NpcSlot : IEquatable<NpcSlot>
    {
        public readonly string Anchor;
        public readonly NpcActivity Activity;
        /// <summary>Game hour this slot started at.</summary>
        public readonly double StartHour;

        public NpcSlot(string anchor, NpcActivity activity, double startHour) { Anchor = anchor; Activity = activity; StartHour = startHour; }
        public bool Equals(NpcSlot o) => Anchor == o.Anchor && Activity == o.Activity && StartHour == o.StartHour;
        public override bool Equals(object? obj) => obj is NpcSlot s && Equals(s);
        public override int GetHashCode() => unchecked((Anchor.GetHashCode() * 31 + (int)Activity) * 31 + StartHour.GetHashCode());
        public override string ToString() => $"{Activity}@{Anchor}";
    }

    /// <summary>An NPC's slot changed (or she was placed for the first time): the view walks her from <see cref="FromAnchor"/> to <see cref="ToAnchor"/>.</summary>
    public readonly struct NpcChange
    {
        public readonly string NpcId;
        /// <summary>Anchor of the previous slot; null on the first <see cref="NpcRoster.Sync"/> — put her straight at <see cref="ToAnchor"/>, no walk.</summary>
        public readonly string? FromAnchor;
        public readonly string ToAnchor;
        public readonly NpcActivity Activity;
        public readonly NpcActivity? PreviousActivity;

        public NpcChange(string npcId, string? from, string to, NpcActivity activity, NpcActivity? previous)
        {
            NpcId = npcId; FromAnchor = from; ToAnchor = to; Activity = activity; PreviousActivity = previous;
        }
        /// <summary>The anchor stayed the same (the herbalist goes from work to bed in her own house): only the activity changes.</summary>
        public bool Walks => FromAnchor != null && FromAnchor != ToAnchor;
        public override string ToString() => $"{NpcId}: {FromAnchor ?? "-"} → {ToAnchor} ({Activity})";
    }

    /// <summary>
    /// Who is where (project-design.md §2 «NPC живут по расписанию»). Stateless in the world sense — the slot is a function of the clock,
    /// so nothing is saved: after a load or a sleep the NPCs simply are where the time says. <see cref="Sync"/> turns clock moves into
    /// events for the view.
    /// </summary>
    public sealed class NpcRoster
    {
        readonly NpcSettings _s;
        readonly WorldClock _clock;
        readonly Dictionary<string, NpcSlot> _last = new Dictionary<string, NpcSlot>(StringComparer.Ordinal);

        public NpcRoster(NpcSettings settings, WorldClock clock)
        {
            _s = settings ?? throw new ArgumentNullException(nameof(settings));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <summary>Walking speed of a resident, m/s (npcs.json walkSpeed): the view moves her along the route with it.</summary>
        public float WalkSpeed(string npcId) => Def(npcId) != null ? _s.WalkSpeed : 0f;

        public IEnumerable<string> Ids => _s.Npcs.Keys;
        public NpcDef Def(string npcId) => _s.Npcs.TryGetValue(npcId, out var d) ? d : throw new ArgumentException($"npcs.json: no npc '{npcId}'", nameof(npcId));

        /// <summary>The slot now.</summary>
        public NpcSlot Slot(string npcId) => SlotAt(npcId, _clock.TimeOfDay);

        /// <summary>The slot at a time of day (0..1). The last entry of the list continues over midnight.</summary>
        public NpcSlot SlotAt(string npcId, double timeOfDay)
        {
            var list = Def(npcId).Schedule;
            if (list.Count == 0) throw new InvalidOperationException($"npcs.json: '{npcId}' has no schedule");
            double hour = timeOfDay * 24.0;
            var pick = list[list.Count - 1];
            foreach (var e in list)
            {
                if (e.Hour <= hour) pick = e; else break;
            }
            return new NpcSlot(pick.Anchor, pick.ParsedActivity ?? NpcActivity.Stroll, pick.Hour);
        }

        /// <summary>The shop is open: she has one and stands at it selling (§2 «магазины закрыты» ночью; in the tavern in the evening — closed).</summary>
        public bool IsShopOpen(string npcId) => _s.Npcs.TryGetValue(npcId, out var d) && d.Shop && Slot(npcId).Activity == NpcActivity.Trade;

        /// <summary>
        /// Call after the clock moved (each frame, after sleep). Returns one change for every NPC whose slot differs from the last call —
        /// everyone on the first call. If time jumped over several slots (sleep), the change goes straight to the final one.
        /// </summary>
        public IReadOnlyList<NpcChange> Sync()
        {
            List<NpcChange>? changes = null;
            foreach (var id in _s.Npcs.Keys)
            {
                var now = Slot(id);
                if (_last.TryGetValue(id, out var was))
                {
                    if (was.Equals(now)) continue;
                    (changes ??= new List<NpcChange>()).Add(new NpcChange(id, was.Anchor, now.Anchor, now.Activity, was.Activity));
                }
                else (changes ??= new List<NpcChange>()).Add(new NpcChange(id, null, now.Anchor, now.Activity, null));
                _last[id] = now;
            }
            return changes ?? (IReadOnlyList<NpcChange>)NoChanges;   // nothing moved: a shared empty list (C8)
        }

        static readonly NpcChange[] NoChanges = new NpcChange[0];
    }
}
