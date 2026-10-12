namespace TankBrawl.Core
{
    /// <summary>
    /// What a projectile has hit decides which of its two hitboxes counts:
    /// units are hit by the large one, terrain by the small one.
    /// </summary>
    public enum DamageableKind
    {
        Unit,
        Terrain
    }

    /// <summary>Anything that takes damage: tanks, bricks, bases.</summary>
    public interface IDamageable
    {
        DamageableKind Kind { get; }

        void TakeDamage(in DamageInfo info);
    }
}
