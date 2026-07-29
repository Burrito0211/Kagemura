using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kagamura.Player
{
    /// <summary>
    /// Reads the Attack input and forwards it to the currently equipped weapon, and cycles
    /// between the weapons attached to the player (spec §2.2 — three weapons, one shared base).
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerCombat : MonoBehaviour
    {
        [Tooltip("Active weapon. Defaults to the first WeaponBase found on this object.")]
        [SerializeField] private WeaponBase currentWeapon;

        private PlayerController _player;
        private DodgeController _dodge;
        private InputAction _attackAction;
        private InputAction _switchAction;
        private WeaponBase[] _weapons;

        /// <summary>Raised on equip so the HUD can show the weapon without polling for it.</summary>
        public event Action<WeaponBase> OnWeaponChanged;

        public WeaponBase CurrentWeapon => currentWeapon;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _dodge = GetComponent<DodgeController>();

            // Every WeaponBase on the player is a slot in the cycle, in component order.
            _weapons = GetComponents<WeaponBase>();
            if (currentWeapon == null && _weapons.Length > 0)
                currentWeapon = _weapons[0];
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

            _switchAction = InputSystem.actions?.FindAction("SwitchWeapon");
            _switchAction?.Enable();
        }

        private void Update()
        {
            if (_switchAction != null && _switchAction.WasPressedThisFrame()) CycleWeapon();

            if (_attackAction == null || currentWeapon == null) return;

            // A dodge is a commitment — you can't swing out of the middle of a roll.
            if (_dodge != null && _dodge.IsDodging) return;

            if (_attackAction.WasPressedThisFrame())
                currentWeapon.TryAttack(_player.Facing);
        }

        /// <summary>Step to the next weapon on the player, wrapping around.</summary>
        public void CycleWeapon()
        {
            if (_weapons == null || _weapons.Length < 2) return;

            int index = System.Array.IndexOf(_weapons, currentWeapon);
            EquipWeapon(_weapons[(index + 1) % _weapons.Length]);
        }

        /// <summary>Swap the active weapon (used when Sickle/Bow are added).</summary>
        public void EquipWeapon(WeaponBase weapon)
        {
            currentWeapon = weapon;
            OnWeaponChanged?.Invoke(weapon);
        }
    }
}
