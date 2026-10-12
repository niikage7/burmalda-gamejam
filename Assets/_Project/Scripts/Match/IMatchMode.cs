namespace TankBrawl.Match
{
    /// <summary>
    /// Rules of one game mode. <see cref="MatchManager"/> owns the tanks, spawn points and respawning; the mode decides
    /// when rounds start and end, who scores and what the HUD says. Text properties must return a cached string that is
    /// rebuilt only when its content changes (the HUD reads them every frame).
    /// </summary>
    public interface IMatchMode
    {
        /// <summary>Called once, after the manager has collected the tanks. The mode starts the first round here.</summary>
        void Begin(MatchManager match);

        /// <summary>Called every frame.</summary>
        void Tick();

        /// <summary>A tank died. <paramref name="killer"/> is null when unknown (an environment or an already removed shooter). Called inside the damage callback: only record, act in <see cref="Tick"/>.</summary>
        void OnDied(MatchParticipant victim, MatchParticipant killer);

        /// <summary>Permanent line at the top of the screen (round, score, who is alive).</summary>
        string ScoreText { get; }

        /// <summary>Big message in the middle of the screen; null or empty = nothing.</summary>
        string BannerText { get; }
    }
}
