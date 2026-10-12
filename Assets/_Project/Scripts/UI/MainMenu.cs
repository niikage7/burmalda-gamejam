using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TankBrawl.UI
{
    /// <summary>
    /// Main menu: one button per entry of <c>entries</c>, each loads a scene by name.
    /// To add a test scene, add an entry here and put the scene into Build Settings.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        /// <summary>One menu button: the caption and the scene it loads.</summary>
        [Serializable]
        public struct Entry
        {
            public string label;

            /// <summary>Scene name as in Build Settings, without path and extension.</summary>
            public string sceneName;
        }

        const string MissingSceneSuffix = "  (нет в Build Settings)";

        [SerializeField] Entry[] entries;

        [Tooltip("Inactive button copied for every entry. Must have a Text child.")]
        [SerializeField] Button buttonTemplate;

        [Tooltip("Parent of the created buttons (a layout group).")]
        [SerializeField] RectTransform container;

        void Start()
        {
            if (buttonTemplate == null || container == null)
            {
                Debug.LogError($"{name}: button template or container is not assigned.", this);
                return;
            }

            buttonTemplate.gameObject.SetActive(false);

            for (int i = 0; i < entries.Length; i++)
                CreateButton(entries[i]);
        }

        void CreateButton(Entry entry)
        {
            Button button = Instantiate(buttonTemplate, container);
            button.gameObject.SetActive(true);
            button.gameObject.name = entry.sceneName;

            Text text = button.GetComponentInChildren<Text>();
            bool available = Application.CanStreamedLevelBeLoaded(entry.sceneName);
            if (text != null)
                text.text = available ? entry.label : entry.label + MissingSceneSuffix;

            if (!available)
            {
                Debug.LogWarning($"{name}: scene '{entry.sceneName}' is not in Build Settings, its button is disabled.", this);
                button.interactable = false;
                return;
            }

            string sceneName = entry.sceneName;
            button.onClick.AddListener(() => SceneManager.LoadScene(sceneName));
        }
    }
}
