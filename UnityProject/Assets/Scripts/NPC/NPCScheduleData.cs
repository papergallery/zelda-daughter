using System;
using System.Collections.Generic;
using UnityEngine;
using ZeldaDaughter.World;

namespace ZeldaDaughter.NPC
{
    [Serializable]
    public struct ScheduleEntry
    {
        public TimeOfDay time;
        public string waypointId;
        public NPCActivity activity;
    }

    [CreateAssetMenu(menuName = "ZeldaDaughter/NPC/Schedule", fileName = "NewNPCSchedule")]
    public class NPCScheduleData : ScriptableObject
    {
        [SerializeField] private ScheduleEntry[] _entries;

        public IReadOnlyList<ScheduleEntry> Entries => _entries;

        /// <summary>Returns the last entry whose time is <= the given time. Falls back to last entry.</summary>
        public ScheduleEntry GetCurrentEntry(TimeOfDay time)
        {
            if (_entries == null || _entries.Length == 0)
                return default;

            ScheduleEntry result = _entries[0];
            foreach (var entry in _entries)
            {
                if (entry.time <= time)
                    result = entry;
            }
            return result;
        }
    }
}
