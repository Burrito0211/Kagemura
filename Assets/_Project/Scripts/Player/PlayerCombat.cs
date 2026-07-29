using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kagamura.Player
{
    /// <summary>
    /// Reads the Attack input and forwards it to the currently equipped weapon.
    /// Weapon switching (Sickle/Bow) plugs in here later; for now it just drives the sword.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerCombat : MonoBehaviour
    {
        [Tooltip("Active weapon. Defaults to the first WeaponBase found on this object.")]
        [SerializeField] private WeaponBase currentWeapon;

        private PlayerController _player;
        private DodgeController _dodge;
        private InputAction _attackAction;

        /// <summary>Raised on equip so the HUD can show the weapon without polling for it.</summary>
        public event Action<WeaponBase> OnWeaponChanged;

        public WeaponBase CurrentWeapon => currentWeapon;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _dodge = GetComponent<DodgeController>();
            if (currentWeapon == null)
                currentWeapon = GetComponent<WeaponBase>();
        }

        private void OnEnable()
        {
            _attackAction = InputSystem.actions?.FindAction("Attack");
            if (_attackAction == null)
            {
                Debug.LogError("[PlayerCombat] Could not find 'Attack' action. Check the " +
                               "project-wide Input System action asset.", this);
                return;
            }
            _attackAction.Enable();
        }

        private void Update()
        {
            if (_attackAction == null || currentWeapon == null) return;

            // A dodge is a commitment — you can't swing out of the middle of a roll.
            if (_dodge != null && _dodge.IsDodging) return;

            if (_attackAction.WasPressedThisFrame())
                currentWeapon.TryAttack(_player.Facing);
        }

        /// <summary>Swap the active weapon (used when Sickle/Bow are added).</summary>
        public void EquipWeapon(WeaponBase weapon)
        {
            currentWeapon = weapon;
            OnWeaponChanged?.Invoke(weapon);
        }
    }
}
