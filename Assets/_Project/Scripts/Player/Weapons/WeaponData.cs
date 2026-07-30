using UnityEngine;

namespace Kagamura.Player.Weapons
{
    /// <summary>
    /// Tunable stats for a weapon, authored as an asset so damage/range/combo can be
    /// balanced without touching code (spec §2.2, §6). Sword, Sickle, and Bow will each
    /// be one of these assets, differentiated by data rather than unique code paths.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponData", menuName = "Kagamura/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Sword";

        [Header("Damage")]
        public int damage = 20;
        public float knockbackForce = 6f;

        [Header("Focus")]
        [Tooltip("Focus points per enemy hit, where 100 = one whole charge. So 34 is three hits " +
                 "per charge. Set it against the weapon's hit rate, not per swing — the sickle " +
                 "earns less per hit but lands far more of them.")]
        public int focusPerHit = 34;

        [Header("Timing")]
        [Tooltip("Minimum seconds between swings.")]
        public float attackCooldown = 0.3f;

        [Header("Hitbox (relative to player, +X = forward)")]
        public Vector2 hitboxSize = new Vector2(1.4f, 1.2f);
        public Vector2 hitboxOffset = new Vector2(0.9f, 0f);

        [Header("Combo")]
        [Tooltip("Number of chained swings before the combo restarts (Sword = 2).")]
        public int comboMaxHits = 2;
        [Tooltip("If the next swing doesn't land within this window, the combo resets.")]
        public float comboResetTime = 0.7f;
        [Tooltip("Damage multiplier applied to the final combo hit.")]
        public float finisherDamageMultiplier = 1.5f;

        [Header("Bleed (Sickle)")]
        [Tooltip("Whether hits stack bleed on the target. Off for Sword and Bow.")]
        public bool appliesBleed = false;
        [Tooltip("Damage per tick, per stack. Kept low — the sickle's payoff is in stacking, " +
                 "not in any single tick.")]
        public int bleedDamagePerStack = 2;
        [Tooltip("Seconds between bleed ticks.")]
        public float bleedTickInterval = 0.5f;
        [Tooltip("Seconds a bleed lasts. Every new stack refreshes the whole duration.")]
        public float bleedDuration = 3f;
        [Tooltip("Stack ceiling. This is the sickle's damage cap — raise it and aggressive " +
                 "play pays off harder.")]
        public int bleedMaxStacks = 5;

        [Header("Bow (charge-up)")]
        [Tooltip("Whether the attack draws on hold and fires on release. Off for Sword and Sickle.")]
        public bool chargesOnHold = false;
        [Tooltip("Seconds of draw for a full-power shot. This is the bow's real cost — the " +
                 "time spent committed while an enemy closes.")]
        public float fullDrawTime = 0.8f;
        [Tooltip("Damage multiplier for a shot released instantly, scaling up to 1 at full draw.")]
        [Range(0f, 1f)] public float minDrawDamageMultiplier = 0.35f;
        [Tooltip("Arrow speed at zero draw.")]
        public float minProjectileSpeed = 9f;
        [Tooltip("Arrow speed at full draw. Range comes from speed x lifetime.")]
        public float maxProjectileSpeed = 22f;
        [Tooltip("Seconds before an arrow that hit nothing despawns.")]
        public float projectileLifetime = 2f;
        [Tooltip("Top speed multiplier while drawing. Low values root the player in place.")]
        [Range(0f, 1f)] public float drawMoveSpeedMultiplier = 0.45f;

        [Header("Juice (used later)")]
        [Tooltip("Hit-stop duration on a successful hit. Wired up during the polish pass.")]
        public float hitStopDuration = 0.05f;
    }
}
