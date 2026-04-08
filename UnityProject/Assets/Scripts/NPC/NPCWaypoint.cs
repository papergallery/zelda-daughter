using System.Collections.Generic;
using UnityEngine;

namespace ZeldaDaughter.NPC
{
    /// <summary>
    /// Marker placed on scene objects. NPC scheduler looks up waypoints by string ID.
    /// </summary>
    public class NPCWaypoint : MonoBehaviour
    {
        [SerializeField] private string _waypointId;
        [SerializeField] private NPCActivity _defaultActivity = NPCActivity.Idle;

        public string WaypointId => _waypointId;
        public NPCActivity DefaultActivity => _defaultActivity;

        private static readonly Dictionary<string, NPCWaypoint> _registry = new();

        /// <summary>Returns waypoint by ID, or null if not found/not active.</summary>
        public static NPCWaypoint Find(string id)
        {
            return _registry.TryGetValue(id, out var wp) ? wp : null;
        }

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(_waypointId))
                _registry[_waypointId] = this;
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(_waypointId))
                _registry.Remove(_waypointId);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.3f);
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.5f, _waypointId);
        }
#endif
    }
}
