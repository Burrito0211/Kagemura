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
    }
}
