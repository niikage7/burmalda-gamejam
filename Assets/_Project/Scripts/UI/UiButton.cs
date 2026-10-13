using UnityEngine;
using UnityEngine.UI;

namespace TankBrawl.UI
{
    /// <summary>
    /// A button of the menu style (see <see cref="UiStyle.Button"/>): pressing sinks the plate into its shadow and moves the
    /// content with it, a disabled button turns grey. The built-in colour tint is off.
    /// </summary>
    public class UiButton : Button
    {
        public UiPlate Plate;

        /// <summary>Everything that sits on the plate (the label, icons): it follows the plate when it sinks.</summary>
        public RectTransform Content;

        public Text Label;

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            if (Plate == null)
                return;

            Plate.Pressed = state == SelectionState.Pressed ? 1f : 0f;
            Plate.Disabled = state == SelectionState.Disabled;
            if (Content != null)
                Content.anchoredPosition = new Vector2(0f, -Plate.PressOffset);

            if (Label != null)
                Label.canvasRenderer.SetAlpha(state == SelectionState.Disabled ? 0.6f : 1f);
        }

        public void SetText(string text)
        {
            if (Label != null)
                Label.text = text;
        }

        public void SetTone(UiStyle.Tone tone)
        {
            if (Plate != null)
                Plate.SetTone(tone);
        }
    }
}
