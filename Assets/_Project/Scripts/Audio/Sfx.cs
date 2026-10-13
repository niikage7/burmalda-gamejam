namespace TankBrawl.Audio
{
    /// <summary>
    /// Every sound the game can play. Gameplay code asks for an id, <see cref="SfxLibrary"/> says which clips it is.
    /// A new sound = a new line here + an entry in the library asset; an id without clips is silent.
    /// </summary>
    public enum Sfx
    {
        None = 0,

        // Shots (by projectile type, see WeaponConfig.fireSfx)
        ShotArmorPiercing,
        ShotFragment,
        ShotExplosive,
        ShotBot,

        // Hits and destruction
        HitTank,
        HitSelf,
        ShieldBlock,
        TankExplode,
        ExplosionBlast,
        HitWall,
        Ricochet,
        BrickBreak,
        BrickRestore,
        ShellClash,
        RamHit,

        // Tank systems
        NitroStart,
        LaserStart,
        AmmoSwitch,
        Respawn,

        // Power-ups
        PowerUpSpawn,
        PowerUpPickup,
        ShieldUp,

        // Match flow
        RoundStart,
        CountdownTick,
        RoundWin,
        RoundLose,
        MatchWin,
        MatchLose,
        KillConfirm,
        GearGain,
        ZoneCapture,

        // Interface
        UiClick,
        UiBack,
        UiBuy,
        UiDenied,
        UiOpen
    }
}
