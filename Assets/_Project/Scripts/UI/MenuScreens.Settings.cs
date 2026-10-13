using System;
using TankBrawl.AI;
using TankBrawl.Audio;
using TankBrawl.Progress;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TankBrawl.UI
{
    public partial class MenuScreens
    {
        const int VolumeBlocks = 10;

        GameObject settingsModal;
        GameObject confirmModal;
        readonly System.Collections.Generic.List<Action> settingsRefresh = new System.Collections.Generic.List<Action>();

        static readonly UiStyle.Tone OffTone = new UiStyle.Tone("#8297b0", "#9fb2c8", "#5e6f89");

        static Text InkLabel(Transform parent, string name, string text, int size, TextAnchor align)
        {
            Text label = UiStyle.Label(parent, name, text, size, UiStyle.Ink, align, false);
            label.GetComponent<UiTextOutline>().Width = 0f;
            return label;
        }

        void OpenSettings()
        {
            AudioManager.Play(Sfx.UiOpen);
            foreach (Action refresh in settingsRefresh)
                refresh();
            settingsModal.SetActive(true);
        }

        // A full-screen pop-up layer: a dim backdrop that swallows clicks.
        GameObject NewModal(string name, int order)
        {
            Canvas canvas = UiKit.CreateCanvas(name, order, transform);
            var root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            UiKit.Stretch((RectTransform)root.transform);
            Image dim = UiKit.CreateImage(root.transform, "Dim", new Color(0.078f, 0.063f, 0.059f, 0.72f), null, true);
            UiKit.Stretch(dim.rectTransform);
            return root;
        }

        void BuildSettings()
        {
            settingsModal = NewModal("SettingsCanvas", Order + 5);
            UiPlate panel = UiStyle.Panel(settingsModal.transform, "Panel", 30f);
            UiLayout.Mid(panel.rectTransform, 0f, 0f, 1000f, 700f);
            panel.Depth = 14f;

            Text title = UiStyle.Label(panel.transform, "Title", "НАСТРОЙКИ", 66, Color.white, TextAnchor.MiddleLeft);
            UiLayout.LT(title.rectTransform, 40f, 22f, 700f, 90f);

            UiButton close = UiStyle.IconButton(panel.transform, "Close", UiKit.Theme != null ? UiKit.Theme.closeIcon : null, 92f, () => { AudioManager.Play(Sfx.UiBack); settingsModal.SetActive(false); }, UiStyle.Orange);
            UiLayout.RT(close.Plate.rectTransform, -26f, -26f, 92f, 92f);

            VolumeRow(panel.transform, "Музыка", 126f, () => PlayerProgress.MusicVolume, value => PlayerProgress.MusicVolume = value);
            VolumeRow(panel.transform, "Звуки", 236f, () => PlayerProgress.SfxVolume, value => PlayerProgress.SfxVolume = value);
            ToggleRow(panel.transform, "Вибрация", "При выстреле и получении урона", 346f, () => PlayerProgress.Vibration, value => PlayerProgress.Vibration = value);
            ToggleRow(panel.transform, "Тряска экрана", "При попаданиях и взрывах", 466f, () => PlayerProgress.ScreenShakeOn, value => PlayerProgress.ScreenShakeOn = value);

            Text reset = Tag(panel.transform, "Reset", "Сбросить прогресс", 22, Color.white, UiStyle.Ink);
            SizeTag(reset, 40f, 610f, 270f, 46f);
            var resetButton = reset.transform.parent.gameObject.AddComponent<Button>();
            resetButton.transition = Selectable.Transition.None;
            reset.transform.parent.GetComponent<UiPlate>().raycastTarget = true;
            resetButton.onClick.AddListener(() => { AudioManager.Play(Sfx.UiClick); confirmModal.SetActive(true); });

            Text debug = Tag(panel.transform, "Debug", "Отладка", 22, Color.white, UiStyle.Ink);
            SizeTag(debug, 700f, 610f, 180f, 46f);
            var debugButton = debug.transform.parent.gameObject.AddComponent<Button>();
            debugButton.transition = Selectable.Transition.None;
            debug.transform.parent.GetComponent<UiPlate>().raycastTarget = true;
            debugButton.onClick.AddListener(() =>
            {
                AudioManager.Play(Sfx.UiClick);
                settingsModal.SetActive(false);
                Show(Page.Debug, true);
            });

            settingsModal.SetActive(false);
        }

        void VolumeRow(Transform parent, string label, float top, Func<float> get, Action<float> set)
        {
            UiPlate row = UiStyle.Cream(parent, label, 18f, 4f, 6f);
            UiLayout.LT(row.rectTransform, 40f, top, 920f, 94f);
            Text text = InkLabel(row.transform, "Label", label, 36, TextAnchor.MiddleLeft);
            UiLayout.LT(text.rectTransform, 22f, 0f, 200f, 90f);
            Text value = InkLabel(row.transform, "Value", string.Empty, 36, TextAnchor.MiddleRight);
            UiLayout.RT(value.rectTransform, 22f, 0f, 70f, 90f);

            var blocks = new UiPlate[VolumeBlocks];
            for (int i = 0; i < VolumeBlocks; i++)
            {
                int index = i;
                UiPlate block = UiStyle.Plate(row.transform, "Block" + i, UiStyle.Green, 9f, 3f, 0f, 6f, 0f);
                UiLayout.LT(block.rectTransform, 226f + i * 62f, 18f, 54f, 58f);
                block.raycastTarget = true;
                var button = block.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() =>
                {
                    float current = get();
                    set(Mathf.Approximately(current, (index + 1) / (float)VolumeBlocks) && index == 0 ? 0f : (index + 1) / (float)VolumeBlocks);
                    AudioManager.Play(Sfx.UiClick);
                    Paint();
                });
                blocks[i] = block;
            }

            void Paint()
            {
                int filled = Mathf.RoundToInt(get() * VolumeBlocks);
                for (int i = 0; i < VolumeBlocks; i++)
                    blocks[i].SetTone(i < filled ? UiStyle.Green : UiStyle.Gray);
                value.text = filled.ToString();
            }

            settingsRefresh.Add(Paint);
            Paint();
        }

        void ToggleRow(Transform parent, string label, string hint, float top, Func<bool> get, Action<bool> set)
        {
            UiPlate row = UiStyle.Cream(parent, label, 18f, 4f, 6f);
            UiLayout.LT(row.rectTransform, 40f, top, 920f, 104f);
            row.raycastTarget = true;
            Text text = InkLabel(row.transform, "Label", label, 36, TextAnchor.UpperLeft);
            UiLayout.LT(text.rectTransform, 22f, 8f, 600f, 48f);
            Text hintText = UiStyle.Plain(row.transform, "Hint", hint, 22, UiStyle.SteelText, TextAnchor.UpperLeft);
            UiLayout.LT(hintText.rectTransform, 22f, 56f, 600f, 36f);

            UiPlate track = UiStyle.Plate(row.transform, "Track", UiStyle.Green, 999f, 4f, 0f, 7f, 0f);
            UiLayout.RT(track.rectTransform, 22f, 14f, 168f, 76f);
            UiPlate knob = UiStyle.Plate(track.transform, "Knob", UiStyle.Light, 999f, 4f, 0f, 6f, 0f);
            Text state = UiStyle.Label(track.transform, "State", "ВКЛ", 26, Color.white, TextAnchor.MiddleCenter, false);

            void Paint()
            {
                bool on = get();
                track.SetTone(on ? UiStyle.Green : OffTone);
                UiLayout.LT(knob.rectTransform, on ? 100f : 4f, 4f, 64f, 64f);
                UiLayout.LT(state.rectTransform, on ? 8f : 76f, 0f, 84f, 68f);
                state.text = on ? "ВКЛ" : "ВЫКЛ";
            }

            var button = row.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() =>
            {
                set(!get());
                AudioManager.Play(Sfx.UiClick);
                Paint();
            });
            settingsRefresh.Add(Paint);
            Paint();
        }

        void BuildConfirm()
        {
            confirmModal = NewModal("ConfirmCanvas", Order + 6);
            UiPlate panel = UiStyle.Panel(confirmModal.transform, "Panel", 30f);
            UiLayout.Mid(panel.rectTransform, 0f, 0f, 900f, 440f);
            panel.Depth = 14f;

            Text text = UiStyle.Label(panel.transform, "Text", "Сбросить шестерёнки, улучшения и статистику?\nЭто нельзя отменить.", 38, Color.white);
            UiLayout.LT(text.rectTransform, 30f, 30f, 840f, 200f);

            UiButton yes = UiStyle.Button(panel.transform, "Yes", "СБРОСИТЬ", UiStyle.Red, new Vector2(380f, 100f), () =>
            {
                PlayerProgress.ResetProgress();
                confirmModal.SetActive(false);
            }, 42);
            UiLayout.LB(yes.Plate.rectTransform, 50f, 52f, 380f, 100f);

            UiButton no = UiStyle.Button(panel.transform, "No", "ОТМЕНА", UiStyle.Steel, new Vector2(380f, 100f), () => confirmModal.SetActive(false), 42);
            UiLayout.RB(no.Plate.rectTransform, 50f, 52f, 380f, 100f);

            confirmModal.SetActive(false);
        }

        void BuildDebug()
        {
            RectTransform root = NewPage(Page.Debug);
            Text header = UiStyle.Label(root, "Header", "ОТЛАДКА", 80, Color.white, TextAnchor.MiddleLeft);
            UiLayout.LT(header.rectTransform, 176f, 28f, 1000f, 110f);

            int count = debugScenes != null ? debugScenes.Length : 0;
            const int columns = 3;
            const float width = 520f;
            const float height = 96f;
            for (int i = 0; i < count; i++)
            {
                int column = i % columns;
                int row = i / columns;
                float x = (column - (columns - 1) * 0.5f) * (width + 30f);
                float y = 140f - row * (height + 26f);

                string sceneName = debugScenes[i].sceneName;
                bool available = Application.CanStreamedLevelBeLoaded(sceneName);
                string label = available ? debugScenes[i].label : debugScenes[i].label + " (нет в сборке)";
                UiButton button = UiStyle.Button(root, "Scene_" + sceneName, label, UiStyle.Blue, new Vector2(width, height), () => { DifficultyConfig.Selected = -1; SceneManager.LoadScene(sceneName); }, 32);
                UiLayout.Mid(button.Plate.rectTransform, x, y, width, height);
                button.interactable = available;
            }

            UiButton gift = UiStyle.Button(root, "Gift", "+" + DebugGift + " шестерёнок", UiStyle.Green, new Vector2(width, height), () =>
            {
                PlayerProgress.AddGears(DebugGift);
                AudioManager.Play(Sfx.GearGain);
            }, 32);
            UiLayout.BottomMid(gift.Plate.rectTransform, -(width + 30f), 60f, width, height);

            UiButton unlock = UiStyle.Button(root, "Unlock", "Открыть все сложности", UiStyle.Blue, new Vector2(width, height), () =>
            {
                PlayerProgress.UnlockAllDifficulties(DifficultyConfig.Count);
                AudioManager.Play(Sfx.GearGain);
            }, 30);
            UiLayout.BottomMid(unlock.Plate.rectTransform, 0f, 60f, width, height);

            UiButton reset = UiStyle.Button(root, "Reset", "Сбросить прогресс", UiStyle.Red, new Vector2(width, height), () => confirmModal.SetActive(true), 32);
            UiLayout.BottomMid(reset.Plate.rectTransform, width + 30f, 60f, width, height);
        }
    }
}
