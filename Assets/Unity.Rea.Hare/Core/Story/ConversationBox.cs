using System;
using System.Collections.Generic;
using Unity.Hare.Content;

namespace Unity.Hare.Story
{
    /// <summary>obj_conversation's mode.</summary>
    public enum ConversationMode
    {
        Off,
        SlideIn,
        Idle,
        SlideOut,
    }

    /// <summary>
    /// obj_conversation as data (U.HARE-T28): the comm box that rises from the bottom of the GUI layer with the speaker's
    /// portrait in its frame and types the speaker's line (<see cref="Typewriter"/>). The defaults are the original's, read from HARE.gmx: spr_conversation_box
    /// (1920x256), spr_comm_frame_back, spr_portrait_general_zee and spr_comm_frame (195x195) at
    /// <c>frame_margin_left = 30</c>, <c>frame_margin_top = 30</c>, the name centred under the frame in fnt_point and the
    /// text at <c>text_margin_left = 260</c>, <c>text_margin_top = 24</c> in fnt_score, both with a size-2 outline.
    /// </summary>
    [Serializable]
    public class ConversationDefinition
    {
        public string BoxSprite = "spr_conversation_box";
        /// <summary>spr_conversation_box's size: <c>width</c>, <c>height</c>.</summary>
        public int Width = 1920;
        public int Height = 256;
        public string FrameBackSprite = "spr_comm_frame_back";
        public string PortraitSprite = "spr_portrait_general_zee";
        public string FrameSprite = "spr_comm_frame";
        /// <summary>spr_comm_frame's size: <c>frame_width</c>, <c>frame_height</c>.</summary>
        public int FrameWidth = 195;
        public int FrameHeight = 195;
        public int FrameMarginLeft = 30;
        public int FrameMarginTop = 30;
        /// <summary>The speaker's name's outline (fnt_point, fa_center, under the frame).</summary>
        public int NameOutline = 2;
        /// <summary>GML: <c>y -= 1 + ((y - endY) * 0.1)</c> sliding in.</summary>
        public double SlideInMin = 1d;
        /// <summary>GML: <c>y += 4 + ((startY - y) * 0.1)</c> sliding out.</summary>
        public double SlideOutMin = 4d;
        public double SlideFactor = 0.1d;
        /// <summary>The first pass of a slide, and next_line, play it.</summary>
        public string SlideSound = "sfx_flick";
        /// <summary>next_line's <c>if(obj_conversation.frame_name == "Admiral Zee") audio_play_sound(sfx_woof...) else audio_play_sound(sfx_flick...)</c>.</summary>
        public string BarkSpeaker = "Admiral Zee";
        public string BarkSound = "sfx_woof";
        /// <summary>The line's typing.</summary>
        public TypewriterDefinition Typewriter = TypewriterDefinition.Conversation();
        /// <summary>The dialogue the map's comm box speaks (the dialogue section's lines under this id).</summary>
        public string MapScript = "conversation.map";

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".BoxSprite", BoxSprite);
            ValidationHelpers.Positive(errors, path + ".Width", Width);
            ValidationHelpers.Positive(errors, path + ".Height", Height);
            ValidationHelpers.Required(errors, path + ".FrameBackSprite", FrameBackSprite);
            ValidationHelpers.Required(errors, path + ".PortraitSprite", PortraitSprite);
            ValidationHelpers.Required(errors, path + ".FrameSprite", FrameSprite);
            ValidationHelpers.Positive(errors, path + ".FrameWidth", FrameWidth);
            ValidationHelpers.Positive(errors, path + ".FrameHeight", FrameHeight);
            ValidationHelpers.NonNegative(errors, path + ".NameOutline", NameOutline);
            if (!(SlideInMin > 0d)) errors.Add($"{path}.SlideInMin: must be greater than 0 (was {SlideInMin}).");
            if (!(SlideOutMin > 0d)) errors.Add($"{path}.SlideOutMin: must be greater than 0 (was {SlideOutMin}).");
            if (!(SlideFactor >= 0d) || SlideFactor >= 1d) errors.Add($"{path}.SlideFactor: must be at least 0 and below 1 (was {SlideFactor}).");
            ValidationHelpers.Required(errors, path + ".SlideSound", SlideSound);
            ValidationHelpers.Required(errors, path + ".BarkSound", BarkSound);
            if (Typewriter == null) errors.Add(path + ".Typewriter: must not be null.");
            else Typewriter.Validate(errors, path + ".Typewriter");
            ValidationHelpers.Required(errors, path + ".MapScript", MapScript);
        }
    }

    /// <summary>
    /// obj_conversation, engine-free, in GUI pixels (its Draw GUI draws at x, y; U.HARE-T28). Create puts it just below
    /// the view (<c>startY = view_hview</c>, <c>endY = view_hview - height</c>, <c>x = 0</c>), <c>mode = "off"</c>.
    /// <see cref="SlideIn"/> (obj_destination reaching a new mission) and <see cref="SlideOut"/> (obj_map's A and D) set
    /// only the mode. Step: sliding in, the first pass plays sfx_flick and resets the text, then
    /// <c>y -= 1 + ((y - endY) * 0.1)</c> until y is not above endY, which snaps it there and idles it; the idle's first
    /// pass arms the typing (<c>alarm[0] = type_speed</c>), and Alarm 0 types only while it is idle. Sliding out, the first
    /// pass plays sfx_flick, then <c>y += 4 + ((startY - y) * 0.1)</c> until back at startY, where it goes off and resets
    /// the text. A key press (vk_anykey) or the global left press, once the line is out and it is idle, run next_line
    /// (<see cref="NextLine"/>). GameMaker runs an instance's alarms before its Step, and so does <see cref="Step"/>.
    ///
    /// obj_textdone (spr_textdone at 8x, bottom right of the view) is the marker <see cref="TextDone"/> shows while the
    /// finished line waits for a key; next_line destroys it (U.HARE-T87, BIBLE V41: the original never creates it, an
    /// intended feature completed).
    /// </summary>
    public sealed class ConversationBox
    {
        bool firstPass = true;

        public ConversationBox(ConversationDefinition definition, string speaker, string text, int viewWidth, int viewHeight, TextDoneDefinition textDone = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Speaker = speaker ?? "";
            Typewriter = new Typewriter(definition.Typewriter, text ?? "");
            TextDone = new TextDoneMarker(textDone ?? new TextDoneDefinition());
            ViewWidth = viewWidth;
            ViewHeight = viewHeight;
            StartY = viewHeight;
            EndY = viewHeight - definition.Height;
            Y = StartY;
        }

        public ConversationDefinition Definition { get; }

        /// <summary>GML: frame_name, the name under the portrait.</summary>
        public string Speaker { get; }

        /// <summary>The line's typing (GML: text, output, position, print_complete, alarm[0]).</summary>
        public Typewriter Typewriter { get; }

        /// <summary>obj_textdone, shown while the line is typed out and the box idles.</summary>
        public TextDoneMarker TextDone { get; }

        public int ViewWidth { get; }
        public int ViewHeight { get; }
        public double X => 0d;
        public double Y { get; private set; }
        public double StartY { get; }
        public double EndY { get; }
        public ConversationMode Mode { get; private set; } = ConversationMode.Off;

        /// <summary>GML: print_complete.</summary>
        public bool PrintComplete => Typewriter.PrintComplete;

        /// <summary>The frame (back, portrait, frame) is drawn here.</summary>
        public double FrameX => X + Definition.FrameMarginLeft;
        public double FrameY => Y + Definition.FrameMarginTop;
        /// <summary>The name is drawn centred on this point (fa_center, top aligned).</summary>
        public double NameX => FrameX + Definition.FrameWidth / 2d;
        public double NameY => FrameY + Definition.FrameHeight;
        /// <summary>The line is drawn here (fa_left).</summary>
        public double TextX => X + Definition.Typewriter.X;
        public double TextY => Y + Definition.Typewriter.Y;

        /// <summary>obj_destination's <c>current_conversation.mode = "slidein"</c>.</summary>
        public void SlideIn() => Mode = ConversationMode.SlideIn;

        /// <summary>obj_map's <c>current_conversation.mode = "slideout"</c>.</summary>
        public void SlideOut() => Mode = ConversationMode.SlideOut;

        /// <summary>
        /// One step: Alarm 0 (types while idle), then the Step event. Returns the sounds it played, in order (none, or
        /// sfx_flick on a slide's first pass, or a typed character's sfx_type).
        /// </summary>
        public IReadOnlyList<string> Step()
        {
            List<string> sounds = null;
            if (Typewriter.TickAlarm() && Mode == ConversationMode.Idle)
            {
                var typed = Typewriter.Fire();
                if (typed.Sound != null) (sounds ??= new List<string>()).Add(typed.Sound);
            }

            var d = Definition;
            switch (Mode)
            {
                case ConversationMode.SlideIn:
                    if (firstPass)
                    {
                        firstPass = false;
                        (sounds ??= new List<string>()).Add(d.SlideSound);
                        Typewriter.Reset();
                    }
                    if (Y > EndY) Y -= d.SlideInMin + (Y - EndY) * d.SlideFactor;
                    else
                    {
                        Y = EndY;
                        Mode = ConversationMode.Idle;
                        firstPass = true;
                    }
                    break;
                case ConversationMode.Idle:
                    if (firstPass)
                    {
                        firstPass = false;
                        Typewriter.ArmTyping();
                    }
                    break;
                case ConversationMode.SlideOut:
                    if (firstPass)
                    {
                        firstPass = false;
                        (sounds ??= new List<string>()).Add(d.SlideSound);
                    }
                    if (Y < StartY) Y += d.SlideOutMin + (StartY - Y) * d.SlideFactor;
                    else
                    {
                        Y = StartY;
                        Mode = ConversationMode.Off;
                        Typewriter.Reset();
                        firstPass = true;
                    }
                    break;
            }

            TextDone.Step(PrintComplete && Mode == ConversationMode.Idle);
            return (IReadOnlyList<string>)sounds ?? Array.Empty<string>();
        }

        /// <summary>
        /// Its key press (vk_anykey) and global left pressed events: <c>if(print_complete &amp;&amp; mode == "idle")
        /// next_line();</c>, which slides it out (<c>mode = "slideout"; firstPass = true</c>) and
        /// plays sfx_flick, then sfx_woof for Admiral Zee (sfx_flick for anyone else). Returns those sounds, or an empty
        /// list when it was refused.
        /// </summary>
        public IReadOnlyList<string> NextLine()
        {
            if (!PrintComplete || Mode != ConversationMode.Idle) return Array.Empty<string>();
            Mode = ConversationMode.SlideOut;
            firstPass = true;
            TextDone.Destroy();
            var d = Definition;
            return new[] { d.SlideSound, Speaker == d.BarkSpeaker ? d.BarkSound : d.SlideSound };
        }
    }
}
