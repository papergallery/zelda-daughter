using UnityEngine;
using UnityEngine.AI;
using ZeldaDaughter.World;

namespace ZeldaDaughter.NPC
{
    /// <summary>
    /// Moves NPC to waypoints according to their schedule when time of day changes.
    /// </summary>
    public class NPCScheduler : MonoBehaviour
    {
        [SerializeField] private NPCProfile _profile;
        [SerializeField] private NavMeshAgent _agent;
        [SerializeField] private Animator _animator;
        [SerializeField] private float _stoppingDistance = 0.5f;

        private NPCActivity _currentActivity = NPCActivity.Idle;
        private bool _isMoving;

        /// <summary>False while sleeping or mid-walk, so NPCInteractable can gate interaction.</summary>
        public bool IsAvailable => _currentActivity != NPCActivity.Sleeping && !_isMoving;
        public NPCActivity CurrentActivity => _currentActivity;

        private void Awake()
        {
            if (_agent == null)
                TryGetComponent(out _agent);

            if (_animator == null)
                TryGetComponent(out _animator);

            if (_agent != null)
                _agent.stoppingDistance = _stoppingDistance;
        }

        private void OnEnable()
        {
            DayNightCycle.OnTimeOfDayChanged += HandleTimeChanged;
        }

        private void OnDisable()
        {
            DayNightCycle.OnTimeOfDayChanged -= HandleTimeChanged;
        }

        private void HandleTimeChanged(TimeOfDay newTime)
        {
            if (_profile == null || _profile.Schedule == null)
                return;

            var entry = _profile.Schedule.GetCurrentEntry(newTime);

            if (string.IsNullOrEmpty(entry.waypointId))
                return;

            var waypoint = NPCWaypoint.Find(entry.waypointId);
            if (waypoint == null)
            {
                Debug.LogWarning($"[NPCScheduler] Waypoint '{entry.waypointId}' not found for NPC '{name}'");
                return;
            }

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.SetDestination(waypoint.transform.position);
                _isMoving = true;
            }

            _currentActivity = entry.activity;
        }

        private void Update()
        {
            if (_agent != null && _isMoving)
            {
                bool arrived = !_agent.pathPending
                    && _agent.remainingDistance <= _stoppingDistance
                    && _agent.pathStatus == NavMeshPathStatus.PathComplete;

                if (arrived)
                    _isMoving = false;
            }

            if (_animator != null)
                _animator.SetBool("IsWalking", _isMoving);
        }
    }
}
