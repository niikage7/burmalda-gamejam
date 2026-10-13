namespace TankBrawl.Core
{
    /// <summary>
    /// What a projectile has hit decides which of its two hitboxes counts:
    /// units are hit by the large one, terrain by the small one.
    /// </summary>
    public enum DamageableKind
    {
        /// <summary>Tanks and bases: take <see cref="DamageInfo.Amount"/>, hit by the large hitbox.</summary>
        Unit,

        /// <summary>Destructible map blocks: take <see cref="DamageInfo.BlockDamage"/>, hit by the small hitbox.</summary>
        Terrain
    }

    /// <summary>Anything that takes damage: tanks, bricks, bases.</summary>
    public interface IDamageable
    {
        /// <summary>Decides which projectile hitbox hits this object, see <see cref="DamageableKind"/>.</summary>
        DamageableKind Kind { get; }

        /// <summary>Applies one hit. Implementations take only the part of <paramref name="info"/> that concerns them.</summary>
        void TakeDamage(in DamageInfo info);
    }
}
