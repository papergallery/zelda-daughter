using UnityEngine;

namespace ZeldaDaughter.Combat
{
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyFSM : MonoBehaviour
    {
        [SerializeField] private EnemyData _data;

        private EnemyState _state = EnemyState.Idle;
        private EnemyHealth _health;
        private EnemyAttackSignal _attackSignal;
        private Transform _player;
        private Vector3 _spawnPoint;
        private Vector3 _wanderTarget;
        private float _stateTimer;
        private float _attackTimer;
        private Animator _animator;
        private bool _isAggro;
        private bool _inWindup;
        private float _stunOverrideDuration;

        private static readonly int AnimSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimAttack = Animator.StringToHash("Attack");
        private static readonly int AnimHit = Animator.StringToHash("Hit");
        private static readonly int AnimDefeat = Animator.StringToHash("Defeat");

        public EnemyState CurrentState => _state;
        public EnemyData Data => _data;

        private void Awake()
        {
            _health = GetComponent<EnemyHealth>();
            _animator = GetComponentInChildren<Animator>();
            _attackSignal = GetComponentInChildren<EnemyAttackSignal>();
            _spawnPoint = transform.position;
        }

        private void Start()
        {
            var playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null) _player = playerObj.transform;
        }

        private void OnEnable()
        {
            EnemyHealth.OnDeath += HandleDeath;
            EnemyHealth.OnStagger += HandleStagger;
            EnemyHealth.OnDamaged += HandleDamaged;
        }

        private void OnDisable()
        {
            EnemyHealth.OnDeath -= HandleDeath;
            EnemyHealth.OnStagger -= HandleStagger;
            EnemyHealth.OnDamaged -= HandleDamaged;
        }

        private void Update()
        {
            if (_data == null || _player == null) return;

            _stateTimer += Time.deltaTime;
            _attackTimer += Time.deltaTime;

            float distToPlayer = Vector3.Distance(transform.position, _player.position);

            switch (_state)
            {
                case EnemyState.Idle:
                    UpdateIdle(distToPlayer);
                    break;
                case EnemyState.Wander:
                    UpdateWander(distToPlayer);
                    break;
                case EnemyState.Alert:
                    UpdateAlert(distToPlayer);
                    break;
                case EnemyState.Chase:
                    UpdateChase(distToPlayer);
                    break;
                case EnemyState.Attack:
                    UpdateAttack(distToPlayer);
                    break;
                case EnemyState.Stagger:
                    UpdateStagger();
                    break;
                case EnemyState.Flee:
                    UpdateFlee(distToPlayer);
                    break;
            }
        }

        private void SetState(EnemyState newState)
        {
            _state = newState;
            _stateTimer = 0f;

            if (newState == EnemyState.Attack)
            {
                _inWindup = true;
                _attackSignal?.ShowWindup(_data.WindupTime);
            }
        }

        private void UpdateIdle(float distToPlayer)
        {
            SetAnimSpeed(0f);

            if (CheckAggro(distToPlayer))
            {
                SetState(EnemyState.Alert);
                return;
            }

            if (_stateTimer > Random.Range(2f, 5f))
            {
                PickWanderTarget();
                SetState(EnemyState.Wander);
            }
        }

        private void UpdateWander(float distToPlayer)
        {
            if (CheckAggro(distToPlayer))
            {
                SetState(EnemyState.Alert);
                return;
            }

            MoveToward(_wanderTarget, _data.MoveSpeed);
            float distToTarget = Vector3.Distance(transform.position, _wanderTarget);

            if (distToTarget < 1f || _stateTimer > 10f)
                SetState(EnemyState.Idle);
        }

        private void UpdateAlert(float distToPlayer)
        {
            LookAt(_player.position);
            SetAnimSpeed(0f);

            if (_stateTimer > 0.5f)
                SetState(EnemyState.Chase);
        }

        private void UpdateChase(float distToPlayer)
        {
            if (distToPlayer <= _data.AttackRange)
            {
                SetState(EnemyState.Attack);
                return;
            }

            if (distToPlayer > _data.AggroRange * 2f)
            {
                _isAggro = false;
                SetState(EnemyState.Idle);
                return;
            }

            MoveToward(_player.position, _data.ChaseSpeed);
        }

