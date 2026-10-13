using UnityEngine;

namespace TankBrawl.Core
{
    /// <summary>One hit. Units take <see cref="Amount"/> HP, destructible blocks take <see cref="BlockDamage"/> durability.</summary>
    public readonly struct DamageInfo
    {
        /// <summary>Hit points taken from a unit (tank, base).</summary>
        public readonly float Amount;

        /// <summary>Durability taken from a destructible block (brick). Ignored by units.</summary>
        public readonly int BlockDamage;

        /// <summary>World point of the hit. For terrain it lies just inside the struck surface, so it maps to the right tile.</summary>
        public readonly Vector2 Point;

        /// <summary>Direction the damage was travelling in.</summary>
        public readonly Vector2 Direction;

        /// <summary>Who dealt the damage (the shooter's root object); may be null.</summary>
        public readonly GameObject Source;

        public DamageInfo(float amount, int blockDamage, Vector2 point, Vector2 direction, GameObject source)
        {
            Amount = amount;
            BlockDamage = blockDamage;
            Point = point;
            Direction = direction;
            Source = source;
        }
    }
}
