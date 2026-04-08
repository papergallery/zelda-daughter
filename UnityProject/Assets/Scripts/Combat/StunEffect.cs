using UnityEngine;

namespace ZeldaDaughter.Combat
{
    /// <summary>Компонент оглушения на враге. Добавляется динамически при ударе молотом.</summary>
    public class StunEffect : MonoBehaviour
    {
        private float _remainingDuration;
        private EnemyFSM _fsm;
        private bool _isStunned;

        private void Awake()
        {
            _fsm = GetComponent<EnemyFSM>();
        }

        /// <summary>Применить или продлить оглушение. Повторный вызов обновляет длительность.</summary>
        public void Apply(float duration)
        {
            _remainingDuration = duration;
            _isStunned = true;

            if (_fsm != null)
                _fsm.ForceStun(duration);
        }

        private void Update()
        {
            if (!_isStunned) return;

            _remainingDuration -= Time.deltaTime;

            if (_remainingDuration <= 0f)
            {
                _isStunned = false;
                // FSM выйдет из Stagger сам через UpdateStagger → SetState(Chase)
                // Здесь только сбрасываем флаг компонента
            }
        }
    }
}
