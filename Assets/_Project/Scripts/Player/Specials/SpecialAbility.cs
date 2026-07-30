using System;
using System.Collections.Generic;
using Kagamura.Systems;
using UnityEngine;

namespace Kagamura.Player.Specials
{
    /// <summary>
    /// Shared base for the two specials (spec §2.4), mirroring WeaponBase: this owns the cost
    /// and cooldown gating, subclasses own what the ability actually does.
    ///
    /// Focus is spent only once the ability is cleared to start, so a press that fails the
    /// cooldown or the cost never charges the player for nothing.
    /// </summary>
    public abstract class SpecialAbility : MonoBehaviour
    {
        [Tooltip("Stats asset for this ability (Create > Kagamura > Special Data).")]
        [SerializeField] protected SpecialData data;

        [Tooltip("Which layers this ability can hit. Set to your Enemy layer.")]
        [SerializeField] protected LayerMask targetLayers;

        [Header("Aim Preview")]
        [Tooltip("Show the area this ability will hit while its hotkey is held. Hold to look, " +
                 "release to commit — a tap fires immediately, so the preview costs nothing " +
                 "once the player knows the ranges.")]
        [SerializeField] private bool showAimPreview = true;
        [Tooltip("Preview opacity while aiming.")]
        [Range(0f, 1f)][SerializeField] private float previewAlpha = 0.22f;
        [Tooltip("Preview colour when the hotkey is held but the ability can't be used — no " +
                 "charges, or still on cooldown. Answers 'why won't it fire' before release.")]
        [SerializeField] private Color previewBlockedTint = new Color(0.6f, 0.6f, 0.65f);

        protected PlayerController _player;
        protected Health _health;
        protected Focus _focus;
        protected SpriteRenderer _sprite;
        protected Color _baseColor = Color.white;

        protected float _lastUseTime = -999f;
        protected bool _running;

        /// <summary>Fired when the ability starts, for VFX/SFX and the HUD.</summary>
        public event Action OnUsed;

        public SpecialData Data => data;
        public bool IsRunning => _running;

        public bool OffCooldown => data != null && Time.time >= _lastUseTime + data.cooldown;

        /// <summary>Everything the HUD needs to know to show the button as available.</summary>
        public bool CanUse => data != null
                             && !_running
                             && OffCooldown
                             && (_focus == null || _focus.CanAfford(data.focusCost))
                             && (_health == null || _health.IsAlive);

        protected virtual void Awake()
        {
            _player = GetComponent<PlayerController>();
            _health = GetComponent<Health>();
            _focus = GetComponent<Focus>();
            _sprite = GetComponentInChildren<SpriteRenderer>();
            if (_sprite != null) _baseColor = _sprite.color;
        }

        /// <summary>
        /// Called by PlayerSpecials on input. Returns whether the ability actually started —
        /// false means it was on cooldown, already running, or unaffordable.
        /// </summary>
        public bool TryUse(int facing)
        {
            if (data == null || _running || !OffCooldown) return false;
            if (_health != null && !_health.IsAlive) return false;

            // Charged last, and only once every other gate has passed. TrySpend raises its own
            // failure event, so the HUD still gets to say "not enough Focus".
            if (_focus != null && !_focus.TrySpend(data.focusCost)) return false;

            _lastUseTime = Time.time;
            OnUsed?.Invoke();
            DoUse(facing);
            return true;
        }

        protected abstract void DoUse(int facing);

        /// <summary>
        /// The world-space area this ability would hit if used right now. Drives the aim preview,
        /// and the abilities resolve their damage against the same call — so the outline can't
        /// drift out of step with what actually gets hit.
        /// </summary>
        public abstract void GetAimArea(int facing, out Vector2 center, out Vector2 size);

        // --- Aim preview ----------------------------------------------------------------
        // Greybox only: a translucent quad. Replaced by real VFX at the art pass, at which point
        // showAimPreview comes off.

        private SpriteRenderer _preview;
        private bool _aiming;

