using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TankBrawl.UI
{
    /// <summary>
    /// Back key (Escape; the Android Back button arrives as Escape): from any scene returns to the menu,
    /// in the menu quits the app. Creates itself once per run, so no scene needs to contain it.
    /// </summary>
    public class SceneNavigator : MonoBehaviour
    {
        /// <summary>Name of the menu scene; it must be in Build Settings.</summary>
        public const string MenuSceneName = "Menu";

        static SceneNavigator instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (instance != null)
                return;

            var host = new GameObject(nameof(SceneNavigator));
            DontDestroyOnLoad(host);
            instance = host.AddComponent<SceneNavigator>();
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;

            if (SceneManager.GetActiveScene().name == MenuSceneName)
                Application.Quit();
            else if (Application.CanStreamedLevelBeLoaded(MenuSceneName))
                SceneManager.LoadScene(MenuSceneName);
            else
                Debug.LogWarning($"Scene '{MenuSceneName}' is not in Build Settings, cannot go back to the menu.");
        }
    }
}
