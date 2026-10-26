using System;
using System.Collections.Generic;
using System.Globalization;

namespace Unity.Hare.Popups
{
    /// <summary>
    /// One obj_float_text (U.HARE-T27), engine-free, in room pixels (y down). Its Step, once a room step: while
    /// <see cref="Flash"/>, <c>if(colorCycle &lt;= 4) colorCycle += 1; else colorCycle = 0</c> and the switch's colour
    /// (white, white, red, yellow, blue; colorCycle 5 matches no case and keeps blue); then, while <c>alpha &gt; 0</c>,
    /// <c>alpha -= fadespeed; y -= floatspeed</c>, and otherwise <c>alpha = 0; instance_destroy()</c>. Alpha is a double,
    /// as GameMaker's, so 0.01 off 1 a hundred times leaves a hair below 0 and a score popup goes on its 101st step. Its Draw (in the
    /// room, not the GUI layer) is <c>draw_text_outline_color(x, y-10, 1, strText, color, outline)</c>, fa_center,
    /// fnt_point, at <c>alpha</c>.
    /// </summary>
    public sealed class FloatText
    {
        readonly FloatTextDefinition definition;

        internal FloatText(FloatTextDefinition definition, double x, double y, string text)
        {
            this.definition = definition;
            X = x;
            Y = y;
            Text = text ?? "";
            Alpha = definition.StartAlpha;
            Color = definition.Color;
            OutlineColor = definition.OutlineColor;
            Flash = definition.Flash;
            FadeSpeed = definition.FadeSpeed;
            FloatSpeed = definition.FloatSpeed;
        }

        public double X { get; private set; }
        public double Y { get; private set; }
        /// <summary>GML strText.</summary>
        public string Text { get; }
        public double Alpha { get; private set; }
        public int ColorCycle { get; private set; }
        /// <summary>The text's colour, #RRGGBB.</summary>
        public string Color { get; internal set; }
        public string OutlineColor { get; internal set; }
        public bool Flash { get; internal set; }
        public double FadeSpeed { get; internal set; }
        public double FloatSpeed { get; internal set; }
        /// <summary>instance_destroy() ran.</summary>
        public bool Gone { get; private set; }
        /// <summary>How many steps it has run.</summary>
        public int Steps { get; private set; }

        /// <summary>Where its Draw puts the text's top (<c>y - 10</c>), fa_center on <see cref="X"/>.</summary>
        public double DrawY => Y + definition.DrawOffsetY;

        /// <summary>The alpha it draws at, clamped to 0..1 as draw_set_alpha clamps it.</summary>
        public float DrawAlpha => (float)Math.Max(0.0, Math.Min(1.0, Alpha));

        /// <summary>Its outline size.</summary>
        public int Outline => definition.Outline;

        /// <summary>One Step; false once it has destroyed itself.</summary>
        public bool Step()
        {
            if (Gone) return false;
            Steps++;
            if (Flash)
            {
                if (ColorCycle <= definition.FlashCycleTop) ColorCycle++;
                else ColorCycle = 0;
                var colors = definition.FlashColors;
                if (colors != null && ColorCycle < colors.Length) Color = colors[ColorCycle];
            }
            if (Alpha > 0)
            {
                Alpha -= FadeSpeed;
                Y -= FloatSpeed;
                return true;
            }
            Alpha = 0;
            Gone = true;
            return false;
        }
    }

    /// <summary>
    /// Every obj_float_text in the room (U.HARE-T27), stepped in creation order (GameMaker's instance order) by the room's
    /// clock, with show_float_text, show_float_text_ext and show_float_text_color to make them, and <see cref="ShowScore"/>
    /// for the one call the original actually makes: every <c>current_score += n</c> (a kill, a broken rock, the boss, a
    /// shrapnel piece) sits next to <c>show_float_text_ext(x, y, string(n), true, 0.01, 1)</c>.
    /// </summary>
    public sealed class FloatTextField
    {
        readonly List<FloatText> texts = new List<FloatText>();

        public FloatTextField(FloatTextDefinition definition)
        {
            Definition = definition ?? new FloatTextDefinition();
        }

        public FloatTextDefinition Definition { get; }

        /// <summary>The texts still alive, oldest first.</summary>
        public IReadOnlyList<FloatText> Texts => texts;

        /// <summary>show_float_text(x, y, text): Create's defaults (flashing, 0.008 a step, 1 px a step).</summary>
        public FloatText Show(double x, double y, string text)
        {
            var made = new FloatText(Definition, x, y, text);
            texts.Add(made);
            return made;
        }

        /// <summary>show_float_text_ext(x, y, text, flash, fadespeed, floatspeed).</summary>
        public FloatText ShowExt(double x, double y, string text, bool flash, double fadeSpeed, double floatSpeed)
        {
            var made = Show(x, y, text);
            made.Flash = flash;
            made.FadeSpeed = fadeSpeed;
            made.FloatSpeed = floatSpeed;
            return made;
        }

        /// <summary>show_float_text_color(x, y, text, flash, fadespeed, floatspeed, color, outline).</summary>
        public FloatText ShowColor(double x, double y, string text, bool flash, double fadeSpeed, double floatSpeed, string color, string outline)
        {
            var made = ShowExt(x, y, text, flash, fadeSpeed, floatSpeed);
            made.Color = color;
            made.OutlineColor = outline;
            return made;
        }

        /// <summary><c>show_float_text_ext(x, y, "n", true, 0.01, 1)</c> beside a <c>current_score += n</c>.</summary>
        public FloatText ShowScore(double x, double y, int points) =>
            ShowExt(x, y, points.ToString(CultureInfo.InvariantCulture), Definition.ScoreFlash, Definition.ScoreFadeSpeed, Definition.ScoreFloatSpeed);

        /// <summary>Every text's Step, oldest first; the ones that destroyed themselves go.</summary>
        public void Step()
        {
            int count = texts.Count;
            for (int i = 0; i < count; i++) texts[i].Step();
            texts.RemoveAll(t => t.Gone);
        }

        /// <summary>The room went (its instances with it).</summary>
        public void Clear() => texts.Clear();
    }
}
