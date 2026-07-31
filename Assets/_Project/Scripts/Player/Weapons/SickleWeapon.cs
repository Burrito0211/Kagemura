using Kagemura.Systems;
using UnityEngine;

namespace Kagemura.Player.Weapons
{
    /// <summary>
    /// The aggressive close-range option (spec §2.2): fast, very short reach, poor damage per
    /// swing, and every hit stacks bleed. Build Order step 5, and the brief there is explicit —
    /// it has to feel meaningfully different from the sword, not like a faster one.
    ///
    /// The difference is where the damage comes from. The sword pays out per swing, so hit and
    /// retreat works. The sickle pays out over time and only while the stacks keep landing, so
    /// it asks the player to stay inside the rusher's attack range and dodge through swings
    /// rather than back off — the risk is the price of the damage.
    ///
    /// No combo: chasing a finisher would pull it back toward the sword's rhythm.
    /// </summary>
    public class SickleWeapon : WeaponBase
    {
        protected override void DoAttack(int facing)
        {
            int hits = PerformHit(facing, data.damage, out _);
            if (hits > 0) return;

            // Whiffing matters more here than with the sword: the reach is short enough that
            // missing is a real cost at this attack speed.
            Debug.Log("[Sickle] whiff");
        }

        protected override void OnHitTarget(Collider2D victim, in DamageInfo info)
        {
            if (!data.appliesBleed) return;

            Bleed.ApplyStack(victim.gameObject, data.bleedDamagePerStack, data.bleedTickInterval,
                             data.bleedDuration, data.bleedMaxStacks, gameObject);
        }
    }
}
