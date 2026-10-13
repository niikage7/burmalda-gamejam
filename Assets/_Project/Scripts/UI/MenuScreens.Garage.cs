using TankBrawl.Audio;
using TankBrawl.Progress;
using UnityEngine;
using UnityEngine.UI;

namespace TankBrawl.UI
{
    public partial class MenuScreens
    {
        sealed class UpgradeCard
        {
            public UpgradeBranch Branch;
            public Text Level;
            public Text Value;
            public Text Next;
            public UiButton Buy;
            public Text Cost;
            public Text Lack;
            public Image BuyGear;
        }

        readonly System.Collections.Generic.List<UpgradeCard> upgradeCards = new System.Collections.Generic.List<UpgradeCard>();
        Text garageRank;
        Text garageMatches;
        Text rankBotHealth;
        Text rankBotDamage;

        const float CardWidth = 344f;
        const float CardHeight = 322f;

        void BuildGarage()
        {
            RectTransform root = NewPage(Page.Garage);
            UiTheme theme = UiKit.Theme;

            Text header = UiStyle.Label(root, "Header", "ГАРАЖ", 80, Color.white, TextAnchor.MiddleLeft);
            UiLayout.LT(header.rectTransform, 176f, 28f, 600f, 110f);

            BuildPodium(root, 470f, 0f, 0f, 300f, "Podium");
            UiLayout.CenterFromLT((RectTransform)root.Find("Podium"), 440f, 470f, 730f, 730f);

            // Under the podium: the tank's name and what the player has done.
            UiPlate tag = UiStyle.Plate(root, "Name", UiStyle.Blue, 18f, 4f, 8f, 9f, 8f);
            UiLayout.CenterFromLT(tag.rectTransform, 440f, 780f, 380f, 72f);
            Text tagText = UiStyle.Label(tag.transform, "Text", "МОЙ ТАНК", 44, Color.white);
            UiLayout.Fill(tagText.rectTransform, 0f, 0f, 0f, 6f);

            garageMatches = Tag(root, "Matches", string.Empty, 26, Color.white, UiStyle.Ink);
            UiLayout.CenterFromLT((RectTransform)garageMatches.transform.parent, 440f, 862f, 470f, 48f);
            garageMatches.fontStyle = FontStyle.Normal;

            UiPlate hint = UiStyle.Cream(root, "Hint", 16f, 3f, 5f);
            UiLayout.CenterFromLT(hint.rectTransform, 440f, 970f, 620f, 100f);
            Text hintText = UiStyle.Plain(hint.transform, "Text", "Улучшения бесконечны и действуют сразу. Но каждый купленный уровень усиливает и ботов.", 23, UiStyle.Ink, TextAnchor.MiddleCenter);
            hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiLayout.Fill(hintText.rectTransform, 18f, 6f, 18f, 6f);

            // The panel with the cards: three in a row, two rows (five upgrades and the threat rank).
            UiPlate panel = UiStyle.Panel(root, "Panel", 26f);
            UiLayout.RT(panel.rectTransform, 40f, 128f, 1120f, 880f);
            Text title = UiStyle.Label(panel.transform, "Title", "УЛУЧШЕНИЯ", 46, Color.white, TextAnchor.MiddleLeft);
            UiLayout.LT(title.rectTransform, 28f, 16f, 600f, 64f);
            Text note = UiStyle.Plain(panel.transform, "Note", "Действуют на все матчи", 24, UiStyle.Ink, TextAnchor.MiddleRight);
            UiLayout.RT(note.rectTransform, 30f, 26f, 460f, 44f);

            ProgressionConfig progression = PlayerProgress.Config;
            for (int i = 0; i < PlayerProgress.BranchCount; i++)
            {
                var branch = (UpgradeBranch)i;
                ProgressionConfig.Branch info = progression != null && progression.branches != null && i < progression.branches.Length ? progression.branches[i] : null;
                upgradeCards.Add(BuildCard(panel.transform, branch, info, i % 3, i / 3));
            }

            BuildRankTile(panel.transform, 2, 1);
        }

        static UiStyle.Tone ToneOf(UpgradeBranch branch)
        {
            switch (branch)
            {
                case UpgradeBranch.Hull: return UiStyle.Steel;
                case UpgradeBranch.Engine: return UiStyle.Green;
                case UpgradeBranch.Weapon: return UiStyle.Orange;
                case UpgradeBranch.FireRate: return UiStyle.Sand;
                default: return UiStyle.Water;
            }
        }

