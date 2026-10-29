using System;
using System.Collections.Generic;
using Unity.Hare.Content;
using Unity.Hare.Popups;

namespace Unity.Hare.Story
{
    /// <summary>
    /// One of the original's typewriters as data (U.HARE-T28): obj_typewriter_story (room_story's), obj_conversation's
    /// built-in one, and the plain obj_typewriter. Each reveals its <c>text</c> one character per Alarm 0 firing,
    /// <c>type_speed</c> steps apart. With <see cref="Pauses"/> (obj_typewriter_story and obj_conversation) a <c>^</c> is
    /// never drawn (<c>string_replace_all(output,"^", "")</c>) and holds the next character back <c>pause_delay</c>
    /// (room_speed) steps, every character not in <see cref="SilentChars"/> restarts <see cref="Sound"/>
    /// (<c>audio_stop_sound</c> then <c>audio_play_sound</c>), and the alarm stops once the text is out
    /// (<c>print_complete</c>). Without (obj_typewriter) the text is copied as it is, with no sound, and the alarm runs
    /// for as long as the instance lives, taking <see cref="FadeStep"/> off its alpha each firing once it is told to fade.
    /// </summary>
    [Serializable]
    public class TypewriterDefinition
    {
        /// <summary>The GML object it ports.</summary>
        public string Object = "obj_typewriter_story";
        /// <summary>Where its Draw GUI draws the text: <c>draw_text_outline_alpha(x, y, 1, output, alpha)</c>.</summary>
        public int X = 30;
        public int Y = 60;
        /// <summary>GML: type_speed, the steps between two characters.</summary>
        public int TypeSpeedSteps = 1;
        /// <summary>GML: pause_delay (room_speed), the steps a ^ holds the next character back.</summary>
        public int PauseDelaySteps = 60;
        /// <summary>Whether ^ pauses and is stripped, the sound plays and the alarm stops at the end (see the class).</summary>
        public bool Pauses = true;
        /// <summary>The sound each typed character restarts; "" for none.</summary>
        public string Sound = "sfx_type2";
        /// <summary>The characters that type without <see cref="Sound"/>.</summary>
        public string SilentChars = " #^.,";
        /// <summary>The outline draw_text_outline* draws around the text.</summary>
        public int Outline = 1;
        /// <summary>GML: <c>alpha -= 0.01</c>, a firing, while <c>fadeout</c> (obj_typewriter only).</summary>
        public double FadeStep = 0.01;

        /// <summary>room_story's obj_typewriter_story, made by obj_story at (30, 60) in fnt_medium.</summary>
        public static TypewriterDefinition Story() => new TypewriterDefinition();

        /// <summary>obj_conversation's typing: <c>type_speed = 2</c>, <c>pause_delay = room_speed</c>, sfx_type, at (260, 24) from the box in fnt_score.</summary>
        public static TypewriterDefinition Conversation() => new TypewriterDefinition
        {
            Object = "obj_conversation", X = 260, Y = 24, TypeSpeedSteps = 2, PauseDelaySteps = 60, Pauses = true,
            Sound = "sfx_type", SilentChars = " #^", Outline = 2,
        };

