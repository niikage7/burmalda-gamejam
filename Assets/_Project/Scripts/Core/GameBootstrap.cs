using UnityEngine;

namespace TankBrawl.Core
{
    /// <summary>Global app settings applied once before the first scene loads.</summary>
    public static class GameBootstrap
    {
        const int TargetFrameRate = 60;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            Application.targetFrameRate = TargetFrameRate;
        }
    }
}
