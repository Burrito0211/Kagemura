using UnityEngine;
using UnityEngine.InputSystem;

namespace Kagamura.Player.Specials
{
    /// <summary>
    /// Reads the two special inputs and forwards them to the abilities on the player — the
    /// specials' equivalent of PlayerCombat (spec §2.4).
    ///
    /// Two named slots rather than a cycle: there are exactly two specials by the §8 cut list,
    /// and they're different enough that having them on their own buttons is the point.
    ///
    /// Hold to aim, release to fire — the same shape as the bow's draw. Holding shows where the
    /// ability will land and costs nothing, so a player can check the reach and let go without
    /// firing; a tap fires immediately, so it stays out of the way once the ranges are learned.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerSpecials : MonoBehaviour
    {
        [Tooltip("Offensive burst. Auto-filled from the first SlamSpecial on this object.")]
        [SerializeField] private SpecialAbility burst;
        [Tooltip("Mobility/utility. Auto-filled from the first DashStrikeSpecial on this object.")]
        [SerializeField] private SpecialAbility mobility;

        private PlayerController _player;
        private DodgeController _dodge;
        private ParryController _parry;
        private InputAction _burstAction;
        private InputAction _mobilityAction;

        public SpecialAbility Burst => burst;
        public SpecialAbility Mobility => mobility;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _dodge = GetComponent<DodgeController>();
            _parry = GetComponent<ParryController>();

            if (burst == null) burst = GetComponent<SlamSpecial>();
            if (mobility == null) mobility = GetComponent<DashStrikeSpecial>();

            // Both fields take any SpecialAbility, so dragging the same component into both is
            // an easy mistake with a baffling symptom: one ability answers both keys, the other
            // looks like it stopped existing, and whichever slot is polled last wins the aim
            // state — so the first key's preview never appears. Repair it and say so.
            if (burst != null && ReferenceEquals(burst, mobility))
            {
                var slam = GetComponent<SlamSpecial>();
                var dash = GetComponent<DashStrikeSpecial>();

                Debug.LogError($"[PlayerSpecials] 'burst' and 'mobility' both point at " +
                               $"{burst.GetType().Name}. Falling back to the SlamSpecial and " +
                               "DashStrikeSpecial on this object — fix the two fields in the " +
                               "inspector, or clear both and let them auto-fill.", this);

                if (slam != null) burst = slam;
                mobility = dash;
            }
        }

        private void OnEnable()
        {
            _burstAction = InputSystem.actions?.FindAction("SpecialBurst");
            _mobilityAction = InputSystem.actions?.FindAction("SpecialDash");

            if (_burstAction == null || _mobilityAction == null)
            {
                Debug.LogError("[PlayerSpecials] Missing 'SpecialBurst' or 'SpecialDash' action. " +
                               "Check the project-wide Input System action asset.", this);
                return;
            }

            _burstAction.Enable();
            _mobilityAction.Enable();
        }

        private void Update()
        {
            // A dodge or parry outranks a special: both are already committed, and cancelling
            // into a slam would make their punishable tails free. Nor can one special interrupt
            // the other — both take over the player's movement.
            bool blocked = (_dodge != null && _dodge.IsDodging)
                           || (_parry != null && _parry.IsBusy)
                           || (burst != null && burst.IsRunning)
                           || (mobility != null && mobility.IsRunning);

            // Each slot is polled independently, so holding one to look doesn't lock out the other.
            UpdateSlot(burst, _burstAction, blocked);

            // Guard against both slots resolving to one ability anyway (a player carrying only
            // one special, say): a second pass would fight the first over the aim flag.
            if (!ReferenceEquals(mobility, burst)) UpdateSlot(mobility, _mobilityAction, blocked);
        }

        private void UpdateSlot(SpecialAbility ability, InputAction action, bool blocked)
        {
            if (ability == null || action == null) return;

            if (blocked)
            {
                // Drop the aim rather than the input: if the key is still held once the dodge or
                // the other special finishes, aiming picks straight back up.
                ability.SetAiming(false);
                return;
            }

            ability.SetAiming(action.IsPressed());

            if (!action.WasReleasedThisFrame()) return;

            ability.SetAiming(false);
            ability.TryUse(_player.Facing);
        }
    }
}
