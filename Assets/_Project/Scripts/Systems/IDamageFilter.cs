namespace Kagamura.Systems
{
    /// <summary>
    /// Sits between a hit and the Health that would take it. Health looks for one of these on
    /// its own GameObject and gives it the last word on every incoming hit — so armour, guards,
    /// and resistances can exist without Health knowing what any of them are.
    ///
    /// This is the seam DamageInfo's own comment anticipates: damage types and resistances grow
    /// here rather than as more branches inside Health.
    /// </summary>
    public interface IDamageFilter
    {
        /// <summary>
        /// Inspect and optionally alter a hit before it lands. Return false to reject it
        /// outright — the hit then routes through Health.OnDamageAvoided, the same path a
        /// dodge or parry uses, so anything already listening for "that didn't land" gets it
        /// for free.
        /// </summary>
        bool FilterDamage(ref DamageInfo info);
    }
}