        static Vector2 CardOrigin(int column, int row)
        {
            return new Vector2(24f + column * (CardWidth + 20f), 124f + row * (CardHeight + 62f));
        }

        UpgradeCard BuildCard(Transform parent, UpgradeBranch branch, ProgressionConfig.Branch info, int column, int row)
        {
            var card = new UpgradeCard { Branch = branch };
            UiStyle.Tone tone = ToneOf(branch);
            Vector2 origin = CardOrigin(column, row);

            UiPlate plate = UiStyle.Plate(parent, "Card" + branch, tone, 22f, 4f, 8f, 12f, 8f);
            UiLayout.LT(plate.rectTransform, origin.x, origin.y, CardWidth, CardHeight);

            // The title tab sticks out above the card.
            UiPlate tab = UiStyle.Plate(plate.transform, "Tab", new UiStyle.Tone(tone.Face, tone.Hi, tone.Face), 14f, 4f, 5f, 0f, 0f);
            tab.SetTone(new UiStyle.Tone(tone.Face, new Color(1f, 1f, 1f, 0.22f), tone.Face));
            tab.HiHeight = 6f;
            UiLayout.TopMid(tab.rectTransform, 0f, -28f, 280f, 56f);
            tab.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2f);
            Text tabText = UiStyle.Label(tab.transform, "Text", (info != null ? info.displayName : branch.ToString()).ToUpperInvariant(), 34, Color.white, TextAnchor.MiddleCenter, false);
            UiLayout.Fill(tabText.rectTransform, 0f, 0f, 0f, 4f);

            // Icon on a cream disc, the level under it.
            UiPlate disc = UiStyle.Plate(plate.transform, "IconDisc", UiStyle.Light, 999f, 4f, 5f, 9f, 0f);
            UiLayout.LT(disc.rectTransform, 18f, 46f, 108f, 108f);
            Image icon = UiStyle.Icon(disc.transform, "Icon", info != null ? info.icon : null, 76f);
            UiLayout.Mid(icon.rectTransform, 0f, 2f, 76f, 76f);

            UiPlate chip = UiStyle.Pill(plate.transform, "LevelChip", UiStyle.Ink);
            UiLayout.LT(chip.rectTransform, 22f, 162f, 100f, 32f);
            card.Level = UiStyle.Label(chip.transform, "Text", string.Empty, 22, UiStyle.Yellow, TextAnchor.MiddleCenter, false);
            UiLayout.Fill(card.Level.rectTransform, 0f, 0f, 0f, 2f);

            // The stat now and after the next level.
            UiPlate valueBox = UiStyle.Cream(plate.transform, "ValueBox", 12f, 3f);
            UiLayout.LT(valueBox.rectTransform, 140f, 54f, 188f, 52f);
            card.Value = UiStyle.Label(valueBox.transform, "Value", string.Empty, 28, UiStyle.Ink, TextAnchor.MiddleCenter, false);
            card.Value.GetComponent<UiTextOutline>().Width = 0f;
            UiLayout.Fill(card.Value.rectTransform, 4f, 0f, 4f, 3f);

            card.Next = UiStyle.Label(plate.transform, "Next", string.Empty, 24, Color.white, TextAnchor.MiddleCenter, false);
            UiLayout.LT(card.Next.rectTransform, 140f, 112f, 188f, 34f);

            Text description = UiStyle.Plain(plate.transform, "Description", info != null ? info.description : string.Empty, 20, UiStyle.Ink, TextAnchor.UpperLeft);
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            description.verticalOverflow = VerticalWrapMode.Truncate;
            UiLayout.LT(description.rectTransform, 22f, 200f, 300f, 50f);

            UpgradeBranch captured = branch;
            card.Buy = UiStyle.Button(plate.transform, "Buy", string.Empty, UiStyle.Green, new Vector2(CardWidth - 44f, 66f), () => Buy(captured), 40);
            UiLayout.LT(card.Buy.Plate.rectTransform, 22f, 244f, CardWidth - 44f, 62f);
            card.Buy.Label.gameObject.SetActive(false);