        /// <summary>
        /// Driven by PlayerSpecials while the ability's hotkey is held. Aiming is display only —
        /// it costs nothing and commits to nothing, so letting go without firing is free.
        /// </summary>
        public void SetAiming(bool aiming) => _aiming = aiming;

        protected virtual void Update() => UpdateAimPreview();

        private void UpdateAimPreview()
        {
            if (!showAimPreview || data == null || _player == null) return;

            // Only on while the key is held, plus the brief moment the ability resolves — which
            // turns the slam's wind-up into a visible telegraph rather than a rooted pause with
            // nothing on screen.
            bool visible = _aiming || _running;

            if (_preview == null)
            {
                if (!visible) return;          // nothing built until it's first needed
                _preview = BuildPreview();
            }

            _preview.enabled = visible;
            if (!visible) return;

            // Held but unusable reads as grey: the player finds out before release, not after.
            Color baseColor = _running || CanUse ? data.tint : previewBlockedTint;
            float alpha = _running ? Mathf.Min(1f, previewAlpha * 2.5f) : previewAlpha;
            _preview.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            GetAimArea(_player.Facing, out Vector2 center, out Vector2 size);
            _preview.transform.position = center;
            _preview.transform.localScale = new Vector3(size.x, size.y, 1f);
        }

        protected virtual void OnDisable()
        {
            // Don't leave an outline hanging on screen if this gets switched off mid-aim.
            _aiming = false;
            if (_preview != null) _preview.enabled = false;
        }

        private SpriteRenderer BuildPreview()
        {
            // Deliberately not a child of the player: PlayerController flips the player's
            // localScale.x to face, which would mirror and squash a child preview.
            var go = new GameObject($"{name} Aim Preview (greybox)");

            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = GreyboxArt.WhiteSprite();
            sprite.color = new Color(data.tint.r, data.tint.g, data.tint.b, previewAlpha);
            sprite.sortingOrder = 1;   // over the ground, under the actors
            return sprite;
        }

        private void OnDestroy()
        {
            // The preview isn't parented to the player, so it won't be cleaned up with them.
            if (_preview != null) Destroy(_preview.gameObject);
        }

        protected void SetTint(bool on)
        {
            if (_sprite != null) _sprite.color = on ? data.tint : _baseColor;
        }

        /// <summary>
        /// Damage everything alive on the target layers inside a box, once each. Shared by both
        /// specials — the slam calls it once, the dash sweeps it per frame.
        ///
        /// Pass a direction to knock everything the same way (the dash, along its travel), or
        /// null to knock each victim outward from the center (the slam, which should scatter).
        /// <paramref name="alreadyHit"/> keeps a multi-frame sweep from hitting the same enemy twice.
        /// </summary>
        protected int DamageBox(Vector2 center, Vector2 size, Vector2? knockDir,
                                HashSet<Collider2D> alreadyHit = null)
        {
            var cols = Physics2D.OverlapBoxAll(center, size, 0f, targetLayers);

            int count = 0;
            foreach (var col in cols)
            {
                if (alreadyHit != null && !alreadyHit.Add(col)) continue;
                if (!col.TryGetComponent<IDamageable>(out var victim) || !victim.IsAlive) continue;

                victim.TakeDamage(new DamageInfo
                {
                    Amount = data.damage,
                    HitPoint = col.ClosestPoint(center),
                    KnockbackDir = knockDir ?? RadialKnock(center, col),
                    KnockbackForce = data.knockbackForce,
                    Source = gameObject
                });
                count++;
            }
            return count;
        }

        /// <summary>Outward from the blast, lifted by knockbackLift so the slam pops enemies up.</summary>
        private Vector2 RadialKnock(Vector2 center, Collider2D victim)
        {
            float away = victim.bounds.center.x - center.x;
            // Directly overhead or underfoot: pick the player's facing rather than dividing by zero.
            float sign = Mathf.Abs(away) < 0.01f ? (_player != null ? _player.Facing : 1) : Mathf.Sign(away);
            return new Vector2(sign * (1f - data.knockbackLift), data.knockbackLift).normalized;
        }
    }
}
