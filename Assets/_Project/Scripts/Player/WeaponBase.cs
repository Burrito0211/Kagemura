using Kagemura.Player.Weapons;
using Kagemura.Systems;
using UnityEngine;

namespace Kagemura.Player
{
    /// <summary>
    /// Shared base for all melee/ranged weapons (spec §2.2). Owns cooldown gating and the
    /// overlap-based hit query; subclasses implement how a swing behaves (combo, charge, etc.).
    /// Attach to the player root — the hitbox is placed relative to the player using facing.
    /// </summary>
    public abstract class WeaponBase : MonoBehaviour
    {
        [Tooltip("Stats asset for this weapon (Create > Kagemura > Weapon Data).")]
        [SerializeField] protected WeaponData data;

        [Tooltip("Which layers this weapon can hit. Set to your Enemy layer.")]
        [SerializeField] protected LayerMask targetLayers;

        protected float _lastAttackTime = -999f;
        protected int _lastFacing = 1;

        private Focus _focus;
        private bool _focusResolved;

        /// <summary>
        /// The player's Focus pool, if they have one. Resolved lazily rather than in Awake:
        /// WeaponBase has no Awake of its own, and subclasses that add one shouldn't have to
        /// remember to call base.
        /// </summary>
        protected Focus PlayerFocus
        {
            get
            {
                if (!_focusResolved)
                {
                    _focus = GetComponent<Focus>();
                    _focusResolved = true;
                }
                return _focus;
            }
        }

        public WeaponData Data => data;
        public bool CanAttack => data != null && Time.time >= _lastAttackTime + data.attackCooldown;

        private float _edge = 1f;

        /// <summary>
        /// The seasonal edge (spec §2.6), as a multiplier. 1 = unsharpened.
        ///
        /// Deliberately not a damage number. Each weapon spends this on the thing that makes it
        /// itself — the sword's finisher, the sickle's bleed, the bow's draw — because a flat
        /// damage multiplier is close to worthless on the sickle, where the bleed does the work,
        /// and overwhelming on the bow, which already hits hardest. Subclasses read this where it
        /// belongs for them.
        /// </summary>
        public float Edge => _edge;

        /// <summary>True while a season is sharpening this weapon. Read by the HUD.</summary>
        public bool IsSharpened => _edge > 1.0001f;

        /// <summary>
        /// Sharpen this weapon for as long as the level lasts.
        ///
        /// A runtime field, and never a write into <see cref="data"/>. WeaponData is a
        /// ScriptableObject: edits to it persist in the editor and survive play sessions, so a
        /// spring bonus written there would still be on the blade in winter, and still there
        /// tomorrow morning. That failure is invisible — the weapon simply gets better and stays
        /// better — which is why the rule is in the spec rather than only here.
        ///
        /// Clamped at 1: this system exists to sharpen, and a season quietly dulling a weapon
        /// would be a much bigger design decision than a mis-typed field should be able to make.
        /// </summary>
        public void SetEdge(float multiplier) => _edge = Mathf.Max(1f, multiplier);

        /// <summary>Called by PlayerCombat on attack input. Respects cooldown, then swings.</summary>
        public void TryAttack(int facing)
        {
            if (!CanAttack) return;
            _lastAttackTime = Time.time;
            _lastFacing = facing;
            DoAttack(facing);
        }

        protected abstract void DoAttack(int facing);

        /// <summary>
        /// Attack input released. Weapons that resolve on press (sword, sickle) ignore it;
        /// the bow fires here, because its shot is defined by how long the button was held.
        /// </summary>
        public virtual void ReleaseAttack(int facing) { }

        /// <summary>
        /// Drop any attack in progress — called when a dodge interrupts. A half-drawn bow
        /// loses the shot rather than firing it, so rolling out of a draw has a real cost.
        /// </summary>
        public virtual void CancelAttack() { }

        /// <summary>
        /// Overlap the weapon hitbox in front of the player and damage everything alive on the
        /// target layers. Returns the number of things hit; outputs the hitbox center.
        /// </summary>
        protected int PerformHit(int facing, int damage, out Vector2 center)
        {
            center = HitboxCenter(facing);
            var cols = Physics2D.OverlapBoxAll(center, data.hitboxSize, 0f, targetLayers);

            int count = 0;
            foreach (var col in cols)
            {
                if (col.TryGetComponent<IDamageable>(out var target) && target.IsAlive)
                {
                    var info = new DamageInfo
                    {
                        Amount = damage,
                        HitPoint = col.ClosestPoint(center),
                        KnockbackDir = new Vector2(facing, 0.25f).normalized,
                        KnockbackForce = data.knockbackForce,
                        Source = gameObject
                    };
                    target.TakeDamage(info);
                    OnHitTarget(col, info);
                    // Per victim, not per swing: a swing that catches two enemies is worth more
                    // Focus, which is what makes positioning pay off.
                    PlayerFocus?.Gain(data.focusPerHit);
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Per-weapon on-hit effect (spec §2.2) — bleed, lifesteal, and so on. Runs once per
        /// victim, after the damage lands. The base weapon has none.
        /// </summary>
        protected virtual void OnHitTarget(Collider2D victim, in DamageInfo info) { }

        protected Vector2 HitboxCenter(int facing) =>
            (Vector2)transform.position + new Vector2(data.hitboxOffset.x * facing, data.hitboxOffset.y);

        private void OnDrawGizmosSelected()
        {
            if (data == null) return;
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.8f);
            Gizmos.DrawWireCube(HitboxCenter(_lastFacing), data.hitboxSize);
        }
    }
}
