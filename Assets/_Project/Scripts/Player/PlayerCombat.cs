using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kagemura.Player
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
        private ParryController _parry;
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
            _parry = GetComponent<ParryController>();

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

            // A dodge or a parry is a commitment — you can't swing out of the middle of either,
            // and a bow draw is dropped rather than held through it.
            if ((_dodge != null && _dodge.IsDodging) || (_parry != null && _parry.IsBusy))
            {
                currentWeapon.CancelAttack();
                return;
            }

            if (_attackAction.WasPressedThisFrame())
                currentWeapon.TryAttack(_player.Facing);
            else if (_attackAction.WasReleasedThisFrame())
                currentWeapon.ReleaseAttack(_player.Facing);
        }

        /// <summary>Step to the next weapon on the player, wrapping around.</summary>
        public void CycleWeapon()
        {
            if (_weapons == null || _weapons.Length < 2) return;

            // Don't carry a half-drawn bow over to the next weapon.
            if (currentWeapon != null) currentWeapon.CancelAttack();

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
