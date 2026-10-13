using TankBrawl.Audio;
using TankBrawl.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TankBrawl.Match
{
    /// <summary>The pause overlay (back key or the on-screen menu button): continue, restart the match, leave. Time stands still while it is up.</summary>
    public class PausePanel : MonoBehaviour
    {
        const int Order = 120;

        MatchManager match;
        GameObject root;

        public static PausePanel Create(MatchManager owner)
        {
            Canvas canvas = UiKit.CreateCanvas("PauseCanvas", Order, owner.transform);
            var panel = canvas.gameObject.AddComponent<PausePanel>();
            panel.match = owner;
            panel.Build(canvas);
            owner.PauseChanged += panel.OnPauseChanged;
            return panel;
        }

        void Build(Canvas canvas)
        {
            root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            UiKit.Stretch((RectTransform)root.transform);

            Image dim = UiKit.CreateImage(root.transform, "Dim", UiKit.Dim, null, true);
            UiKit.Stretch(dim.rectTransform);

            Image panel = UiKit.CreatePanel(root.transform, "Panel", UiKit.PanelColor);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 640f));

            Text title = UiKit.CreateText(panel.transform, "Title", "ПАУЗА", 80, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(600f, 100f));
            title.rectTransform.pivot = new Vector2(0.5f, 1f);

            AddButton(panel.transform, "Continue", "ПРОДОЛЖИТЬ", UiKit.ButtonGreen, 170f, () => match.SetPaused(false));
            AddButton(panel.transform, "Restart", "НАЧАТЬ ЗАНОВО", UiKit.ButtonBlue, 310f, () => match.RestartMatch());
            AddButton(panel.transform, "Exit", "В МЕНЮ", UiKit.ButtonGray, 450f, () => match.ExitToMenu());

            root.SetActive(false);
        }

        static void AddButton(Transform parent, string name, string label, Color color, float yFromTop, UnityEngine.Events.UnityAction onClick)
        {
            Button button = UiKit.CreateButton(parent, name, label, color, new Vector2(520f, 110f), onClick, 44);
            UiKit.Place(button.image.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -yFromTop), new Vector2(520f, 110f));
            button.image.rectTransform.pivot = new Vector2(0.5f, 1f);
        }

        void OnPauseChanged(bool paused)
        {
            root.SetActive(paused);
        }

        void OnDestroy()
        {
            if (match != null)
                match.PauseChanged -= OnPauseChanged;
        }
    }
}
