using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Rea.Hare.UI;

namespace Unity.Rea.Hare.Popups
{
    /// <summary>
    /// Every obj_float_text drawn (U.HARE-T27): Core's <see cref="FloatTextField"/> as outlined fnt_point text
    /// (<see cref="HareTextStyles.FloatText"/>), each text centred on its x with its top at <c>y - 10</c>, in its flashing
    /// colour with its outline colour, at its alpha. The original draws them in the room (their Draw event), so they stay
    /// where they were made as the view moves; here they are drawn on the 1920x1080 GUI layer at their place in view 0
    /// (<see cref="Refresh"/> takes the view's top left each frame), under the HUD objects, which is where view 0 shows
    /// them (docs/BIBLE.md V35). The room steps the field; this only draws it.
    /// </summary>
    public sealed class FloatTextView : VisualElement
    {
        public const string ElementName = "obj_float_text";

        readonly FloatTextField field;
        readonly Dictionary<FloatText, Entry> shown = new Dictionary<FloatText, Entry>();
        readonly List<FloatText> gone = new List<FloatText>();

        sealed class Entry
        {
            public VisualElement Box;
            public VisualElement Text;
            public string Color;
        }

        FloatTextView(FloatTextField field, int width, int height)
        {
            this.field = field;
            name = ElementName;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.width = width;
            style.height = height;
            style.overflow = Overflow.Hidden; // view 0's edges
        }

        /// <summary>The texts' rules.</summary>
        public FloatTextField Field => field;

        /// <summary>
        /// Puts the layer on <paramref name="gui"/>, beneath whatever the GUI layer already holds (the HUD objects draw
        /// over the room).
        /// </summary>
        public static FloatTextView Show(VisualElement gui, FloatTextField field, int width, int height)
        {
            var view = new FloatTextView(field, width, height);
            gui.Insert(0, view);
            return view;
        }

        /// <summary>The element drawing <paramref name="text"/>, or null.</summary>
        public VisualElement ElementOf(FloatText text) => text != null && shown.TryGetValue(text, out var entry) ? entry.Box : null;

        /// <summary>How many texts are drawn.</summary>
        public int Count => shown.Count;

        /// <summary>Draws every text as it now is, with view 0's top left at room pixel (<paramref name="viewX"/>, <paramref name="viewY"/>).</summary>
        public void Refresh(float viewX, float viewY)
        {
            gone.Clear();
            foreach (var pair in shown)
                if (pair.Key.Gone || !Contains(pair.Key)) gone.Add(pair.Key);
            foreach (var text in gone)
            {
                shown[text].Box.RemoveFromHierarchy();
                shown.Remove(text);
            }

            foreach (var text in field.Texts)
            {
                if (!shown.TryGetValue(text, out var entry))
                {
                    entry = Build(text);
                    shown[text] = entry;
                }
                entry.Box.style.left = (float)text.X - viewX;
                entry.Box.style.top = (float)text.DrawY - viewY;
                entry.Box.style.opacity = text.DrawAlpha;
                if (entry.Color != text.Color)
                {
                    entry.Color = text.Color;
                    entry.Text.style.color = Parse(text.Color, Color.white);
                }
            }
        }

        bool Contains(FloatText text)
        {
            foreach (var t in field.Texts) if (ReferenceEquals(t, text)) return true;
            return false;
        }

        /// <summary>draw_set_halign(fa_center); draw_text_outline_color(x, y-10, 1, strText, color, outline).</summary>
        Entry Build(FloatText text)
        {
            var box = HareUi.OutlineTextColor(text.Text, text.Outline, Parse(text.Color, Color.white), Parse(text.OutlineColor, Color.black),
                HareTextStyles.FloatText.UssClass, this);
            box.name = ElementName + "-text";
            box.style.position = Position.Absolute;
            box.style.translate = new Translate(Length.Percent(-50), 0);
            box.style.unityTextAlign = TextAnchor.UpperCenter;
            return new Entry { Box = box, Text = box[box.childCount - 1], Color = text.Color };
        }

        static Color Parse(string html, Color fallback) => ColorUtility.TryParseHtmlString(html, out var color) ? color : fallback;
    }
}
