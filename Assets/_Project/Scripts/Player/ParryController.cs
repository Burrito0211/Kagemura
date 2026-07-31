using System;
using System.Collections;
using Kagemura.Enemies;
using Kagemura.Systems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kagemura.Player
{
    /// <summary>
    /// Parry (spec §2.3, §9 answered as dodge + parry): a short window that negates an incoming
    /// attack and breaks the attacker's swing.
    ///
    /// Deliberately sits *alongside* the dodge rather than replacing it, which is what makes the
    /// two worth having:
    ///   - Dodge: longer window, moves you out of danger, no punish for a miss beyond the roll's
    ///     own tail. The safe answer, always available.
    ///   - Parry: shorter window, no movement, and whiffing locks you in place for
    ///     <see cref="failRecovery"/> — but a success interrupts the attacker and pays Focus.
    ///     The greedy answer.
    ///
    /// So the dodge stays correct for "I don't know what's coming" and the parry pays for "I do".
    /// No damage is dealt: the reward is tempo and Focus, not a free hit — that keeps it from
    /// becoming the only thing worth doing.
    ///
    /// Parry success is detected off Health.OnDamageAvoided rather than by looking for incoming
    /// attacks: the same event that already tells the HUD a hit was negated. It can't be gamed,
    /// because it takes a real attack that would otherwise have landed.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(Health))]
    public class ParryController : MonoBehaviour
    {
        [Header("Window")]
        [Tooltip("How long the parry can catch an attack. Keep it under the dodge's i-frames — " +
                 "the tighter window is what earns the bigger payoff.")]
        [SerializeField] private float parryWindow = 0.12f;
        [Tooltip("Lockout after a parry that caught nothing. This is the whole risk of the " +
                 "move: at 0 there's no reason to ever dodge again.")]
        [SerializeField] private float failRecovery = 0.35f;
        [Tooltip("Lockout after a successful parry, before another can start.")]
        [SerializeField] private float successCooldown = 0.25f;

        [Header("Reward")]
        [Tooltip("Seconds the parried attacker is frozen, on top of dropping its swing.")]
        [SerializeField] private float staggerDuration = 0.9f;
        [Tooltip("Focus points on a successful parry, on top of what Focus already pays for any " +
                 "avoided hit — so the parry is strictly the better read, not a separate economy.")]
        [SerializeField] private int focusBonus = 50;

        [Header("Feedback")]
        [Tooltip("Sprite tint during the active window. Placeholder until the parry anim exists.")]
        [SerializeField] private Color parryTint = new Color(1f, 0.98f, 0.85f);
        [Tooltip("Sprite tint during the failed-parry lockout, so the punish is legible.")]
        [SerializeField] private Color failTint = new Color(0.45f, 0.45f, 0.5f);

        private PlayerController _player;
        private Health _health;
        private DodgeController _dodge;
        private Focus _focus;
        private SpriteRenderer _sprite;
        private Color _baseColor = Color.white;
        private InputAction _parryAction;

        private bool _windowOpen;
        private bool _caughtSomething;
        private bool _locked;
        private float _cooldownEndTime = -999f;

        /// <summary>Fired on a successful parry with the attacker, for VFX/SFX and hit-stop.</summary>
        public event Action<GameObject> OnParrySuccess;

        /// <summary>Fired when the window closed empty — the punish hook.</summary>
        public event Action OnParryFailed;

        public bool IsParrying => _windowOpen;

        /// <summary>True during the window or the recovery after it — PlayerCombat reads this.</summary>
        public bool IsBusy => _windowOpen || _locked;

        public bool CanParry => !_windowOpen
                                && !_locked
                                && Time.time >= _cooldownEndTime
                                && (_dodge == null || !_dodge.IsDodging)
                                && _health.IsAlive;

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _health = GetComponent<Health>();
            _dodge = GetComponent<DodgeController>();
            _focus = GetComponent<Focus>();
            _sprite = GetComponentInChildren<SpriteRenderer>();
            if (_sprite != null) _baseColor = _sprite.color;
        }

        private void OnEnable()
        {
            _parryAction = InputSystem.actions?.FindAction("Parry");
            if (_parryAction == null)
            {
                Debug.LogError("[ParryController] Could not find the 'Parry' action. Check the " +
                               "project-wide Input System action asset (Player/Parry).", this);
                return;
            }
            _parryAction.Enable();

            _health.OnDamageAvoided += HandleDamageAvoided;
        }

        private void OnDisable()
        {
            _health.OnDamageAvoided -= HandleDamageAvoided;

            // Never leave the player invulnerable or locked because this was switched off.
            if (_windowOpen) CloseWindow();
            if (_locked) EndLock();
        }

        private void Update()
        {
            if (_parryAction == null) return;

            if (_parryAction.WasPressedThisFrame() && CanParry)
                StartCoroutine(ParryRoutine());
        }

        private IEnumerator ParryRoutine()
        {
            _windowOpen = true;
            _caughtSomething = false;

            // The window is real invulnerability, so a parried hit routes through the same
            // OnDamageAvoided path the dodge uses — one code path for "that didn't land".
            _health.SetInvulnerable(true);
            SetTint(parryTint);

            float elapsed = 0f;
            while (elapsed < parryWindow && !_caughtSomething)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            CloseWindow();

            if (_caughtSomething)
            {
                _cooldownEndTime = Time.time + successCooldown;
                yield break;
            }

            // Caught nothing: the player is rooted and vulnerable. This is what a mistimed parry
            // costs, and why the dodge stays the sensible default.
            OnParryFailed?.Invoke();
            yield return LockRoutine();
        }

        private IEnumerator LockRoutine()
        {
            _locked = true;
            _player.SetControlLocked(true);
            SetTint(failTint);

            yield return new WaitForSeconds(failRecovery);

            EndLock();
            _cooldownEndTime = Time.time;
        }

        private void CloseWindow()
        {
            if (!_windowOpen) return;
            _windowOpen = false;
            _health.SetInvulnerable(false);
            SetTint(_baseColor);
        }

        private void EndLock()
        {
            _locked = false;
            _player.SetControlLocked(false);
            SetTint(_baseColor);
        }

        /// <summary>
        /// A hit was negated. If our window is what negated it, that's a parry: break the
        /// attacker's swing, freeze it, and pay the bonus.
        /// </summary>
        private void HandleDamageAvoided(DamageInfo info)
        {
            if (!_windowOpen) return;

            _caughtSomething = true;

            // A parry with no identifiable attacker still counts — the read was correct even if
            // the damage came from something that can't be staggered (a trap, a projectile).
            if (info.Source != null && info.Source.TryGetComponent<EnemyBase>(out var enemy))
            {
                enemy.InterruptAttack();
                enemy.Stagger(staggerDuration);
            }

            _focus?.Gain(focusBonus);
            OnParrySuccess?.Invoke(info.Source);
        }

        private void SetTint(Color color)
        {
            if (_sprite != null) _sprite.color = color;
        }
    }
}
