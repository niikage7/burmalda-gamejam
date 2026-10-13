using TankBrawl.Audio;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace TankBrawl.UI
{
    /// <summary>
    /// Small helpers to build screens in code (menu, HUD, result screen): canvas, labels, buttons, bars. Art comes from
    /// <see cref="UiTheme"/> when it has sprites; otherwise everything is a plain coloured rectangle, so the game is fully usable
    /// without a single UI sprite. Text is the built-in UnityEngine.UI.Text (TextMeshPro is not imported).
    /// </summary>
    public static class UiKit
    {
        public const int ReferenceWidth = 1920;
        public const int ReferenceHeight = 1080;

        public static readonly Color Dim = new Color(0.03f, 0.04f, 0.07f, 0.82f);
        public static readonly Color PanelColor = new Color(0.11f, 0.14f, 0.2f, 1f);
        public static readonly Color PanelLight = new Color(0.17f, 0.21f, 0.3f, 1f);
        public static readonly Color ButtonBlue = new Color(0.2f, 0.45f, 0.85f, 1f);
        public static readonly Color ButtonGreen = new Color(0.22f, 0.65f, 0.35f, 1f);
        public static readonly Color ButtonGray = new Color(0.32f, 0.36f, 0.45f, 1f);
        public static readonly Color ButtonRed = new Color(0.75f, 0.27f, 0.25f, 1f);
        public static readonly Color Accent = new Color(1f, 0.82f, 0.25f, 1f);
        public static readonly Color Muted = new Color(0.68f, 0.72f, 0.8f, 1f);
        public static readonly Color Good = new Color(0.45f, 0.9f, 0.5f, 1f);
        public static readonly Color Bad = new Color(1f, 0.45f, 0.4f, 1f);

        const string ThemeResource = "UiTheme";

        static Font font;
        static UiTheme theme;
        static bool themeLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            font = null;
            theme = null;
            themeLoaded = false;
        }

        public static Font Font
        {
            get
            {
                if (font == null)
                    font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        /// <summary>The optional art set; null when there is none (everything is then drawn flat).</summary>
        public static UiTheme Theme
        {
            get
            {
                if (!themeLoaded)
                {
                    theme = Resources.Load<UiTheme>(ThemeResource);
                    themeLoaded = true;
                }

                return theme;
            }
        }

        // ---- Structure ----

        /// <summary>A full-screen overlay canvas that scales with the screen height (a button has the same physical size on any aspect ratio).</summary>
        public static Canvas CreateCanvas(string name, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null)
                go.transform.SetParent(parent, false);

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        /// <summary>A rectangle stretched over the screen's safe area (cutouts, rounded corners) inside <paramref name="parent"/>.</summary>
        public static RectTransform CreateSafeArea(Transform parent)
        {
            RectTransform rect = CreateRect("SafeArea", parent);
            Stretch(rect);
            rect.gameObject.AddComponent<SafeAreaPanel>();
            return rect;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rect, float margin = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(margin, margin);
            rect.offsetMax = new Vector2(-margin, -margin);
        }

        /// <summary>Places a rectangle by one anchor point: anchor and pivot are the same corner/edge/centre.</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static Image CreateImage(Transform parent, string name, Color color, Sprite sprite = null, bool raycast = false)
        {
            RectTransform rect = CreateRect(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            if (sprite != null)
            {
                image.sprite = sprite;
                image.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            }

            return image;
        }

        /// <summary>A panel in the theme's frame (flat colour without art).</summary>
        public static Image CreatePanel(Transform parent, string name, Color color, bool raycast = true)
        {
            UiTheme t = Theme;
            return CreateImage(parent, name, color, t != null ? t.panel : null, raycast);
        }

        public static Text CreateText(Transform parent, string name, string text, int size, Color color, TextAnchor alignment, FontStyle style = FontStyle.Normal, bool outline = true)
        {
            RectTransform rect = CreateRect(name, parent);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = Font;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = alignment;
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.supportRichText = true;
            label.text = text;

            if (outline)
            {
                var shadow = rect.gameObject.AddComponent<Outline>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
                shadow.effectDistance = new Vector2(2f, -2f);
            }

            return label;
        }

        /// <summary>A button with a centred label. Plays the click sound by itself.</summary>
        public static Button CreateButton(Transform parent, string name, string label, Color color, Vector2 size, UnityAction onClick, int fontSize = 40)
        {
            UiTheme t = Theme;
            Image image = CreateImage(parent, name, color, t != null ? t.button : null, true);
            image.rectTransform.sizeDelta = size;

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.7f);
            colors.colorMultiplier = 1f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            Text text = CreateText(image.transform, "Label", label, fontSize, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(text.rectTransform);

            button.onClick.AddListener(() => AudioManager.Play(Sfx.UiClick));
            if (onClick != null)
                button.onClick.AddListener(onClick);
            return button;
        }

        /// <summary>The gear currency icon: the theme's sprite, or a golden rotated square as a placeholder.</summary>
        public static Image CreateGearIcon(Transform parent, float size)
        {
            UiTheme t = Theme;
            Sprite sprite = t != null ? t.gear : null;
            Image icon = CreateImage(parent, "GearIcon", sprite != null ? Color.white : Accent, sprite);
            icon.rectTransform.sizeDelta = new Vector2(size, size);
            if (sprite == null)
                icon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            icon.preserveAspect = true;
            return icon;
        }

        public static void SetLabel(Button button, string text)
        {
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
                label.text = text;
        }

        /// <summary>A horizontal slider 0..1 built from three plain images.</summary>
        public static Slider CreateSlider(Transform parent, string name, Vector2 size, float value, UnityAction<float> onChanged)
        {
            Image background = CreateImage(parent, name, new Color(0.05f, 0.07f, 0.1f, 1f), null, true);
            background.rectTransform.sizeDelta = size;

            RectTransform fillArea = CreateRect("FillArea", background.transform);
            Stretch(fillArea, 6f);
            Image fill = CreateImage(fillArea, "Fill", ButtonBlue);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

            RectTransform handleArea = CreateRect("HandleArea", background.transform);
            Stretch(handleArea);
            handleArea.offsetMin = new Vector2(18f, 0f);
            handleArea.offsetMax = new Vector2(-18f, 0f);
            Image handle = CreateImage(handleArea, "Handle", Accent, null, true);
            handle.rectTransform.sizeDelta = new Vector2(36f, size.y + 16f);

            var slider = background.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            if (onChanged != null)
                slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        /// <summary>Makes sure there is an EventSystem with the Input System UI module, so buttons work on any screen.</summary>
        public static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem", typeof(EventSystem));
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
    }
}
