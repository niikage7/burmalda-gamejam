using TankBrawl.Audio;
using TankBrawl.Core;
using TankBrawl.Progress;
using TankBrawl.UI;
using UnityEngine;
using UnityEngine.UI;

namespace TankBrawl.Match
{
    /// <summary>
    /// The result screen: victory or defeat, the score line, the table of all tanks (kills, deaths, points), the reward
    /// breakdown with the total in gears and the wallet, and three buttons: play again, upgrades, menu.
    /// Built hidden by <see cref="MatchHud"/>, filled and shown when the manager reports the match finished.
    /// </summary>
    public class MatchEndScreen : MonoBehaviour
    {
        const int Order = 130;
        const int MaxRows = 6;
        const int MaxRewards = 4;
        const float RowHeight = 54f;

        sealed class Row
        {
            public GameObject Root;
            public Image Stripe;
            public Image Back;
            public Text Name;
            public Text Kills;
            public Text Deaths;
            public Text Score;
        }

        sealed class RewardRow
        {
            public GameObject Root;
            public Text Label;
            public Text Amount;
        }

        MatchManager match;
        GameObject root;
        Text headline;
        Text summary;
        Text scoreHeader;
        Text totalLabel;
        Text walletLabel;
        readonly Row[] rows = new Row[MaxRows];
        readonly RewardRow[] rewards = new RewardRow[MaxRewards];

        public static MatchEndScreen Create(MatchManager owner)
        {
            Canvas canvas = UiKit.CreateCanvas("ResultCanvas", Order, owner.transform);
            var screen = canvas.gameObject.AddComponent<MatchEndScreen>();
            screen.match = owner;
            screen.Build(canvas);
            owner.MatchFinished += screen.Show;
            return screen;
        }

        void OnDestroy()
        {
            if (match != null)
                match.MatchFinished -= Show;
        }

        void Build(Canvas canvas)
        {
            root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            UiKit.Stretch((RectTransform)root.transform);

            Image dim = UiKit.CreateImage(root.transform, "Dim", UiKit.Dim, null, true);
            UiKit.Stretch(dim.rectTransform);

            Image panel = UiKit.CreatePanel(root.transform, "Panel", UiKit.PanelColor);
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400f, 900f));

            headline = UiKit.CreateText(panel.transform, "Headline", string.Empty, 110, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(headline.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1200f, 130f));
            headline.rectTransform.pivot = new Vector2(0.5f, 1f);

            summary = UiKit.CreateText(panel.transform, "Summary", string.Empty, 36, UiKit.Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(summary.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1200f, 50f));
            summary.rectTransform.pivot = new Vector2(0.5f, 1f);

