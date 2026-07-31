namespace Kagemura.Systems
{
    /// <summary>
    /// Anything that can be hit — enemies, the player, breakable props.
    /// Weapons talk to this interface only, so they never need to know the concrete type.
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(in DamageInfo info);
    }
}