        /// <summary>obj_typewriter: <c>type_speed = 2</c>, no pauses, no sound, fnt_point (only commented-out code makes one).</summary>
        public static TypewriterDefinition Plain() => new TypewriterDefinition
        {
            Object = "obj_typewriter", X = 20, Y = 630, TypeSpeedSteps = 2, PauseDelaySteps = 0, Pauses = false,
            Sound = "", SilentChars = "", Outline = 1,
        };

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Object", Object);
            ValidationHelpers.Positive(errors, path + ".TypeSpeedSteps", TypeSpeedSteps);
            if (Pauses) ValidationHelpers.Positive(errors, path + ".PauseDelaySteps", PauseDelaySteps);
            else ValidationHelpers.NonNegative(errors, path + ".PauseDelaySteps", PauseDelaySteps);
            ValidationHelpers.NonNegative(errors, path + ".Outline", Outline);
            if (!(FadeStep > 0d) || FadeStep > 1d) errors.Add($"{path}.FadeStep: must be greater than 0 and at most 1 (was {FadeStep}).");
            if (Sound == null) errors.Add(path + ".Sound: must not be null (\"\" for none).");
            if (SilentChars == null) errors.Add(path + ".SilentChars: must not be null.");
        }
    }

    /// <summary>What one Alarm 0 firing of a <see cref="Typewriter"/> did.</summary>
    public readonly struct TypeStep
    {
        public TypeStep(bool typed, string sound, bool paused)
        {
            Typed = typed;
            Sound = sound;
            Paused = paused;
        }

        /// <summary>A character came out.</summary>
        public bool Typed { get; }
        /// <summary>The sound to restart (audio_stop_sound, audio_play_sound), or null.</summary>
        public string Sound { get; }
        /// <summary>The character was a ^: the next one waits pause_delay steps.</summary>
        public bool Paused { get; }
    }

    /// <summary>
    /// A typewriter running, engine-free (U.HARE-T28): obj_typewriter_story, obj_conversation's typing or obj_typewriter,
    /// as its <see cref="TypewriterDefinition"/> says. GameMaker's alarm is kept exactly: set to n, it counts down once a
    /// step and fires on the step it reaches 0, so a character every <c>type_speed</c> steps.
    /// <see cref="Step"/> is the instance's whole step (its alarm); obj_conversation, whose alarm only types while it is
    /// idle, drives <see cref="TickAlarm"/> and <see cref="Fire"/> itself.
    /// </summary>
    public sealed class Typewriter
    {
        int alarm = -1;

        public Typewriter(TypewriterDefinition definition, string text = "")
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Text = text ?? "";
        }

        public TypewriterDefinition Definition { get; }

        /// <summary>GML: text, with # for a new line and (when it pauses) ^ for a pause.</summary>
        public string Text { get; set; }
        /// <summary>GML: position, the characters of <see cref="Text"/> out so far.</summary>
        public int Position { get; private set; }
        /// <summary>GML: output, what is drawn (^ removed when it pauses).</summary>
        public string Output { get; private set; } = "";
        /// <summary>GML: raw_output, the copied text before the ^ are removed.</summary>
        public string RawOutput { get; private set; } = "";
        /// <summary>GML: next_char, the character the last firing copied ("" before the first).</summary>
        public string NextChar { get; private set; } = "";
        /// <summary>GML: print_complete.</summary>
        public bool PrintComplete { get; private set; }
        /// <summary>GML: alpha (obj_typewriter's fade).</summary>
        public double Alpha { get; private set; } = 1d;
        /// <summary>GML: fadeout.</summary>
        public bool FadingOut { get; private set; }
        /// <summary>obj_typewriter faded away: <c>instance_destroy()</c>.</summary>
        public bool Destroyed { get; private set; }
        /// <summary>GML: alarm[0], -1 when not set.</summary>
        public int Alarm => alarm;

        /// <summary><see cref="Output"/> as drawn: GameMaker's # is a line break.</summary>
        public string DrawText => GmlString.Lines(Output);

        /// <summary>The alpha draw_set_alpha makes of <see cref="Alpha"/>.</summary>
        public float DrawAlpha => (float)Math.Max(0d, Math.Min(1d, Alpha));

        /// <summary>GML: <c>alarm[0] = steps</c>.</summary>
        public void Arm(int steps) => alarm = steps;

        /// <summary>GML: <c>alarm[0] = type_speed</c>, as Create and obj_conversation's idle do.</summary>
        public void ArmTyping() => Arm(Definition.TypeSpeedSteps);

        /// <summary>obj_typewriter's <c>fadeout = true</c>.</summary>
        public void FadeOut() => FadingOut = true;

        /// <summary>obj_conversation's <c>output = ""; position = 0; print_complete = false;</c></summary>
        public void Reset()
        {
            Output = "";
            Position = 0;
            PrintComplete = false;
        }

        /// <summary>The alarm's count down for a step: true on the step it reaches 0 (its event fires).</summary>
        public bool TickAlarm()
        {
            if (alarm <= 0) return false;
            alarm--;
            if (alarm > 0) return false;
            alarm = -1;
            return true;
        }

        /// <summary>One step of a typewriter on its own: its alarm, and the Alarm 0 event if it fires.</summary>
        public TypeStep Step() => !Destroyed && TickAlarm() ? Fire() : default;

        /// <summary>The Alarm 0 event.</summary>
        public TypeStep Fire()
        {
            if (Destroyed) return default;
            return Definition.Pauses ? FirePausing() : FirePlain();
        }

        TypeStep FirePausing()
        {
            if (PrintComplete) return default; // Text writing complete; the alarm is not set again
            Position++;
            NextChar = CharAt(Text, Position);
            RawOutput = Copy(Text, Position);
            Output = RawOutput.Replace("^", "");
            string sound = null;
            if (NextChar.Length > 0 && Definition.SilentChars.IndexOf(NextChar[0]) < 0 && !string.IsNullOrEmpty(Definition.Sound))
                sound = Definition.Sound;
            bool paused = NextChar == "^";
            Arm(paused ? Definition.PauseDelaySteps : Definition.TypeSpeedSteps);
            if (RawOutput == Text) PrintComplete = true;
            return new TypeStep(true, sound, paused);
        }

        TypeStep FirePlain()
        {
            bool typed = false;
            if (Output != Text)
            {
                Position++;
                NextChar = CharAt(Text, Position);
                Output = Copy(Text, Position);
                RawOutput = Output;
                typed = true;
            }
            if (FadingOut)
            {
                Alpha -= Definition.FadeStep;
                if (Alpha <= 0d)
                {
                    Destroyed = true;
                    return new TypeStep(typed, null, false);
                }
            }
            Arm(Definition.TypeSpeedSteps);
            return new TypeStep(typed, null, false);
        }

        /// <summary>GML string_char_at(text, position): 1-based, "" past either end.</summary>
        static string CharAt(string text, int position) =>
            position >= 1 && position <= text.Length ? text[position - 1].ToString() : "";

        /// <summary>GML string_copy(text, 0, position): the first <paramref name="count"/> characters (an index below 1 reads as 1).</summary>
        static string Copy(string text, int count) => text.Substring(0, Math.Max(0, Math.Min(count, text.Length)));
    }
}
