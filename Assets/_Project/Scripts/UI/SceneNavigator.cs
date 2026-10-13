using System;
using TankBrawl.Match;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TankBrawl.UI
{
    /// <summary>
    /// Back key (Escape; the Android Back button arrives as Escape): in a match it pauses (or leaves the result screen),
    /// in the menu it walks one screen back and on the main screen quits the app, in any other scene (a test arena) it returns
    /// to the menu after a second press within <see cref="ExitConfirmSeconds"/>, so a stray tap does not end the run.
    /// Creates itself once per run, so no scene needs to contain it.
    /// </summary>
    public class SceneNavigator : MonoBehaviour
    {
        /// <summary>Name of the menu scene; it must be in Build Settings.</summary>
        public const string MenuSceneName = "Menu";

        /// <summary>Outside a match and the menu, a second back press within this many seconds leaves to the menu.</summary>
        public const float ExitConfirmSeconds = 2f;

        const int HintOrder = 140;
        const string ExitHint = "Нажмите ещё раз, чтобы выйти в меню";

        static SceneNavigator instance;

        float exitArmedUntil;
        GameObject hint;

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
                if (instance != null && !instance.ConfirmExit())
                    return;

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

            if (hint != null && hint.activeSelf && Time.unscaledTime > exitArmedUntil)
                hint.SetActive(false);
        }

        // The first press arms the exit and shows the hint; returns true on the second press inside the window.
        bool ConfirmExit()
        {
            if (Time.unscaledTime <= exitArmedUntil)
            {
                exitArmedUntil = 0f;
                if (hint != null)
                    hint.SetActive(false);
                return true;
            }

            exitArmedUntil = Time.unscaledTime + ExitConfirmSeconds;
            ShowHint();
            return false;
        }

        void ShowHint()
        {
            if (hint == null)
            {
                Canvas canvas = UiKit.CreateCanvas("BackHintCanvas", HintOrder, transform);
                Image back = UiKit.CreatePanel(canvas.transform, "Hint", new Color(0f, 0f, 0f, 0.7f), false);
                UiKit.Place(back.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(1000f, 100f));
                Text text = UiKit.CreateText(back.transform, "Text", ExitHint, 40, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiKit.Stretch(text.rectTransform);
                hint = back.gameObject;
            }

            hint.SetActive(true);
        }
    }
}