            BuildTable(panel.transform);
            BuildRewards(panel.transform);
            BuildButtons(panel.transform);
            root.SetActive(false);
        }

        void BuildTable(Transform parent)
        {
            RectTransform table = UiKit.CreateRect("Table", parent);
            UiKit.Place(table, new Vector2(0f, 1f), new Vector2(50f, -225f), new Vector2(800f, 420f));
            table.pivot = new Vector2(0f, 1f);

            // Header.
            AddCell(table, "HName", "Танк", 22f, 0f, 360f, TextAnchor.MiddleLeft, UiKit.Muted, 26);
            AddCell(table, "HKills", "Убийств", 420f, 0f, 120f, TextAnchor.MiddleCenter, UiKit.Muted, 26);
            AddCell(table, "HDeaths", "Смертей", 545f, 0f, 120f, TextAnchor.MiddleCenter, UiKit.Muted, 26);
            scoreHeader = AddCell(table, "HScore", "Очки", 670f, 0f, 120f, TextAnchor.MiddleCenter, UiKit.Muted, 26);

            for (int i = 0; i < MaxRows; i++)
            {
                float y = -(i + 1) * RowHeight - 6f;
                var row = new Row();
                row.Back = UiKit.CreateImage(table, "Row" + i, new Color(1f, 1f, 1f, 0.06f));
                UiKit.Place(row.Back.rectTransform, new Vector2(0f, 1f), new Vector2(0f, y), new Vector2(800f, RowHeight - 4f));
                row.Back.rectTransform.pivot = new Vector2(0f, 1f);
                row.Root = row.Back.gameObject;

                row.Stripe = UiKit.CreateImage(row.Back.transform, "Stripe", Color.white);
                row.Stripe.rectTransform.anchorMin = Vector2.zero;
                row.Stripe.rectTransform.anchorMax = new Vector2(0f, 1f);
                row.Stripe.rectTransform.offsetMin = Vector2.zero;
                row.Stripe.rectTransform.offsetMax = new Vector2(10f, 0f);

                row.Name = AddCell(row.Back.transform, "Name", string.Empty, 22f, 0f, 380f, TextAnchor.MiddleLeft, Color.white, 32, true);
                row.Kills = AddCell(row.Back.transform, "Kills", string.Empty, 420f, 0f, 120f, TextAnchor.MiddleCenter, Color.white, 32, true);
                row.Deaths = AddCell(row.Back.transform, "Deaths", string.Empty, 545f, 0f, 120f, TextAnchor.MiddleCenter, Color.white, 32, true);
                row.Score = AddCell(row.Back.transform, "Score", string.Empty, 670f, 0f, 120f, TextAnchor.MiddleCenter, UiKit.Accent, 32, true);
                rows[i] = row;
            }
        }

        // A text cell: left edge x, width w, filling the height of its parent when fillHeight, otherwise one header line.
        static Text AddCell(Transform parent, string name, string text, float x, float y, float width, TextAnchor align, Color color, int size, bool fillHeight = false)
        {
            Text label = UiKit.CreateText(parent, name, text, size, color, align, FontStyle.Bold, false);
            RectTransform rect = label.rectTransform;
            rect.anchorMin = new Vector2(0f, fillHeight ? 0f : 1f);
            rect.anchorMax = new Vector2(0f, fillHeight ? 1f : 1f);
            rect.pivot = new Vector2(0f, fillHeight ? 0.5f : 1f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, fillHeight ? 0f : 44f);
            return label;
        }

        void BuildRewards(Transform parent)
        {
            Image box = UiKit.CreateImage(parent, "RewardBox", UiKit.PanelLight);
            UiKit.Place(box.rectTransform, new Vector2(1f, 1f), new Vector2(-50f, -225f), new Vector2(460f, 420f));
            box.rectTransform.pivot = new Vector2(1f, 1f);

            Text title = UiKit.CreateText(box.transform, "Title", "НАГРАДА", 34, UiKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(420f, 50f));
            title.rectTransform.pivot = new Vector2(0.5f, 1f);

            for (int i = 0; i < MaxRewards; i++)
            {
                var row = new RewardRow();
                RectTransform rect = UiKit.CreateRect("Reward" + i, box.transform);
                UiKit.Place(rect, new Vector2(0.5f, 1f), new Vector2(0f, -76f - i * 62f), new Vector2(420f, 58f));
                rect.pivot = new Vector2(0.5f, 1f);
                row.Root = rect.gameObject;

                row.Label = UiKit.CreateText(rect, "Label", string.Empty, 26, Color.white, TextAnchor.MiddleLeft, FontStyle.Normal, false);
                row.Label.rectTransform.anchorMin = new Vector2(0f, 0f);
                row.Label.rectTransform.anchorMax = new Vector2(0.72f, 1f);
                row.Label.rectTransform.offsetMin = row.Label.rectTransform.offsetMax = Vector2.zero;
                row.Label.verticalOverflow = VerticalWrapMode.Truncate;
                row.Label.horizontalOverflow = HorizontalWrapMode.Wrap;

                row.Amount = UiKit.CreateText(rect, "Amount", string.Empty, 34, UiKit.Accent, TextAnchor.MiddleRight, FontStyle.Bold, false);
                row.Amount.rectTransform.anchorMin = new Vector2(0.7f, 0f);
                row.Amount.rectTransform.anchorMax = new Vector2(1f, 1f);
                row.Amount.rectTransform.offsetMin = row.Amount.rectTransform.offsetMax = Vector2.zero;
                rewards[i] = row;
            }

            totalLabel = UiKit.CreateText(box.transform, "Total", string.Empty, 40, UiKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(totalLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(420f, 56f));
            totalLabel.rectTransform.pivot = new Vector2(0.5f, 0f);

            walletLabel = UiKit.CreateText(box.transform, "Wallet", string.Empty, 26, UiKit.Muted, TextAnchor.MiddleCenter, FontStyle.Normal, false);
            UiKit.Place(walletLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(420f, 40f));
            walletLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
        }

        void BuildButtons(Transform parent)
        {
            AddButton(parent, "Again", "ЕЩЁ РАЗ", UiKit.ButtonGreen, -440f, () => match.RestartMatch());
            AddButton(parent, "Upgrades", "УЛУЧШЕНИЯ", UiKit.ButtonBlue, 0f, () =>
            {
                MenuScreens.OpenUpgradesOnStart = true;
                match.ExitToMenu();
            });
            AddButton(parent, "Menu", "В МЕНЮ", UiKit.ButtonGray, 440f, () => match.ExitToMenu());
        }

        static void AddButton(Transform parent, string name, string label, Color color, float x, UnityEngine.Events.UnityAction onClick)
        {
            Button button = UiKit.CreateButton(parent, name, label, color, new Vector2(400f, 110f), onClick, 44);
            UiKit.Place(button.image.rectTransform, new Vector2(0.5f, 0f), new Vector2(x, 36f), new Vector2(400f, 110f));
            button.image.rectTransform.pivot = new Vector2(0.5f, 0f);
        }

        void Show(MatchResult result)
        {
            headline.text = result.Headline;
            headline.color = result.HumanWon ? UiKit.Good : UiKit.Bad;
            summary.text = result.ModeName + "   ·   " + result.Summary;
            scoreHeader.gameObject.SetActive(result.ShowScoreColumn);

            for (int i = 0; i < MaxRows; i++)
            {
                Row row = rows[i];
                bool used = i < result.Rows.Count;
                row.Root.SetActive(used);
                if (!used)
                    continue;

                ResultRow data = result.Rows[i];
                row.Stripe.color = TeamColors.Of(data.Team);
                row.Back.color = data.IsHuman ? new Color(1f, 0.82f, 0.25f, 0.18f) : new Color(1f, 1f, 1f, 0.06f);
                row.Name.text = data.Name;
                row.Kills.text = data.Kills.ToString();
                row.Deaths.text = data.Deaths.ToString();
                row.Score.gameObject.SetActive(result.ShowScoreColumn);
                row.Score.text = data.Score.ToString();
            }

            for (int i = 0; i < MaxRewards; i++)
            {
                bool used = i < result.Rewards.Count;
                rewards[i].Root.SetActive(used);
                if (!used)
                    continue;

                rewards[i].Label.text = result.Rewards[i].Label;
                rewards[i].Amount.text = "+" + result.Rewards[i].Gears;
            }

            totalLabel.text = result.TotalGears > 0 ? "Итого: +" + result.TotalGears : "Наград нет";
            walletLabel.text = "Всего шестерёнок: " + PlayerProgress.Gears;
            root.SetActive(true);
            AudioManager.Play(result.HumanWon ? Sfx.RoundWin : Sfx.RoundLose);
        }
    }
}
