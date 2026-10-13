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

            Image dim = UiKit.CreateImage(root.transform, "Dim", new Color(0.078f, 0.063f, 0.059f, 0.6f), null, true);
            UiKit.Stretch(dim.rectTransform);

            UiPlate panel = UiStyle.Panel(root.transform, "Panel", 28f);
            panel.Depth = 14f;
            UiLayout.Mid(panel.rectTransform, 0f, 0f, 620f, 640f);

            Text title = UiStyle.Label(panel.transform, "Title", "ПАУЗА", 72, Color.white);
            UiLayout.LT(title.rectTransform, 0f, 24f, 620f, 100f);

            AddButton(panel.transform, "Continue", "ПРОДОЛЖИТЬ", UiStyle.Green, 150f, () => match.SetPaused(false));
            AddButton(panel.transform, "Restart", "ЗАНОВО", UiStyle.Blue, 290f, () => match.RestartMatch());
            AddButton(panel.transform, "Exit", "В МЕНЮ", UiStyle.Orange, 430f, () => match.ExitToMenu());

            root.SetActive(false);
        }

        static void AddButton(Transform parent, string name, string label, UiStyle.Tone tone, float top, UnityEngine.Events.UnityAction onClick)
        {
            UiButton button = UiStyle.Button(parent, name, label, tone, new Vector2(540f, 104f), onClick, 48);
            UiLayout.LT(button.Plate.rectTransform, 40f, top, 540f, 104f);
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
