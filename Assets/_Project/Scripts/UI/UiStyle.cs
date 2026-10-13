using TankBrawl.Audio;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace TankBrawl.UI
{
    /// <summary>
    /// The look of the menu, pause and result screens, from tanks_art_style.html: flat plates with a thick near-black outline,
    /// a light band on top and a darker face at the bottom, a hard shadow under them, white rounded lettering with a black
    /// outline. Palette, tones (body / light / face) and the builders of the pieces. The score board and the player's bars keep
    /// their own style (<see cref="UiKit"/>, <see cref="UiShape"/>).
    /// </summary>
    public static class UiStyle
    {
        /// <summary>One colour family of a plate: the body, the light band on top, the dark face at the bottom.</summary>
        public readonly struct Tone
        {
            public readonly Color Body;
            public readonly Color Hi;
            public readonly Color Face;

            public Tone(string body, string hi, string face)
            {
                Body = Hex(body);
                Hi = Hex(hi);
                Face = Hex(face);
            }

            public Tone(Color body, Color hi, Color face)
            {
                Body = body;
                Hi = hi;
                Face = face;
            }
        }

        public static readonly Color Ink = UiPlate.Ink;
        public static readonly Color CreamColor = Hex("#fffdf6");
        public static readonly Color Yellow = Hex("#ffd84a");
        public static readonly Color SteelText = Hex("#5e6f89");
        public static readonly Color Grass = Hex("#9bd35d");
        public static readonly Color GoodText = Hex("#2f8a3a");
        public static readonly Color BadText = Hex("#d4302a");

        public static readonly Tone Green = new Tone("#5cb454", "#86d16f", "#3a8a36");
        public static readonly Tone Blue = new Tone("#009cfc", "#5cc3ff", "#0b6fb8");
        public static readonly Tone Red = new Tone("#f42824", "#ff6a5e", "#a8161a");
        public static readonly Tone Orange = new Tone("#e2774a", "#f6a173", "#a8442b");
        public static readonly Tone Steel = new Tone("#9fb2c8", "#d6e2ee", "#5e6f89");
        public static readonly Tone Sand = new Tone("#d0b038", "#ecd36a", "#9a8122");
        public static readonly Tone Gold = new Tone("#ffd84a", "#fff1a0", "#c89a1e");
        public static readonly Tone Water = new Tone("#36b4f2", "#74d7fb", "#1b6fb8");
        public static readonly Tone Light = new Tone("#fffdf6", "#ffffff", "#c3d3e4");
        public static readonly Tone Dark = new Tone("#2c3446", "#3c4760", "#1c2230");
        public static readonly Tone Gray = new Tone("#c3d3e4", "#e4eef8", "#8297b0");

        // Team colours in the order of Team: blue, red, green, yellow.
        public static readonly Tone[] TeamTones = { Blue, Red, Green, Sand };

        public static Color Hex(string html)
        {
            ColorUtility.TryParseHtmlString(html, out Color color);
            return color;
        }

        // ---- Fonts ----

        public static Font Display
        {
            get
            {
                UiTheme theme = UiKit.Theme;
                return theme != null && theme.displayFont != null ? theme.displayFont : UiKit.Font;
            }
        }

        public static Font Body
        {
            get
            {
                UiTheme theme = UiKit.Theme;
                return theme != null && theme.bodyFont != null ? theme.bodyFont : Display;
            }
        }

        // ---- Plates ----

        /// <summary>A plate. Depth is the hard shadow below it; face and hi are the bottom and top bands (0 = none).</summary>
        public static UiPlate Plate(Transform parent, string name, Tone tone, float radius = 18f, float outline = 4f, float depth = 0f, float face = 0f, float hi = 0f)
        {
            UiPlate plate = UiPlate.Create(parent, name);
            plate.SetTone(tone);
            plate.Radius = radius;
            plate.OutlineWidth = outline;
            plate.Depth = depth;
            plate.FaceHeight = face;
            plate.HiHeight = hi;
            return plate;
        }

        /// <summary>The big steel panel that holds a group of controls.</summary>
        public static UiPlate Panel(Transform parent, string name, float radius = 24f)
        {
            return Plate(parent, name, Steel, radius, 4f, 10f, 14f, 8f);
        }

        /// <summary>A cream box for text and values, with the outline and no shadow.</summary>
        public static UiPlate Cream(Transform parent, string name, float radius = 14f, float outline = 3f, float depth = 0f)
        {
            return Plate(parent, name, Light, radius, outline, depth, 0f, 0f);
        }

        /// <summary>A rounded dark (or any flat colour) capsule without outline: counters, tags.</summary>
        public static UiPlate Pill(Transform parent, string name, Color color)
        {
            UiPlate plate = UiPlate.Create(parent, name);
            plate.color = color;
            plate.Radius = 999f;
            plate.OutlineWidth = 0f;
            return plate;
        }

        // ---- Text ----

        /// <summary>
        /// Lettering: the display font, the colour (white by default), the black outline sized to the text and, with
        /// <paramref name="drop"/>, the hard drop under it. Not meant for small running text: use <see cref="Plain"/> there.
        /// </summary>
        public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool drop = true)
        {
            Text label = MakeText(parent, name, text, size, color, align, Display);
            var outline = label.gameObject.AddComponent<UiTextOutline>();
            float width = Mathf.Max(1.5f, Mathf.Round(size * 0.065f));
            outline.Width = width;
            outline.Drop = drop && size >= 26 ? width * 1.8f : 0f;
            return label;
        }

        /// <summary>Dark running text in the body font (on cream plates), no outline.</summary>
        public static Text Plain(Transform parent, string name, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft)
        {
            return MakeText(parent, name, text, size, color, align, Body);
        }

        static Text MakeText(Transform parent, string name, string text, int size, Color color, TextAnchor align, Font font)
        {
            RectTransform rect = UiKit.CreateRect(name, parent);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = font;
            label.fontSize = size;
            label.alignment = align;
            label.color = color;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.supportRichText = true;
            label.lineSpacing = 1f;
            label.text = text;
            return label;
        }

        // ---- Pieces ----

        /// <summary>A picture inside a rect of the given size (aspect kept). Without a sprite the image is hidden.</summary>
        public static Image Icon(Transform parent, string name, Sprite sprite, float size)
        {
            Image image = UiKit.CreateImage(parent, name, Color.white, sprite);
            image.preserveAspect = true;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            image.enabled = sprite != null;
            return image;
        }

        /// <summary>
        /// A button: a plate with a lettered label that sinks into its shadow when pressed. The label (and anything else put
        /// into <see cref="UiButton.Content"/>) sinks with it. Plays the click sound.
        /// </summary>
        public static UiButton Button(Transform parent, string name, string label, Tone tone, Vector2 size, UnityAction onClick, int fontSize = 40)
        {
            float h = size.y;
            UiPlate plate = Plate(parent, name, tone, h * 0.2f, Mathf.Clamp(h * 0.045f, 3f, 5f), Mathf.Clamp(h * 0.09f, 5f, 11f), h * 0.1f, h * 0.22f);
            plate.raycastTarget = true;
            plate.rectTransform.sizeDelta = size;

            var button = plate.gameObject.AddComponent<UiButton>();
            button.Plate = plate;
            button.targetGraphic = plate;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            RectTransform content = UiKit.CreateRect("Content", plate.transform);
            UiKit.Stretch(content);
            button.Content = content;

            Text text = Label(content, "Label", label, fontSize, Color.white);
            UiKit.Stretch(text.rectTransform);
            button.Label = text;

            button.onClick.AddListener(() => AudioManager.Play(Sfx.UiClick));
            if (onClick != null)
                button.onClick.AddListener(onClick);
            return button;
        }

        /// <summary>A square steel button with a picture (back, settings, pause).</summary>
        public static UiButton IconButton(Transform parent, string name, Sprite icon, float size, UnityAction onClick, Tone? tone = null)
        {
            UiButton button = Button(parent, name, string.Empty, tone ?? Steel, new Vector2(size, size), onClick);
            button.Plate.Radius = size * 0.2f;
            button.Plate.OutlineWidth = 4f;
            button.Plate.Depth = 8f;
            button.Plate.FaceHeight = size * 0.1f;
            button.Plate.HiHeight = size * 0.06f;
            button.Label.gameObject.SetActive(false);
            Image picture = Icon(button.Content, "Icon", icon, size * 0.55f);
            picture.rectTransform.anchorMin = picture.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            picture.rectTransform.anchoredPosition = Vector2.zero;
            return button;
        }
    }
}
