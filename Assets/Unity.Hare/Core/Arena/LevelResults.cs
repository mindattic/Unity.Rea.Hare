using System;
using System.Collections.Generic;
using Unity.Hare.Content;

namespace Unity.Hare.Arena
{
    /// <summary>obj_level_results' numbers and text (U.HARE-T22), as its Create, Draw GUI and Enter events have them.</summary>
    [Serializable]
    public class LevelResultsDefinition
    {
        /// <summary>The object's sprite, spr_results (712x712, origin 356,0), drawn by draw_self.</summary>
        public string Sprite = "spr_results";
        /// <summary>Create: <c>width = sprite_get_width(spr_results)</c>.</summary>
        public int Width = 712;
        /// <summary>Create: <c>height = sprite_get_height(spr_results)</c>.</summary>
        public int Height = 712;
        /// <summary>spr_results' xorig.</summary>
        public int OriginX = 356;
        /// <summary>spr_results' yorigin.</summary>
        public int OriginY = 0;
        /// <summary>set_level_complete: <c>results.x = screen_width/2</c>.</summary>
        public int X = 960;
        /// <summary>set_level_complete: <c>results.y = screen_height/4</c>.</summary>
        public int Y = 270;
        /// <summary>Create: <c>image_alpha = 0.7</c> (draw_self's alpha).</summary>
        public float Alpha = 0.7f;
        /// <summary>Create: <c>margin = 25</c>.</summary>
        public int Margin = 25;
        /// <summary>Every line's draw_text_outline size.</summary>
        public int Outline = 1;
        /// <summary>The heading, in fnt_score at <c>y + margin</c>.</summary>
        public string Title = "Level Complete";
        /// <summary>The first row's label, at <c>y + margin*5</c>.</summary>
        public string TimeLabel = "Time";
        /// <summary>The second row's label, at <c>y + margin*6</c>.</summary>
        public string KillsLabel = "Enemies Killed";
        /// <summary>The prompt, at <c>y + height/2 - margin</c>.</summary>
        public string Prompt = "Press [ENTER] to continue";
        /// <summary>The Enter event: <c>audio_play_sound(sfx_boost,1,0)</c>.</summary>
        public string ContinueSound = "sfx_boost";
        /// <summary>The Enter event: <c>effect_create_below(ef_ring, player.x, player.y, 1, c_white)</c>'s size.</summary>
        public int ContinueRingSize = 1;
        /// <summary>That ring's colour, c_white.</summary>
        public string ContinueRingColor = "#FFFFFF";

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Sprite", Sprite);
            ValidationHelpers.Positive(errors, path + ".Width", Width);
            ValidationHelpers.Positive(errors, path + ".Height", Height);
            ValidationHelpers.NonNegative(errors, path + ".Margin", Margin);
            ValidationHelpers.NonNegative(errors, path + ".Outline", Outline);
            if (Alpha < 0f || Alpha > 1f) errors.Add($"{path}.Alpha: must be 0-1 (was {Alpha}).");
            ValidationHelpers.Required(errors, path + ".Title", Title);
            ValidationHelpers.Required(errors, path + ".Prompt", Prompt);
            ValidationHelpers.Required(errors, path + ".ContinueSound", ContinueSound);
            ValidationHelpers.Required(errors, path + ".ContinueRingColor", ContinueRingColor);
        }
    }

    /// <summary>Which way a results line hangs off its x (draw_set_halign).</summary>
    public enum ResultsAlign
    {
        Left,
        Center,
        Right
    }

    /// <summary>One draw_text_outline call of obj_level_results' Draw GUI: where, how aligned, in which font, saying what.</summary>
    public readonly struct ResultsLine
    {
        public readonly float X, Y;
        public readonly ResultsAlign Align;
        /// <summary>fnt_score when true (the heading), fnt_point otherwise.</summary>
        public readonly bool Large;
        public readonly string Text;

        public ResultsLine(float x, float y, ResultsAlign align, bool large, string text)
        {
            X = x;
            Y = y;
            Align = align;
            Large = large;
            Text = text ?? "";
        }

        public override string ToString() => Text;
    }

    /// <summary>
    /// obj_level_results (U.HARE-T22), engine-free: the panel set_level_complete makes at
    /// <c>(screen_width/2, screen_height/4)</c>. Its Draw GUI draws spr_results at 0.7 alpha, then in white with a size-1
    /// black outline: "Level Complete" (fnt_score, centred, <c>y + margin</c>); "Time" and obj_healthbar's strTime
    /// (fnt_point, left at <c>x - width/2 + margin</c> and right at <c>x + width/2 - margin</c>, <c>y + margin*5</c>);
    /// "Enemies Killed" and KILLS the same way at <c>y + margin*6</c>; and "Press [ENTER] to continue" (fnt_point, centred,
    /// <c>y + height/2 - margin</c>). It reads strTime and KILLS every frame, so <see cref="Lines"/> takes them each time.
    /// </summary>
    public sealed class LevelResults
    {
        public LevelResults(LevelResultsDefinition definition)
        {
            Definition = definition ?? new LevelResultsDefinition();
        }

        public LevelResultsDefinition Definition { get; }

        /// <summary>The instance's x (the sprite's origin), GUI pixels.</summary>
        public float X => Definition.X;
        /// <summary>The instance's y.</summary>
        public float Y => Definition.Y;
        /// <summary>The sprite's top left as draw_self puts it.</summary>
        public float SpriteLeft => X - Definition.OriginX;
        public float SpriteTop => Y - Definition.OriginY;

        /// <summary>The Draw GUI's five lines of text, with <paramref name="time"/> (strTime) and <paramref name="kills"/> (KILLS).</summary>
        public ResultsLine[] Lines(string time, int kills)
        {
            var d = Definition;
            float left = X - d.Width / 2f + d.Margin;
            float right = X + d.Width / 2f - d.Margin;
            return new[]
            {
                new ResultsLine(X, Y + d.Margin, ResultsAlign.Center, true, d.Title),
                new ResultsLine(left, Y + d.Margin * 5, ResultsAlign.Left, false, d.TimeLabel),
                new ResultsLine(right, Y + d.Margin * 5, ResultsAlign.Right, false, time),
                new ResultsLine(left, Y + d.Margin * 6, ResultsAlign.Left, false, d.KillsLabel),
                new ResultsLine(right, Y + d.Margin * 6, ResultsAlign.Right, false, kills.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new ResultsLine(X, Y + d.Height / 2f - d.Margin, ResultsAlign.Center, false, d.Prompt),
            };
        }
    }

    /// <summary>
    /// obj_healthbar's clock (U.HARE-T22, for obj_level_results' "Time"): <c>minutes</c>, <c>seconds</c> and strTime, set
    /// to "00:00" by its Create (obj_game's Create makes one per room) and counted on by its one-second alarm while not
    /// paused, the same alarm that adds to time_elapsed_seconds (the session's <c>SecondElapsed</c>). Unlike
    /// time_elapsed_seconds it is never reset by a level, so it counts the whole time spent in the room. Minutes do not
    /// wrap: past 99 they print in full.
    /// </summary>
    public sealed class HudClock
    {
        public int Minutes { get; private set; }
        public int Seconds { get; private set; }

        /// <summary>GML strTime.</summary>
        public string Text { get; private set; } = "00:00";

        /// <summary>The alarm's unpaused branch: <c>seconds += 1; if(seconds == 60) { seconds = 0; minutes += 1; }</c>, then strTime.</summary>
        public void Tick()
        {
            Seconds++;
            if (Seconds == 60)
            {
                Seconds = 0;
                Minutes++;
            }
            Text = Pad(Minutes) + ":" + Pad(Seconds);
        }

        static string Pad(int value) =>
            (value < 10 ? "0" : "") + value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
