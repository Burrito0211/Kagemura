using System;
using System.Collections;
using Kagamura.Systems;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kagamura.Player
{
    /// <summary>
    /// Build Order step 3: Dead Cells-style dodge roll with invulnerability frames (spec §2.3).
    /// The i-frame window is authored separately from the roll duration so the dodge can be
    /// tuned generous or tight without changing how far the roll travels.
    ///
    /// Timeline of one dodge (all values inspector-tunable):
    ///   0 ──── iFrameStartDelay ──── iFrames active ──── ends ──── dodgeDuration ──── cooldown
    /// Leaving a startup gap before i-frames means a panic-mashed dodge still eats the hit,
    /// while a well-timed one clears it — that's the skill the whole combat loop is built on.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(Rigidbody2D))]
    public class DodgeController : MonoBehaviour
    {
        [Header("Roll")]
        [Tooltip("Horizontal speed during the roll, units/second.")]
        [SerializeField] private float dodgeSpeed = 18f;
        [Tooltip("How long the roll lasts.")]
        [SerializeField] private float dodgeDuration = 0.25f;
        [Tooltip("Seconds before another dodge can start, measured from the end of the roll.")]
        [SerializeField] private float cooldown = 0.45f;
        [Tooltip("Fraction of roll speed kept when the roll ends. 0 = dead stop, 1 = keep all.")]
        [Range(0f, 1f)][SerializeField] private float exitSpeedRetained = 0.35f;

        [Header("Invulnerability")]
        [Tooltip("Delay after the dodge starts before i-frames begin. Keep small but non-zero " +
                 "so mashing dodge isn't a free 'ignore damage' button.")]
        [SerializeField] private float iFrameStartDelay = 0.04f;
        [Tooltip("How long i-frames last. Should end before the roll does so the recovery " +
                 "tail is punishable.")]
        [SerializeField] private float iFrameDuration = 0.16f;

        [Header("Rules")]
        [Tooltip("Allow dodging while airborne.")]
        [SerializeField] private bool allowAirDodge = true;
        [Tooltip("Freeze vertical velocity during the roll (a flat, readable dash).")]
        [SerializeField] private bool zeroGravityDuringDodge = true;

        [Header("Feedback")]
        [Tooltip("Sprite tint while i-frames are active. Placeholder until the dodge anim exists.")]
        [SerializeField] private Color iFrameTint = new Color(0.6f, 0.85f, 1f, 0.6f);

        private PlayerController _player;
        private Rigidbody2D _rb;
        private Health _health;
        private SpriteRenderer _sprite;
        private InputAction _dodgeAction;

        private bool _isDodging;
        private bool _iFramesActive;
        private float _cooldownEndTime = -999f;
        private float _baseGravityScale;
        private Color _baseColor = Color.white;

        /// <summary>Fired with the roll direction (+1/-1) when a dodge starts — VFX/SFX hook.</summary>
        public event Action<int> OnDodgeStarted;
        public event Action OnDodgeEnded;

        public bool IsDodging => _isDodging;
        public bool CanDodge => !_isDodging
                                && Time.time >= _cooldownEndTime
                                && (allowAirDodge || _player.IsGrounded)
                                && (_health == null || _health.IsAlive);

        private void Awake()
        {
            _player = GetComponent<PlayerController>();
            _rb = GetComponent<Rigidbody2D>();
            _health = GetComponent<Health>();
            _sprite = GetComponentInChildren<SpriteRenderer>();
            if (_sprite != null) _baseColor = _sprite.color;
        }

        private void OnEnable()
        {
            _dodgeAction = InputSystem.actions?.FindAction("Dodge");
            if (_dodgeAction == null)
            {
                Debug.LogError("[DodgeController] Could not find the 'Dodge' action. Check the " +
                               "project-wide Input System action asset (Player/Dodge).", this);
                return;
            }
            _dodgeAction.Enable();
        }

        private void OnDisable()
        {
            // Never leave the player locked out of their own controller.
            if (_isDodging) EndDodge();
        }

        private void Update()
        {
            if (_dodgeAction == null) return;

            if (_dodgeAction.WasPressedThisFrame() && CanDodge)
                StartCoroutine(DodgeRoutine());
        }

        private IEnumerator DodgeRoutine()
        {
            // Roll toward held input, or straight ahead if standing still.
            int dir = Mathf.Abs(_player.MoveInput) > 0.01f
                ? (int)Mathf.Sign(_player.MoveInput)
                : _player.Facing;

            _isDodging = true;
            _player.SetControlLocked(true);
            // Captured here rather than in Awake — PlayerController also writes gravityScale,
            // and Awake order between components on the same object isn't guaranteed.
            _baseGravityScale = _rb.gravityScale;
            if (zeroGravityDuringDodge) _rb.gravityScale = 0f;
            _rb.linearVelocity = new Vector2(dir * dodgeSpeed, zeroGravityDuringDodge ? 0f : _rb.linearVelocity.y);
            OnDodgeStarted?.Invoke(dir);

            float elapsed = 0f;
            float iFrameEnd = iFrameStartDelay + iFrameDuration;

            while (elapsed < dodgeDuration)
            {
                // Hold roll speed for the whole duration — no drag-off mid-dodge.
                _rb.linearVelocity = new Vector2(dir * dodgeSpeed,
                    zeroGravityDuringDodge ? 0f : _rb.linearVelocity.y);

                SetIFrames(elapsed >= iFrameStartDelay && elapsed < iFrameEnd);

                elapsed += Time.deltaTime;
                yield return null;
            }

            EndDodge();
        }

        private void SetIFrames(bool active)
        {
            if (active == _iFramesActive) return;
            _iFramesActive = active;

            // Counted hold on Health — released again below no matter how the dodge ends.
            _health?.SetInvulnerable(active);

            if (_sprite != null)
                _sprite.color = active ? iFrameTint : _baseColor;
        }

        private void EndDodge()
        {
            SetIFrames(false);
            _isDodging = false;
            _rb.gravityScale = _baseGravityScale;
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x * exitSpeedRetained, _rb.linearVelocity.y);
            _player.SetControlLocked(false);
            _cooldownEndTime = Time.time + cooldown;
            OnDodgeEnded?.Invoke();
        }
    }
}