        private void UpdateAttack(float distToPlayer)
        {
            LookAt(_player.position);
            SetAnimSpeed(0f);

            if (_inWindup)
            {
                // Фаза 1: Windup — ждём WindupTime, враг замирает с визуальным сигналом
                if (_stateTimer >= _data.WindupTime)
                {
                    _inWindup = false;
                    // Фаза 2: Strike — проверяем дистанцию, мог ли игрок уклониться
                    if (distToPlayer <= _data.AttackRange)
                        PerformAttack();
                    // else — промах: игрок отошёл за время windup

                    _attackTimer = 0f;
                }
                return;
            }

            // Ожидание кулдауна перед следующим замахом
            if (distToPlayer > _data.AttackRange * 1.5f)
            {
                SetState(EnemyState.Chase);
                return;
            }

            if (_attackTimer >= _data.AttackCooldown)
                SetState(EnemyState.Attack); // перезапускает windup через SetState
        }

        private void UpdateStagger()
        {
            SetAnimSpeed(0f);

            float duration = _stunOverrideDuration > 0f
                ? _stunOverrideDuration
                : (_data != null ? _data.StaggerDuration : 1f);

            if (_stateTimer >= duration)
            {
                _stunOverrideDuration = 0f;
                SetState(EnemyState.Chase);
            }
        }

        private void UpdateFlee(float distToPlayer)
        {
            var awayDir = (transform.position - _player.position).normalized;
            MoveToward(transform.position + awayDir * 5f, _data.ChaseSpeed);

            if (distToPlayer > _data.AggroRange * 2f)
                SetState(EnemyState.Idle);
        }

        private bool CheckAggro(float distToPlayer)
        {
            if (_isAggro) return true;

            if (_data.AggroOnSight && distToPlayer <= _data.AggroRange)
            {
                _isAggro = true;
                return true;
            }

            return false;
        }

        protected virtual void PerformAttack()
        {
            if (_animator != null)
                _animator.SetTrigger(AnimAttack);

            if (_player.TryGetComponent<IDamageable>(out var damageable) && damageable.IsAlive)
            {
                var info = new DamageInfo(
                    _data.Damage,
                    _data.InflictedWoundType,
                    _data.WoundSeverity,
                    gameObject
                );
                damageable.TakeDamage(info);
            }
        }

        private void MoveToward(Vector3 target, float speed)
        {
            var dir = (target - transform.position).normalized;
            dir.y = 0f;
            transform.position += dir * speed * Time.deltaTime;

            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);

            SetAnimSpeed(speed / _data.ChaseSpeed);
        }

        private void LookAt(Vector3 target)
        {
            var dir = (target - transform.position).normalized;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);
        }

        private void SetAnimSpeed(float speed)
        {
            if (_animator != null)
                _animator.SetFloat(AnimSpeed, speed, 0.1f, Time.deltaTime);
        }

        private void PickWanderTarget()
        {
            var offset = Random.insideUnitSphere * _data.WanderRadius;
            offset.y = 0f;
            _wanderTarget = _spawnPoint + offset;
        }

        private void HandleDeath(EnemyHealth enemy)
        {
            if (enemy != _health) return;
            SetState(EnemyState.Death);
            if (_animator != null) _animator.SetTrigger(AnimDefeat);
            enabled = false;
        }

        private void HandleStagger(EnemyHealth enemy)
        {
            if (enemy != _health) return;
            SetState(EnemyState.Stagger);
            if (_animator != null) _animator.SetTrigger(AnimHit);
        }

        private void HandleDamaged(EnemyHealth enemy, float hpRatio)
        {
            if (enemy != _health) return;
            if (_data.AggroOnDamage && !_isAggro)
            {
                _isAggro = true;
                if (_state == EnemyState.Idle || _state == EnemyState.Wander)
                    SetState(EnemyState.Alert);
            }
        }

        /// <summary>Принудительно перевести врага в Stagger на заданное время (для оглушения молотом).</summary>
        public void ForceStun(float duration)
        {
            _stunOverrideDuration = duration;
            SetState(EnemyState.Stagger);
            if (_animator != null) _animator.SetTrigger(AnimHit);
        }

        public void Initialize(EnemyData data, Vector3 spawnPoint)
        {
            _data = data;
            _spawnPoint = spawnPoint;
        }
    }
}
