using Kagamura.Player.Weapons;
using Kagamura.Systems;
using UnityEngine;

namespace Kagamura.Player
{
    /// <summary>
    /// Shared base for all melee/ranged weapons (spec §2.2). Owns cooldown gating and the
    /// overlap-based hit query; subclasses implement how a swing behaves (combo, charge, etc.).
    /// Attach to the player root — the hitbox is placed relative to the player using facing.
    /// </summary>
    public abstract class WeaponBase : MonoBehaviour
    {
        [Tooltip("Stats asset for this weapon (Create > Kagamura > Weapon Data).")]
        [SerializeField] protected WeaponData data;

        [Tooltip("Which layers this weapon can hit. Set to your Enemy layer.")]
        [SerializeField] protected LayerMask targetLayers;

        protected float _lastAttackTime = -999f;
        protected int _lastFacing = 1;

        public WeaponData Data => data;
        public bool CanAttack => data != null && Time.time >= _lastAttackTime + data.attackCooldown;

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
                    count++;
                }
            }
            return count;
        }

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
