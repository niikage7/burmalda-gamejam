using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace TankBrawl.Map
{
    /// <summary>
    /// Unity 6.6 does not build the shapes of a <c>TilemapCollider2D</c> when a scene loads: the composite
    /// collider stays empty, so tanks and projectiles pass through walls. Switching the collider off and on builds it.
    /// Done once for every loaded scene, so no scene needs a component for this.
    /// </summary>
    public static class TilemapColliderBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            // Domain reload is off: without the unsubscribe every Play Mode run would add one more handler.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // The scene the game starts in; harmless if sceneLoaded has already handled it.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void RebuildStartScene()
        {
            RebuildAll();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RebuildAll();
        }

        static void RebuildAll()
        {
            TilemapCollider2D[] colliders = Object.FindObjectsByType<TilemapCollider2D>(FindObjectsInactive.Exclude);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (!colliders[i].enabled)
                    continue;

                colliders[i].enabled = false;
                colliders[i].enabled = true;
            }
        }
    }
}
