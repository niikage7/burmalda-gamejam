using TankBrawl.Core;
using TankBrawl.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TankBrawl.Match
{
    /// <summary>
    /// The kill list in the top right corner: "killer » victim", names in their team colours, newest on top, each line fades
    /// after <see cref="LineLifetime"/>. A tank with no known killer shows as "name погиб". Lines that involve the human
    /// get a dark backing so they are easy to spot.
    /// </summary>
    public class KillFeed : MonoBehaviour
    {
        const int MaxLines = 5;
        const float LineLifetime = 6f;
        const float FadeTime = 1f;
        const float LineHeight = 44f;

        sealed class Line
        {
            public Image Back;
            public Text Text;
            public float Born;
            public bool Used;
        }

        readonly Line[] lines = new Line[MaxLines];
        MatchManager match;

        public void Init(MatchManager owner)
        {
            match = owner;
            for (int i = 0; i < MaxLines; i++)
                lines[i] = BuildLine(i);

            match.KillRegistered += OnKill;
        }

        Line BuildLine(int index)
        {
            var line = new Line();
            line.Back = UiKit.CreateImage(transform, "Line" + index, new Color(0f, 0f, 0f, 0f));
            UiKit.Place(line.Back.rectTransform, new Vector2(1f, 1f), new Vector2(0f, -index * LineHeight), new Vector2(540f, LineHeight - 4f));

            line.Text = UiKit.CreateText(line.Back.transform, "Text", string.Empty, 28, Color.white, TextAnchor.MiddleRight, FontStyle.Bold);
            UiKit.Stretch(line.Text.rectTransform);
            line.Text.rectTransform.offsetMax = new Vector2(-10f, 0f);
            line.Back.gameObject.SetActive(false);
            return line;
        }

        void OnDestroy()
        {
            if (match != null)
                match.KillRegistered -= OnKill;
        }

        void OnKill(MatchParticipant victim, MatchParticipant killer)
        {
            // Shift the older lines down one place, then fill the first one.
            for (int i = MaxLines - 1; i > 0; i--)
                Copy(lines[i - 1], lines[i]);

            Line first = lines[0];
            first.Used = true;
            first.Born = Time.unscaledTime;
            first.Text.text = killer != null
                ? Colored(killer) + "  »  " + Colored(victim)
                : Colored(victim) + "  погиб";
            bool involvesHuman = victim.IsHuman || (killer != null && killer.IsHuman);
            first.Back.color = involvesHuman ? new Color(0f, 0f, 0f, 0.55f) : new Color(0f, 0f, 0f, 0.25f);
            first.Back.gameObject.SetActive(true);
        }

        static string Colored(MatchParticipant p)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(TeamColors.Of(p.Team)) + ">" + p.Name + "</color>";
        }

        static void Copy(Line from, Line to)
        {
            to.Used = from.Used;
            to.Born = from.Born;
            to.Text.text = from.Text.text;
            to.Back.color = from.Back.color;
            to.Back.gameObject.SetActive(from.Used);
        }

        void Update()
        {
            for (int i = 0; i < MaxLines; i++)
            {
                Line line = lines[i];
                if (!line.Used)
                    continue;

                float age = Time.unscaledTime - line.Born;
                if (age >= LineLifetime)
                {
                    line.Used = false;
                    line.Back.gameObject.SetActive(false);
                    continue;
                }

                // The text fades as one with its backing.
                float alpha = age > LineLifetime - FadeTime ? (LineLifetime - age) / FadeTime : 1f;
                line.Text.canvasRenderer.SetAlpha(alpha);
                line.Back.canvasRenderer.SetAlpha(alpha);
            }
        }
    }
}
