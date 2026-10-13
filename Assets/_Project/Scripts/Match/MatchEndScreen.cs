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
        UiPlate bannerPlate;
        Text headline;
        Text summary;
        Text scoreHeader;
        Text totalLabel;
        Text walletLabel;
        Text unlockLabel;
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

            Image dim = UiKit.CreateImage(root.transform, "Dim", new Color(0.078f, 0.063f, 0.059f, 0.72f), null, true);
            UiKit.Stretch(dim.rectTransform);

            UiPlate panel = UiStyle.Panel(root.transform, "Panel", 30f);
            panel.Depth = 14f;
            UiKit.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(1400f, 800f));

            // The banner sticks out above the panel, tilted; its colour is set in Show.
            bannerPlate = UiStyle.Plate(root.transform, "Banner", UiStyle.Green, 30f, 6f, 14f, 18f, 12f);
            UiLayout.TopMid(bannerPlate.rectTransform, 0f, 50f, 980f, 170f);
            bannerPlate.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2f);
            headline = UiStyle.Label(bannerPlate.transform, "Headline", string.Empty, 110, Color.white, TextAnchor.MiddleCenter);
            UiKit.Stretch(headline.rectTransform);

            summary = UiStyle.Label(panel.transform, "Summary", string.Empty, 30, Color.white, TextAnchor.MiddleCenter, false);
            UiKit.Place(summary.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(1300f, 50f));
            summary.rectTransform.pivot = new Vector2(0.5f, 1f);

            // Above the buttons, under the table: the line about a newly opened difficulty.
            unlockLabel = UiStyle.Label(panel.transform, "Unlock", string.Empty, 36, UiStyle.Yellow, TextAnchor.MiddleCenter, false);
            UiKit.Place(unlockLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(1200f, 60f));
            unlockLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
            unlockLabel.gameObject.SetActive(false);

            BuildTable(panel.transform);
            BuildRewards(panel.transform);
            BuildButtons(panel.transform);
            root.SetActive(false);
        }

        void BuildTable(Transform parent)
        {
            RectTransform table = UiKit.CreateRect("Table", parent);
            UiKit.Place(table, new Vector2(0f, 1f), new Vector2(50f, -170f), new Vector2(800f, 420f));
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
            UiPlate box = UiStyle.Plate(parent, "RewardBox", UiStyle.Dark, 20f, 4f, 6f, 0f, 0f);
            UiKit.Place(box.rectTransform, new Vector2(1f, 1f), new Vector2(-50f, -170f), new Vector2(460f, 420f));
            box.rectTransform.pivot = new Vector2(1f, 1f);

            Text title = UiStyle.Label(box.transform, "Title", "НАГРАДА", 34, UiStyle.Yellow, TextAnchor.MiddleCenter, false);
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

            totalLabel = UiStyle.Label(box.transform, "Total", string.Empty, 40, UiStyle.Yellow, TextAnchor.MiddleCenter, false);
            UiKit.Place(totalLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(420f, 56f));
            totalLabel.rectTransform.pivot = new Vector2(0.5f, 0f);

            walletLabel = UiKit.CreateText(box.transform, "Wallet", string.Empty, 26, UiKit.Muted, TextAnchor.MiddleCenter, FontStyle.Normal, false);
            UiKit.Place(walletLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(420f, 40f));
            walletLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
        }

        void BuildButtons(Transform parent)
        {
            AddButton(parent, "Again", "ЕЩЁ РАЗ", UiStyle.Green, -440f, () => match.RestartMatch());
            AddButton(parent, "Upgrades", "ГАРАЖ", UiStyle.Blue, 0f, () =>
            {
                MenuScreens.OpenUpgradesOnStart = true;
                match.ExitToMenu();
            });
            AddButton(parent, "Menu", "В МЕНЮ", UiStyle.Steel, 440f, () => match.ExitToMenu());
        }

        static void AddButton(Transform parent, string name, string label, UiStyle.Tone tone, float x, UnityEngine.Events.UnityAction onClick)
        {
            UiButton button = UiStyle.Button(parent, name, label, tone, new Vector2(400f, 110f), onClick, 48);
            UiLayout.BottomMid(button.Plate.rectTransform, x, 40f, 400f, 110f);
        }

        void Show(MatchResult result)
        {
            headline.text = result.Headline;
            bannerPlate.SetTone(result.HumanWon ? UiStyle.Green : UiStyle.Red);
            summary.text = result.ModeName + "   ·   " + result.Summary + "   ·   " + result.DifficultyName;
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
            unlockLabel.text = result.UnlockText ?? string.Empty;
            unlockLabel.gameObject.SetActive(!string.IsNullOrEmpty(result.UnlockText));
            root.SetActive(true);
            AudioManager.Play(result.HumanWon ? Sfx.RoundWin : Sfx.RoundLose);
        }
    }
}
