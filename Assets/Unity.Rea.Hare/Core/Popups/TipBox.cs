namespace Unity.Hare.Popups
{
    /// <summary>obj_tip's mode.</summary>
    public enum TipMode
    {
        SlideIn,
        Idle,
        SlideOut,
        Off
    }

    /// <summary>
    /// obj_tip (U.HARE-T27), engine-free, in GUI pixels (its Draw GUI draws at x, y). Create: <c>alpha = 0.7</c>, the
    /// box's size from spr_tip_box, <c>x = view_wview; y = view_hview - height - 30</c>, <c>startX = x</c>,
    /// <c>endX = view_wview - width - 30</c>, <c>margin = 20</c>, <c>mode = "slidein"; firstPass = true</c>. Step:
    /// sliding in, its first pass plays sfx_flick, then <c>x -= 1 + (x - endX) * 0.1</c> until x is not above endX, which
    /// snaps it there, idles it and sets firstPass again (the idle's first pass clears it); sliding out (nothing in the
    /// original sets "slideout", but the state is there) <c>x += 2 + (startX - x) * 0.1</c> until back at startX, where it
    /// destroys itself; a slide out after an idle plays no sfx_flick, as firstPass is spent. Any key press (vk_anykey) or a
    /// left press on the box destroys it at once. Its Draw GUI draws spr_tip_box at 0.7 alpha, then the text at alpha 1,
    /// fa_left, fnt_point, with a size-1 outline at <c>(x + margin, y + margin)</c>.
    /// </summary>
    public sealed class TipBox
    {
        bool firstPass = true;

        public TipBox(TipDefinition definition, string text, int viewWidth, int viewHeight)
        {
            Definition = definition ?? new TipDefinition();
            Text = text ?? "";
            X = viewWidth;
            Y = viewHeight - Definition.Height - Definition.BottomMargin;
            StartX = X;
            EndX = viewWidth - Definition.Width - Definition.RightMargin;
        }

        public TipDefinition Definition { get; }

        /// <summary>GML text, # marking a new line.</summary>
        public string Text { get; set; }

        /// <summary><see cref="Text"/> as drawn: GameMaker's # is a line break.</summary>
        public string DrawText => GmlString.Lines(Text);

        public double X { get; private set; }
        public double Y { get; }
        public double StartX { get; }
        public double EndX { get; }
        public TipMode Mode { get; private set; } = TipMode.SlideIn;
        public bool Gone { get; private set; }

        /// <summary>Where the text is drawn.</summary>
        public double TextX => X + Definition.Margin;
        public double TextY => Y + Definition.Margin;

        /// <summary>One Step; <paramref name="playSound"/> says sfx_flick plays. False once it has destroyed itself.</summary>
        public bool Step(out bool playSound)
        {
            playSound = false;
            if (Gone) return false;
            var d = Definition;
            switch (Mode)
            {
                case TipMode.SlideIn:
                    if (firstPass)
                    {
                        firstPass = false;
                        playSound = true;
                    }
                    if (X > EndX) X -= d.SlideInMin + (X - EndX) * d.SlideFactor;
                    else
                    {
                        X = EndX;
                        Mode = TipMode.Idle;
                        firstPass = true;
                    }
                    break;
                case TipMode.Idle:
                    if (firstPass) firstPass = false;
                    break;
                case TipMode.SlideOut:
                    if (firstPass)
                    {
                        firstPass = false;
                        playSound = true;
                    }
                    if (X < StartX) X += d.SlideOutMin + (StartX - X) * d.SlideFactor;
                    else
                    {
                        X = StartX;
                        Mode = TipMode.Off;
                        Gone = true;
                        return false;
                    }
                    break;
            }
            return true;
        }

        /// <summary><c>mode = "slideout"</c>.</summary>
        public void SlideOut()
        {
            if (!Gone) Mode = TipMode.SlideOut;
        }

        /// <summary>Its key press (vk_anykey) and left pressed events: <c>instance_destroy()</c>.</summary>
        public void Dismiss()
        {
            Gone = true;
            Mode = TipMode.Off;
        }

        /// <summary>Whether GUI point (<paramref name="x"/>, <paramref name="y"/>) is on the box (spr_tip_box's full rectangle mask, origin 0,0).</summary>
        public bool Contains(double x, double y) =>
            x >= X && x < X + Definition.Width && y >= Y && y < Y + Definition.Height;
    }

    /// <summary>GameMaker string handling the popups need.</summary>
    public static class GmlString
    {
        /// <summary>draw_text's line breaks: GameMaker 8.1 and Studio 1 draw # as a new line (\# as a literal #).</summary>
        public static string Lines(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var builder = new System.Text.StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\\' && i + 1 < text.Length && text[i + 1] == '#')
                {
                    builder.Append('#');
                    i++;
                }
                else builder.Append(c == '#' ? '\n' : c);
            }
            return builder.ToString();
        }
    }
}
