using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Kagamura.Player.Specials
{
    /// <summary>
    /// Special 2 — the utility/mobility option (spec §2.4): a long forward dash that damages
    /// everything it passes through, with i-frames for the whole trip.
    ///
    /// It's the counterpart to the slam: the slam is committed and punishable, this one is safe
    /// but has to be aimed. It doubles as the traversal tool §2.4 asks for, which is why
    /// dashDistance belongs to level design as much as to combat — greybox gaps around it.
    ///
    /// Distinct from the dodge roll: longer, damaging, costs Focus, and doesn't share the
    /// dodge's cooldown. The dodge stays the free, always-available defensive option.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class DashStrikeSpecial : SpecialAbility
    {
        // Width of the box swept each frame. Only needs to exceed the distance covered per
        // frame (distance/duration * deltaTime) so the sweep leaves no gaps — at the default
        // 5.5 units in 0.18s that's ~0.5 units at 60fps, so this has plenty of margin.
        private const float SweepWidth = 1.4f;

        // How far in front of the player the sweep is centred.
        private const float SweepOffset = 0.5f;

        private Rigidbody2D _rb;
        private float _baseGravity = 1f;

        // Each enemy takes one hit per dash, however many frames it stays inside the sweep.
        private readonly HashSet<Collider2D> _hitThisDash = new HashSet<Collider2D>();

        protected override void Awake()
        {
            base.Awake();
            _rb = GetComponent<Rigidbody2D>();
        }

        protected override void DoUse(int facing) => StartCoroutine(DashRoutine(facing));

        /// <summary>
        /// The whole corridor the dash sweeps, from here to the landing point. This is the
        /// preview that matters most: the dash is directional and travels further than it looks,
        /// so the far edge of the box is where the player will end up.
        /// </summary>
        public override void GetAimArea(int facing, out Vector2 center, out Vector2 size)
        {
            center = (Vector2)transform.position
                     + new Vector2(facing * (SweepOffset + data.dashDistance * 0.5f), 0f);
            size = new Vector2(data.dashDistance + SweepWidth, data.dashHitHeight);
        }

        private IEnumerator DashRoutine(int facing)
        {
            _running = true;
            _hitThisDash.Clear();

            _player?.SetControlLocked(true);
            SetTint(true);
            if (data.dashInvulnerable) _health?.SetInvulnerable(true);

            // Captured per dash, not in Awake: PlayerController writes gravityScale too.
            _baseGravity = _rb.gravityScale;
            _rb.gravityScale = 0f;

            float speed = data.dashDistance / Mathf.Max(0.01f, data.dashDuration);
            float elapsed = 0f;

            while (elapsed < data.dashDuration)
            {
                _rb.linearVelocity = new Vector2(facing * speed, 0f);

                // Sweep the damage box along the dash each frame. A single overlap at the end
                // would let fast dashes tunnel straight past a thin enemy.
                Vector2 center = (Vector2)transform.position + new Vector2(facing * SweepOffset, 0f);
                DamageBox(center, new Vector2(SweepWidth, data.dashHitHeight),
                          new Vector2(facing, 0.15f).normalized, _hitThisDash);

                elapsed += Time.deltaTime;
                yield return null;
            }

            EndDash();
        }

        private void EndDash()
        {
            _rb.gravityScale = _baseGravity;
            _rb.linearVelocity = new Vector2(_rb.linearVelocity.x * data.dashExitSpeedRetained,
                                             _rb.linearVelocity.y);

            if (data.dashInvulnerable) _health?.SetInvulnerable(false);
            SetTint(false);
            _player?.SetControlLocked(false);
            _running = false;
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            // Releasing the i-frame hold matters most here: Health counts holds, so a dash
            // interrupted by a disable would leave the player permanently invulnerable.
            if (!_running) return;
            EndDash();
        }
    }
}
