using TankBrawl.Core;
using TankBrawl.Tank;
using UnityEngine;

namespace TankBrawl.Match
{
    /// <summary>One tank taking part in the match. Tanks are never destroyed, only deactivated, so the reference stays valid.</summary>
    public sealed class MatchParticipant
    {
        public readonly TeamMember Member;
        public readonly TankHealth Health;
        public readonly Rigidbody2D Body;

        /// <summary>The single human player (the one with a player input).</summary>
        public readonly bool IsHuman;

        /// <summary>"Я" for the human, "Бот N" for the others (numbered in the order they were found).</summary>
        public string Name;

        /// <summary>Number among the tanks of its team (the human first); picks the spawn point.</summary>
        public int SpawnIndex;

        public int Kills;
        public int Deaths;

        /// <summary>Points in modes with a personal score (deathmatch).</summary>
        public int Score;

        // Where the tank stood when the match began; used when its team has no spawn points.
        public Vector3 StartPosition;
        public float StartAngle;

        public MatchParticipant(TeamMember member, TankHealth health, bool isHuman)
        {
            Member = member;
            Health = health;
            IsHuman = isHuman;
            member.TryGetComponent(out Body);
            StartPosition = member.transform.position;
            StartAngle = member.transform.eulerAngles.z;
        }

        public Team Team => Member.Team;
        public GameObject Object => Member.gameObject;

        /// <summary>On the field and not dying (a dying tank is still active for the rest of the damage callback).</summary>
        public bool IsAlive => Member.gameObject.activeInHierarchy && !Health.IsDead;
    }
}
