using System;
using System.Collections.Generic;
using Unity.Hare.Flow;

namespace Unity.Hare.Story
{
    /// <summary>
    /// room_story, engine-free (U.HARE-T28): obj_story's typewriter (obj_typewriter_story) typing the introduction over
    /// obj_scroll_world, the earth rising from below the room as bg_nightsky scrolls up behind it. A key (any but Esc)
    /// or the earth reaching the bottom of the room sets the campaign (<c>gamemode = gm_campaign; current_scenario = -1;
    /// current_level = 1</c>) and fades obj_fade_story to room_map. obj_story's Esc (<c>room_goto(room_title)</c>) is
    /// the room flow's (the rooms table's EscapeToTitle). There is no global instance: the story room's view owns one.
    /// State changes only through <see cref="Execute"/>; presentation draws the typewriter, earth and background and plays
    /// the events (the typing sound, the session's game mode, the fade) and never writes back.
    ///
    /// The room's instances are created in room order (obj_scroll_world, obj_story with its typewriter, obj_fade_story),
    /// and each step runs their alarms in that order before obj_scroll_world's Step turns the earth.
    /// </summary>
    public sealed class StoryScreen
    {
        readonly List<StoryEvent> events = new List<StoryEvent>();
        PlayerId issuer;
        int scrollAlarm = -1;

        /// <param name="tuning">The story data (the registry's <c>story</c> section).</param>
        /// <param name="text">obj_story's <c>typewriter.text</c> (the dialogue section's story lines, joined).</param>
        /// <param name="roomWidth">room_width, 1920.</param>
        /// <param name="roomHeight">room_height, 1080.</param>
        public StoryScreen(StoryTuning tuning, string text, int roomWidth, int roomHeight)
        {
            Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            RoomWidth = roomWidth;
            RoomHeight = roomHeight;
            Typewriter = new Typewriter(tuning.Typewriter, text ?? "");
            TextDone = new TextDoneMarker(tuning.TextDone ?? new TextDoneDefinition());
        }

        public StoryTuning Tuning { get; }
        public int RoomWidth { get; }
        public int RoomHeight { get; }

        /// <summary>obj_typewriter_story.</summary>
        public Typewriter Typewriter { get; }

        /// <summary>obj_textdone, shown once the introduction is typed out (U.HARE-T87).</summary>
        public TextDoneMarker TextDone { get; }

        /// <summary>obj_scroll_world's x, y (spr_earthball's origin) and image_angle.</summary>
        public double EarthX { get; private set; }
        public double EarthY { get; private set; }
        public double EarthAngle { get; private set; }

        /// <summary>GML: background_y[0], bg_nightsky's offset (it scrolls up with the earth).</summary>
        public int BackgroundY { get; private set; }

        /// <summary>obj_scroll_world's <c>finished</c>: the earth reached the bottom of the room.</summary>
        public bool Finished { get; private set; }

        /// <summary>Whether the room's Create has run.</summary>
        public bool Created { get; private set; }

        /// <summary>Runs <paramref name="command"/> and returns whether it took effect and what it raised.</summary>
        public StoryResult Execute(StoryCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            events.Clear();
            issuer = command.Id;
            bool accepted = command.Apply(this);
            var raised = events.Count == 0 ? Array.Empty<StoryEvent>() : events.ToArray();
            events.Clear();
            return new StoryResult(accepted, raised);
        }

        void Emit(StoryEvent e)
        {
            e.Id = issuer;
            events.Add(e);
        }

        // ---- Rules the commands call ----

        /// <summary>
        /// The room's Create events: obj_scroll_world at <c>(room_width/2, room_height + sprite_get_height(spr_earthball))</c>
        /// with <c>alarm[0] = scroll_delay</c>, obj_story's typewriter (<c>alarm[0] = type_speed</c>) and its text.
        /// </summary>
        internal bool Create()
        {
            if (Created) return false;
            Created = true;
            EarthX = RoomWidth / 2d;
            EarthY = RoomHeight + Tuning.EarthHeight;
            EarthAngle = 0d;
            BackgroundY = 0;
            scrollAlarm = Tuning.ScrollDelaySteps;
            Typewriter.ArmTyping();
            return true;
        }

        /// <summary>One step at room_speed: obj_scroll_world's alarm, the typewriter's alarm, then the earth's turn.</summary>
        internal bool Step()
        {
            if (!Created) return false;

            if (scrollAlarm > 0 && --scrollAlarm == 0)
            {
                if (EarthY > RoomHeight)
                {
                    EarthY -= Tuning.ScrollStep;
                    BackgroundY -= Tuning.ScrollStep;
                }
                else if (!Finished)
                {
                    Finished = true;
                    EarthY = RoomHeight;
                    Leave();
                }
                scrollAlarm = Tuning.ScrollDelaySteps;
            }

            var typed = Typewriter.Step();
            if (typed.Sound != null) Emit(new StoryTypeSounded { Sound = typed.Sound });

            TextDone.Step(Typewriter.PrintComplete);
            EarthAngle = NormalizeAngle(EarthAngle - Tuning.AutoSpeed);
            return true;
        }

        /// <summary>obj_story's key press for any key: <c>if (!keyboard_check(vk_escape))</c>, every press.</summary>
        internal bool PressKey(bool escapeHeld)
        {
            if (!Created || escapeHeld) return false;
            Leave();
            return true;
        }

        /// <summary><c>next_room = room_map; obj_fade_story.mode = "fadein"; obj_fade_story.firstPass = true;</c> and the campaign.</summary>
        void Leave()
        {
            Emit(new StoryCampaignChosen());
            Emit(new StoryFadeRequested { Room = Tuning.ExitRoom });
        }

        /// <summary>normalize_angle: one turn added or taken off, then (below 0) <c>mod 360</c>.</summary>
        public static double NormalizeAngle(double angle)
        {
            if (angle >= 360d) angle -= 360d;
            else if (angle < 0d)
            {
                angle += 360d;
                angle %= 360d;
            }
            return angle;
        }
    }

    /// <summary>What <see cref="StoryScreen.Execute"/> returns: whether the command took effect and what it raised.</summary>
    public readonly struct StoryResult
    {
        public StoryResult(bool accepted, IReadOnlyList<StoryEvent> events)
        {
            Accepted = accepted;
            Events = events;
        }

        public bool Accepted { get; }
        public IReadOnlyList<StoryEvent> Events { get; }
    }
}
