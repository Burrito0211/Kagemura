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

        [Header("Juice (used later)")]
        [Tooltip("Hit-stop duration on a successful hit. Wired up during the polish pass.")]
        public float hitStopDuration = 0.05f;
    }
}
