using UnityEngine;

namespace Kagamura.Player.Specials
{
    /// <summary>
    /// Tunable stats for a special ability, authored as an asset (spec §2.4, §6) so cost and
    /// cooldown can be balanced without touching code. Same approach as WeaponData: one asset
    /// type with a section per variant, rather than a class per ability.
    /// </summary>
    [CreateAssetMenu(fileName = "SpecialData", menuName = "Kagamura/Special Data")]
    public class SpecialData : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Special";

        [Header("Cost")]
        [Tooltip("Whole Focus charges spent per use. Both specials draw on the same pool, so " +
                 "these two numbers are really one decision: which special the player can " +
                 "afford more often, and whether a full meter buys one of each.")]
        public int focusCost = 3;
        [Tooltip("Seconds before it can be used again, even with Focus to spare. Stops a full " +
                 "meter from becoming a burst of two identical specials back to back.")]
        public float cooldown = 1f;

        [Header("Damage")]
        public int damage = 45;
        public float knockbackForce = 9f;

        [Header("Burst (slam)")]
        [Tooltip("Radius of the slam, in world units, centred on the player.")]
        public float radius = 3f;
        [Tooltip("Rooted wind-up before the hit lands. This is what makes the slam punishable — " +
                 "raise it and the player has to earn the opening first.")]
        public float windup = 0.28f;
        [Tooltip("Rooted recovery after the hit. Also punishable.")]
        public float recovery = 0.22f;
        [Tooltip("Upward bias in the slam's knockback. 0 = flat, 1 = straight up.")]
        [Range(0f, 1f)] public float knockbackLift = 0.6f;

        [Header("Mobility (dash-strike)")]
        [Tooltip("Distance covered, in world units. Doubles as the traversal gap it can clear, " +
                 "so level design depends on this number — change it before greyboxing, not after.")]
        public float dashDistance = 5.5f;
        [Tooltip("Seconds the dash takes. Distance / duration is the speed.")]
        public float dashDuration = 0.18f;
        [Tooltip("Height of the damaging box swept along the dash.")]
        public float dashHitHeight = 1.4f;
        [Tooltip("Keep i-frames for the whole dash. On: an escape tool as well as a gap-closer.")]
        public bool dashInvulnerable = true;
        [Tooltip("Fraction of dash speed kept on exit. Low values stop the player dead on arrival.")]
        [Range(0f, 1f)] public float dashExitSpeedRetained = 0.25f;

        [Header("Feedback")]
        [Tooltip("Sprite tint while the ability is running. Placeholder until the anims exist.")]
        public Color tint = new Color(1f, 0.85f, 0.4f);
        [Tooltip("Hit-stop on connect. Wired up during the polish pass.")]
        public float hitStopDuration = 0.08f;
    }
}
