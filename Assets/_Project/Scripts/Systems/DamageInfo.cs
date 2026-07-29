using UnityEngine;

namespace Kagamura.Systems
{
    /// <summary>
    /// Data passed to anything taking damage. Kept as a struct so hits are allocation-free.
    /// Extended over time (crit flags, damage type, status effects like bleed) without
    /// changing the IDamageable contract.
    /// </summary>
    public struct DamageInfo
    {
        public int Amount;
        public Vector2 HitPoint;
        public Vector2 KnockbackDir;
        public float KnockbackForce;
        public GameObject Source;

        /// <summary>
        /// True for damage-over-time ticks (sickle bleed). Enemies skip their stagger for these:
        /// a bleed that re-staggered every half second would lock the rusher out of attacking
        /// entirely, which would make the sickle a stun-lock rather than a damage choice.
        /// </summary>
        public bool IgnoresStagger;
    }
}