            card.BuyGear = UiStyle.Icon(card.Buy.Content, "Gear", UiKit.Theme != null ? UiKit.Theme.gear : null, 44f);
            UiLayout.Mid(card.BuyGear.rectTransform, -62f, 0f, 44f, 44f);
            card.Cost = UiStyle.Label(card.Buy.Content, "Cost", "0", 40, Color.white, TextAnchor.MiddleLeft);
            UiLayout.Fill(card.Cost.rectTransform, 118f, 0f, 10f, 4f);

            // When the gears are short: how many are missing, on the button itself.
            card.Lack = UiStyle.Label(card.Buy.Content, "Lack", string.Empty, 17, UiStyle.Ink, TextAnchor.LowerCenter, false);
            card.Lack.GetComponent<UiTextOutline>().Width = 0f;
            UiLayout.Fill(card.Lack.rectTransform, 0f, 0f, 0f, 2f);
            return card;
        }

        void BuildRankTile(Transform parent, int column, int row)
        {
            Vector2 origin = CardOrigin(column, row);
            UiStyle.Tone tone = UiStyle.Dark;
            UiPlate plate = UiStyle.Plate(parent, "RankTile", tone, 22f, 4f, 8f, 12f, 8f);
            UiLayout.LT(plate.rectTransform, origin.x, origin.y, CardWidth, CardHeight);

            UiPlate tab = UiStyle.Plate(plate.transform, "Tab", UiStyle.Red, 14f, 4f, 5f, 8f, 6f);
            UiLayout.TopMid(tab.rectTransform, 0f, -28f, 300f, 56f);
            tab.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 2f);
            Text tabText = UiStyle.Label(tab.transform, "Text", "РАНГ УГРОЗЫ", 32, Color.white, TextAnchor.MiddleCenter, false);
            UiLayout.Fill(tabText.rectTransform, 0f, 0f, 0f, 4f);

            garageRank = UiStyle.Label(plate.transform, "Rank", "0", 110, UiStyle.Yellow, TextAnchor.MiddleCenter);
            UiLayout.LT(garageRank.rectTransform, 0f, 50f, CardWidth, 130f);

            Text line = UiStyle.Plain(plate.transform, "Text", "Чем выше ранг, тем сильнее боты. У них сейчас:", 20, Color.white, TextAnchor.MiddleCenter);
            line.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiLayout.LT(line.rectTransform, 24f, 186f, CardWidth - 48f, 54f);

            rankBotHealth = Tag(plate.transform, "BotHealth", string.Empty, 22, UiStyle.Ink, UiStyle.CreamColor);
            SizeTag(rankBotHealth, 24f, 248f, 150f, 40f);
            rankBotDamage = Tag(plate.transform, "BotDamage", string.Empty, 22, UiStyle.Ink, UiStyle.CreamColor);
            SizeTag(rankBotDamage, 182f, 248f, 138f, 40f);
        }

        void Buy(UpgradeBranch branch)
        {
            if (PlayerProgress.TryBuy(branch))
                AudioManager.Play(Sfx.UiBuy);
            else
                AudioManager.Play(Sfx.UiDenied);
        }

        void RefreshGarage()
        {
            ProgressionConfig progression = PlayerProgress.Config;
            if (progression == null || garageRank == null)
                return;

            foreach (UpgradeCard card in upgradeCards)
            {
                int level = PlayerProgress.Level(card.Branch);
                card.Level.text = "Ур. " + level;
                card.Value.text = progression.StatText(card.Branch, level);
                card.Next.text = "→ " + progression.StatText(card.Branch, level + 1);

                int cost = PlayerProgress.NextCost(card.Branch);
                bool canAfford = PlayerProgress.CanAfford(card.Branch);
                card.Cost.text = cost.ToString();
                card.Buy.interactable = canAfford;
                card.Lack.text = canAfford ? string.Empty : "не хватает " + (cost - PlayerProgress.Gears);
            }

            int rank = PlayerProgress.ThreatRank;
            garageRank.text = rank.ToString();
            rankBotHealth.text = "+" + 4 * rank + " HP";
            rankBotDamage.text = "+" + Mathf.RoundToInt(0.5f * rank) + " урона";
            garageMatches.text = "Матчей: " + PlayerProgress.MatchesPlayed + "  ·  Побед: " + PlayerProgress.MatchesWon;
        }
    }
}
