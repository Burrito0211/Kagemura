using System.Collections;
using UnityEngine;

namespace Kagemura.Player.Specials
{
    /// <summary>
    /// Special 1 — the offensive burst (spec §2.4): a rooted AoE slam around the player.
    ///
    /// The wind-up is the whole design. The player is locked in place and takes damage normally
    /// for windup + recovery, so the slam is for openings the player has already made (a
    /// staggered rusher, a group knocked back) rather than a panic button. Deliberately no
    /// i-frames — that's what the dash-strike is for, and giving both abilities an escape
    /// would flatten the choice between them.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class SlamSpecial : SpecialAbility
    {
        [Header("Greybox Tell")]
        [Tooltip("Draw the slam radius while it resolves. Placeholder until the VFX exists.")]
        [SerializeField] private bool showRadiusGizmo = true;

        private Rigidbody2D _rb;
        private Vector2 _lastBlastCenter;
        private float _gizmoUntil = -999f;

        protected override void Awake()
        {
            base.Awake();
            _rb = GetComponent<Rigidbody2D>();
        }

        protected override void DoUse(int facing) => StartCoroutine(SlamRoutine());

        /// <summary>
        /// Centred on the player — the slam isn't aimed, so the preview's job is to teach its
        /// reach. A box rather than a circle: it matches the OverlapBox the weapons use, so
        /// hitboxes stay comparable when tuning, and a square blast is easier to read.
        /// </summary>
        public override void GetAimArea(int facing, out Vector2 center, out Vector2 size)
        {
            center = transform.position;
            size = new Vector2(data.radius * 2f, data.radius);
        }

        private IEnumerator SlamRoutine()
        {
            _running = true;
            _player?.SetControlLocked(true);
            // Locking control stops new input, but momentum carries — plant the player so a
            // "rooted" slam doesn't slide across the floor.
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            SetTint(true);

            yield return new WaitForSeconds(data.windup);

            // Same call the preview uses, so what was outlined is exactly what gets hit.
            GetAimArea(_player != null ? _player.Facing : 1, out Vector2 center, out Vector2 size);
            DamageBox(center, size, null);

            _lastBlastCenter = center;
            _gizmoUntil = Time.time + 0.2f;

            SetTint(false);
            yield return new WaitForSeconds(data.recovery);

            _player?.SetControlLocked(false);
            _running = false;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            // Never leave the player rooted because the object was disabled mid-slam.
            if (!_running) return;
            _running = false;
            SetTint(false);
            _player?.SetControlLocked(false);
        }

        private void OnDrawGizmos()
        {
            if (!showRadiusGizmo || data == null || Time.time > _gizmoUntil) return;
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.7f);
            Gizmos.DrawWireCube(_lastBlastCenter, new Vector3(data.radius * 2f, data.radius, 0f));
        }
    }
}
