using UnityEngine;

namespace Kagamura.Enemies
{
    /// <summary>
    /// Boss stats: everything an ordinary enemy has, plus what the second phase changes.
    ///
    /// A subclass rather than another header block on EnemyData, which already carries a section
    /// each for the rusher, the archer and the shieldbearer. One more would turn the shared asset
    /// into a grab-bag where most fields are inert for most enemies. Inheriting keeps every base
    /// stat and every existing tooltip while the boss-only knobs stay where they mean something.
    ///
    /// Phase 1 uses the inherited values; the multipliers below are what phase 2 does to them,
    /// so tuning the fight is mostly a matter of asking how much sharper the second half should be.
    /// </summary>
    [CreateAssetMenu(fileName = "BossData", menuName = "Kagamura/Boss Data")]
    public class BossData : EnemyData
    {
        [Header("Phase 2 Trigger")]
        [Tooltip("Fraction of max health at which phase 2 begins (spec §2.5 — an HP threshold " +
                 "with a visual tell).")]
        [Range(0.05f, 0.95f)] public float phase2HealthFraction = 0.5f;
        [Tooltip("Seconds the boss is frozen and untouchable while it turns. This is the tell: " +
                 "long enough to read, short enough not to be a cutscene.")]
        public float phaseTransitionTime = 1.4f;
        [Tooltip("Body tint for the whole of phase 2 — the permanent 'this is different now' read.")]
        public Color phase2Color = new Color(0.85f, 0.2f, 0.3f);

        [Header("Phase 2 Multipliers")]
        [Tooltip("Multiplies windup. Below 1 shortens the telegraph — the main difficulty dial, " +
                 "and the one to be careful with: too low and the fight stops being readable.")]
        [Range(0.4f, 1f)] public float phase2WindupScale = 0.75f;
        [Tooltip("Multiplies the gap between attacks. Below 1 means it presses harder.")]
        [Range(0.3f, 1f)] public float phase2CooldownScale = 0.65f;
        [Tooltip("Multiplies move speed.")]
        [Range(1f, 2.5f)] public float phase2SpeedScale = 1.3f;

        [Header("Ranged Attack")]
        [Tooltip("Bolts per volley in phase 1. One is a single aimed shot, dodged like the " +
                 "archer's.")]
        public int phase1Bolts = 1;
        [Tooltip("Bolts per volley in phase 2. A fan has to be moved out of rather than " +
                 "sidestepped, which is what makes the second half feel different.")]
        public int phase2Bolts = 3;
        [Tooltip("Total spread of a multi-bolt fan, in degrees.")]
        public float volleySpreadDegrees = 34f;
        [Tooltip("Beyond this distance the boss shoots instead of closing.")]
        public float rangedThreshold = 5f;
    }
}
