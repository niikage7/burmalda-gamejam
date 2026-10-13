using System;
using TankBrawl.Match;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace TankBrawl.UI
{
    /// <summary>
    /// Back key (Escape; the Android Back button arrives as Escape): in a match it pauses (or leaves the result screen),
    /// in the menu it walks one screen back and on the main screen quits the app, in any other scene it returns to the menu.
    /// Creates itself once per run, so no scene needs to contain it.
    /// </summary>
    public class SceneNavigator : MonoBehaviour
    {
        public const string MenuSceneName = "Menu";

        static SceneNavigator instance;

        /// <summary>The menu screens set this: return true when the back key was used up (one screen back).</summary>
        public static Func<bool> BackHandler { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            BackHandler = null;
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

        /// <summary>The "back" action: see the class summary.</summary>
        public static void GoBack()
        {
            if (MatchManager.Instance != null && MatchManager.Instance.Mode != null)
            {
                MatchManager.Instance.OnBackPressed();
                return;
            }

            if (SceneManager.GetActiveScene().name == MenuSceneName)
            {
                if (BackHandler != null && BackHandler())
                    return;

                Application.Quit();
            }
            else if (Application.CanStreamedLevelBeLoaded(MenuSceneName))
            {
                SceneManager.LoadScene(MenuSceneName);
            }
            else
            {
                Debug.LogWarning($"Scene '{MenuSceneName}' is not in Build Settings, cannot go back to the menu.");
            }
        }

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                GoBack();
        }
    }
}
