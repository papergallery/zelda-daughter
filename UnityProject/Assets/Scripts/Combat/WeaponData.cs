using UnityEngine;

namespace ZeldaDaughter.Combat
{
    [CreateAssetMenu(menuName = "ZeldaDaughter/Combat/Weapon Data", fileName = "NewWeaponData")]
    public class WeaponData : ScriptableObject
    {
        [SerializeField] private float _damage = 10f;
        [SerializeField] private float _attackSpeed = 1f;
        [SerializeField] private float _attackRange = 1.5f;
        [SerializeField] private WoundType _inflictedWoundType = WoundType.Puncture;
        [SerializeField] private float _woundSeverity = 0.3f;
        [SerializeField] private string _animationTrigger = "Attack";
        [SerializeField] private GameObject _weaponModelPrefab;
        [SerializeField] private string _attachBoneName = "RightHand";

        public float Damage => _damage;
        public float AttackSpeed => _attackSpeed;
        public float AttackRange => _attackRange;
        public WoundType InflictedWoundType => _inflictedWoundType;
        public float WoundSeverity => _woundSeverity;
        public string AnimationTrigger => _animationTrigger;
        public GameObject WeaponModelPrefab => _weaponModelPrefab;
        public string AttachBoneName => _attachBoneName;
    }
}
