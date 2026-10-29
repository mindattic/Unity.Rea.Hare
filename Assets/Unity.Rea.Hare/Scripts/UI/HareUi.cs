using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Hare.UI
{
    /// <summary>
    /// The reusable widget kit every screen (HUD, menus, results, upgrade, story) composes in code: a plain
    /// element/label helper, buttons, a bar/gauge, a window/dialog, and <see cref="OutlineText"/>, the shared home
    /// for the original's <c>draw_text_outline</c> family. Styling lives in <c>Theme.uss</c>
    /// (<see cref="HarePanel.ThemeResourcePath"/>); nothing here hard-codes a colour or size that belongs there.
    /// </summary>
    public static class HareUi
    {
        /// <summary>The outline size most of the original's HUD text uses (<c>fnt_score</c>/<c>fnt_point</c> at
        /// size 1); dialogs and buttons pass 2 explicitly, matching gmlHARE's own mix of the two.</summary>
        public const int DefaultOutlineSize = 1;

        public static VisualElement El(string classes = null, VisualElement parent = null)
        {
            var e = new VisualElement();
            AddClasses(e, classes);
            parent?.Add(e);
            return e;
        }

        public static void AddClasses(VisualElement e, string classes)
        {
            if (string.IsNullOrEmpty(classes)) return;
            foreach (var c in classes.Split(' '))
                if (c.Length > 0) e.AddToClassList(c);
        }

        /// <summary>A plain, un-outlined label — most of the original's text goes through <see cref="OutlineText"/>
        /// instead, but a handful of screens (debug overlays, UH-T59) want flat text.</summary>
        public static Label Text(string text, string classes = null, VisualElement parent = null)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            AddClasses(l, classes);
            parent?.Add(l);
            return l;
        }

        // ── draw_text_outline family (scripts/draw_text_outline*.gml) ──────────────────────────────────────────

        /// <summary>draw_text_outline(x, y, size, text): black outline, white text, opaque.</summary>
        public static VisualElement OutlineText(string text, int outlineSize = DefaultOutlineSize, string classes = null, VisualElement parent = null) =>
            BuildOutlineText(text, outlineSize, Color.white, Color.black, 1f, classes, parent);

        /// <summary>draw_text_outline_alpha(x, y, size, text, alpha): black outline, white text, at <paramref name="alpha"/>.</summary>
        public static VisualElement OutlineTextAlpha(string text, int outlineSize, float alpha, string classes = null, VisualElement parent = null) =>
            BuildOutlineText(text, outlineSize, Color.white, Color.black, alpha, classes, parent);

        /// <summary>draw_text_outline_color(x, y, size, text, color, outlineColor): caller's colours, opaque.</summary>
        public static VisualElement OutlineTextColor(string text, int outlineSize, Color textColor, Color outlineColor, string classes = null, VisualElement parent = null) =>
            BuildOutlineText(text, outlineSize, textColor, outlineColor, 1f, classes, parent);

        /// <summary>draw_text_outline_color_alpha(x, y, size, text, color, outlineColor, alpha): every argument the original takes.</summary>
        public static VisualElement OutlineTextColorAlpha(string text, int outlineSize, Color textColor, Color outlineColor, float alpha, string classes = null, VisualElement parent = null) =>
            BuildOutlineText(text, outlineSize, textColor, outlineColor, alpha, classes, parent);

        /// <summary>
        /// Changes the text an <see cref="OutlineText"/> stack draws (every outline layer and the text layer), as a
        /// typewriter's next <c>draw_text_outline(x, y, size, output)</c> would, without rebuilding the stack.
        /// </summary>
        public static void SetOutlineText(VisualElement outlineText, string text)
        {
            if (outlineText == null) return;
            outlineText.Query<Label>().ForEach(l => l.text = text ?? "");
        }

        /// <summary>
        /// Reproduces the GML algorithm exactly rather than approximating it with a drop shadow: every one of the
        /// four scripts draws the string at every integer offset in the full <c>(2*size+1)^2</c> square around the
        /// origin in the outline colour (<c>for(i=x-size;i&lt;=x+size;i+=1) for(n=y-size;n&lt;=y+size;n+=1) draw_text(i,n,text)</c>),
        /// then once more at the origin in the text colour on top. Each GML <c>draw_text</c> call becomes one
        /// stacked <see cref="Label"/> at that pixel offset, in the same order (every outline layer, then the
        /// text layer last so it paints over the centre outline draw) — a square of labels, not a CSS shadow.
        /// </summary>
        static VisualElement BuildOutlineText(string text, int outlineSize, Color textColor, Color outlineColor, float alpha, string classes, VisualElement parent)
        {
            outlineSize = Mathf.Max(0, outlineSize);
            textColor.a *= alpha;
            outlineColor.a *= alpha;

            var box = new VisualElement { name = "outline-text", pickingMode = PickingMode.Ignore };
            box.AddToClassList("outline-text");
            box.style.overflow = Overflow.Visible;
            AddClasses(box, classes);

            for (int dy = -outlineSize; dy <= outlineSize; dy++)
            {
                for (int dx = -outlineSize; dx <= outlineSize; dx++)
                {
                    var layer = MakeLayer(text, outlineColor);
                    if (dx == 0 && dy == 0)
                    {
                        // The one in-flow child: sizes the box to the text's own footprint (or, if an ancestor
                        // stretches the box itself, to the box's stretched width — the same width every absolute
                        // layer below is pinned to, so the stack still lines up). It is also exactly one of the
                        // GML loop's (2*size+1)^2 outline draws (the centre one), so nothing is skipped.
                        layer.style.position = Position.Relative;
                    }
                    else
                    {
                        PinOffsetLayer(layer, dx, dy);
                    }
                    box.Add(layer);
                }
            }

            var main = MakeLayer(text, textColor);
            PinOffsetLayer(main, 0, 0);
            box.Add(main); // added last: GML's final draw_text, painted on top of every outline layer

            parent?.Add(box);
            return box;
        }

        /// <summary>
        /// Pins an absolutely positioned layer to all four edges, offset by (<paramref name="dx"/>, <paramref name="dy"/>),
        /// so it always matches the box's own size exactly (symmetric insets: <c>left = dx, right = -dx</c> gives a
        /// computed width equal to the container's, shifted by dx) rather than shrinking to its own intrinsic text
        /// size. Without this, a box stretched wider than its text by an ancestor (a window title, a HUD column)
        /// would keep the in-flow centre layer's text aligned by <c>-unity-text-align</c> while every absolute
        /// layer stayed shrink-wrapped and pinned to the box's left edge — visibly misaligning the outline from
        /// the text the moment the box isn't exactly text-sized.
        /// </summary>
        static void PinOffsetLayer(VisualElement layer, int dx, int dy)
        {
            layer.style.position = Position.Absolute;
            layer.style.left = dx;
            layer.style.right = -dx;
            layer.style.top = dy;
            layer.style.bottom = -dy;
        }

        static Label MakeLayer(string text, Color color)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore };
            l.style.color = color;
            l.style.whiteSpace = WhiteSpace.NoWrap;
            l.style.marginLeft = 0; l.style.marginRight = 0; l.style.marginTop = 0; l.style.marginBottom = 0;
            l.style.paddingLeft = 0; l.style.paddingRight = 0; l.style.paddingTop = 0; l.style.paddingBottom = 0;
            return l;
        }

        // ── Buttons ─────────────────────────────────────────────────────────────────────────────────────────

        /// <summary>A clickable control with outline text (gmlHARE's buttons are a sprite plus
        /// <c>draw_text_outline</c>, not a flat label) styled entirely by Theme.uss' <c>.hare-btn</c> class.</summary>
        public static Button Button(string text, Action onClick, string classes = null, VisualElement parent = null, int outlineSize = 2)
        {
            var b = new Button(onClick);
            b.AddToClassList("hare-btn");
            AddClasses(b, classes);
            OutlineText(text, outlineSize, "hare-btn-label", b); // already pickingMode.Ignore (BuildOutlineText)
            parent?.Add(b);
            return b;
        }

        // ── Bars / gauges (health, energy, ammo) ───────────────────────────────────────────────────────────

        /// <summary>obj_energybar's own thresholds (HARE.gmx, its Draw event): <c>image_blend</c> is
        /// <c>c_red</c> at or below a third, <c>c_yellow</c> at or below two thirds, its (oddly named, but ported
        /// as defined in globals.gml) <c>c_lightgreen</c> above that. Not invented — reconstructed from the
        /// original's own bar, the only fill-bar gmlHARE draws.</summary>
        public const float LowThreshold = 1f / 3f;
        public const float MidThreshold = 2f / 3f;

        /// <summary>A horizontal fill bar (the energy and health bars): <paramref name="fraction"/> (0..1) wide,
        /// coloured by <see cref="LowThreshold"/>/<see cref="MidThreshold"/>.</summary>
        public static VisualElement Bar(float fraction, string classes = null, VisualElement parent = null)
        {
            var bar = El("hare-bar", parent);
            AddClasses(bar, classes);
            var fill = El("hare-bar-fill", bar);
            fill.name = "fill";
            SetBar(bar, fraction);
            return bar;
        }

        public static void SetBar(VisualElement bar, float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            var fill = bar.Q(name: "fill");
            if (fill == null) return;
            fill.style.width = Length.Percent(fraction * 100f);
            fill.EnableInClassList("hare-bar-fill--low", fraction <= LowThreshold);
            fill.EnableInClassList("hare-bar-fill--mid", fraction > LowThreshold && fraction <= MidThreshold);
        }

        // ── Windows / dialogs (obj_tip, obj_yesno_dialog) ──────────────────────────────────────────────────

        /// <summary>A bordered panel (<c>.hare-window</c>) with an optional outline-text title — the base every
        /// HUD panel, tip and dialog in the original shares.</summary>
        public static VisualElement Window(string title = null, string classes = null, VisualElement parent = null)
        {
            var w = El("hare-window", parent);
            AddClasses(w, classes);
            if (!string.IsNullOrEmpty(title)) OutlineText(title, 2, "hare-window-title", w);
            return w;
        }

        /// <summary>
        /// A modal message box over a dimming veil (obj_yesno_dialog/obj_tip): a title, a message, and one button
        /// per <paramref name="buttons"/> entry; clicking any of them removes the veil (and so the dialog) after
        /// running its callback. Returns the veil, in case the caller wants to dismiss it early.
        /// </summary>
        public static VisualElement Dialog(string title, string message, VisualElement parent, params (string Label, Action OnClick)[] buttons)
        {
            var veil = El("hare-veil fill", parent);
            var dialog = Window(title, "hare-dialog", veil);
            if (!string.IsNullOrEmpty(message)) OutlineText(message, 1, "hare-dialog-message", dialog);
            var row = El("row hare-dialog-buttons", dialog);
            foreach (var (label, onClick) in buttons)
            {
                Button(label, () =>
                {
                    onClick?.Invoke();
                    veil.RemoveFromHierarchy();
                }, "hare-btn--small", row);
            }
            return veil;
        }
    }
}
