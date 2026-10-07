using UnityEngine;
using ZeldaDaughter.Inventory;

namespace ZeldaDaughter.Combat
{
    public class WeaponEquipSystem : MonoBehaviour
    {
        [SerializeField] private Transform _weaponBoneAttach;

        private WeaponData _currentWeapon;
        private GameObject _weaponVisualInstance;

        public static event System.Action<WeaponData> OnWeaponChanged;

        public WeaponData CurrentWeapon => _currentWeapon;
        public bool HasWeapon => _currentWeapon != null;

        public void Equip(WeaponData weapon)
        {
            Unequip();
            _currentWeapon = weapon;

            if (weapon != null && weapon.WeaponModelPrefab != null && _weaponBoneAttach != null)
            {
                _weaponVisualInstance = Instantiate(weapon.WeaponModelPrefab, _weaponBoneAttach);
                _weaponVisualInstance.transform.localPosition = Vector3.zero;
                _weaponVisualInstance.transform.localRotation = Quaternion.identity;
            }

            OnWeaponChanged?.Invoke(weapon);
        }

        public void Unequip()
        {
            if (_weaponVisualInstance != null)
            {
                Destroy(_weaponVisualInstance);
                _weaponVisualInstance = null;
            }
            _currentWeapon = null;
        }

        /// <summary>Экипировать оружие из предмета инвентаря. Ничего не делает, если item не является оружием.</summary>
        public void EquipFromItem(ItemData item)
        {
            if (item == null || !item.IsWeapon) return;
            Equip(item.WeaponData);
        }
    }
}
