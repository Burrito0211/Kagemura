using UnityEngine;

namespace Kagamura.Enemies
{
    /// <summary>
    /// Tunable stats for one enemy type, authored as an asset (spec §6) so behaviour is
    /// differentiated by data where possible. The rusher, the ranged type, and their seasonal
    /// reskins are all assets of this shape — only genuinely different *behaviour* gets a new script.
    ///
    /// The attack timings here are the numbers the dodge is balanced against: windup is the
    /// player's read, and iFrameDuration on DodgeController must comfortably cover activeTime.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Kagamura/Enemy Data")]
    public class EnemyData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Yokai Rusher";

        [Header("Health")]
        public int maxHealth = 60;
        [Tooltip("Seconds the enemy is staggered after taking a hit (lets knockback read).")]
        public float hitStagger = 0.15f;

        [Header("Movement")]
        public float moveSpeed = 3.5f;
        [Tooltip("How far away the player is noticed.")]
        public float chaseRange = 10f;
        [Tooltip("Stops closing in at this distance so it doesn't shove the player around.")]
        public float stopDistance = 1.1f;

        [Header("Attack")]
        [Tooltip("Distance at which the attack is committed to.")]
        public float attackRange = 1.6f;
        public int damage = 12;
        public float knockbackForce = 8f;
        [Tooltip("Telegraph before the hitbox goes live. This is the dodge window — raise it " +
                 "to make the enemy more readable, lower it to make it more dangerous.")]
        public float windupTime = 0.45f;
        [Tooltip("How long the hitbox stays live.")]
        public float activeTime = 0.1f;
        [Tooltip("Punish window after the swing where the enemy can't act.")]
        public float recoveryTime = 0.35f;
        [Tooltip("Minimum seconds between attack attempts.")]
        public float attackCooldown = 1.2f;

        [Header("Attack Hitbox (relative to enemy, +X = forward)")]
        public Vector2 hitboxSize = new Vector2(1.6f, 1.4f);
        public Vector2 hitboxOffset = new Vector2(1f, 0f);

        [Header("Ranged (ignored by melee types)")]
        [Tooltip("Gives ground while the player is nearer than this. Set it inside attackRange, " +
                 "or the enemy backs off before it ever gets a shot away.")]
        public float retreatDistance = 4f;
        [Tooltip("Projectile speed. Slow enough to be read and dodged is the point — this is the " +
                 "second dodge window the type offers, after the windup.")]
        public float projectileSpeed = 9f;
        [Tooltip("Seconds before an unobstructed projectile despawns.")]
        public float projectileLifetime = 3f;

        [Header("Telegraph")]
        [Tooltip("Sprite tint during windup — the visual tell the player reads to time a dodge.")]
        public Color windupColor = new Color(1f, 0.55f, 0.2f);
    }
}
