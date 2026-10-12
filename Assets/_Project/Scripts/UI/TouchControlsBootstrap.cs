using TankBrawl.Input;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TankBrawl.UI
{
    /// <summary>
    /// Puts <c>Resources/TouchControls</c> into every loaded scene that contains a <see cref="PlayerTankInput"/>,
    /// so test scenes (and the future arena) need no manual setup. A scene that already has its own
    /// <see cref="TouchControls"/> instance is left alone.
    /// </summary>
    public static class TouchControlsBootstrap
    {
        const string ResourceName = "TouchControls";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            // Domain reload is off: re-subscribe without stacking handlers from the previous Play Mode run.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Ensure();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Ensure();
        }

        static void Ensure()
        {
            if (Object.FindAnyObjectByType<PlayerTankInput>(FindObjectsInactive.Include) == null)
                return;
            if (Object.FindAnyObjectByType<TouchControls>(FindObjectsInactive.Include) != null)
                return;

            TouchControls prefab = Resources.Load<TouchControls>(ResourceName);
            if (prefab == null)
            {
                Debug.LogWarning($"Resources/{ResourceName} prefab not found, touch controls are not created.");
                return;
            }

            Object.Instantiate(prefab).name = ResourceName;
        }
    }
}
