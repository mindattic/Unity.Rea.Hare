using System;
using System.Collections.Generic;
using Unity.Hare.Content;

namespace Unity.Hare.Flow
{
    /// <summary>
    /// One room as its .room.gmx and controller object define it: its size and room_speed, which <c>obj_fade_*</c>
    /// it has, and which of the controller's key events it answers (Esc's <c>room_goto(room_title)</c>, F1's
    /// <c>window_set_fullscreen(!window_get_fullscreen())</c>).
    /// </summary>
    [Serializable]
    public class RoomDefinition
    {
        public RoomId Room;
        /// <summary>GML: room_width, in pixels.</summary>
        public int Width;
        /// <summary>GML: room_height, in pixels.</summary>
        public int Height;
        /// <summary>GML: room_speed, steps a second; the room's fade steps at this rate.</summary>
        public int StepsPerSecond;
        /// <summary>The room's obj_fade_* and what it does at opaque; <see cref="FadeKind.None"/> when it has none.</summary>
        public FadeKind Fade;
        /// <summary>The room's controller has an Esc key-press event that runs <c>room_goto(room_title)</c>.</summary>
        public bool EscapeToTitle;
        /// <summary>The room's controller has an F1 key-press event that toggles fullscreen.</summary>
        public bool FullscreenKey;

        public RoomDefinition() { }

        public RoomDefinition(RoomId room, int width, int height, int stepsPerSecond, FadeKind fade, bool escapeToTitle, bool fullscreenKey)
        {
            Room = room;
            Width = width;
            Height = height;
            StepsPerSecond = stepsPerSecond;
            Fade = fade;
            EscapeToTitle = escapeToTitle;
            FullscreenKey = fullscreenKey;
        }

        internal void Validate(List<string> errors, string path)
        {
            if (!Enum.IsDefined(typeof(RoomId), Room)) errors.Add($"{path}.Room: not a room (was {(int)Room}).");
            ValidationHelpers.Positive(errors, path + ".Width", Width);
            ValidationHelpers.Positive(errors, path + ".Height", Height);
            ValidationHelpers.Positive(errors, path + ".StepsPerSecond", StepsPerSecond);
            if (!Enum.IsDefined(typeof(FadeKind), Fade)) errors.Add($"{path}.Fade: not a fade kind (was {(int)Fade}).");
        }
    }

    /// <summary>
    /// The room flow as data (the content registry's <c>rooms</c> section, AGENTS.md rule 4): every room, the fade's
    /// speed, the room the game starts in, the room Esc goes to and <c>next_room</c>'s starting value. The defaults
    /// are the original's, read from the .room.gmx files, the obj_fade_* Create events, the room controllers' key
    /// events (obj_title and obj_game answer F1; obj_map, obj_story, obj_intro and obj_game answer Esc) and
    /// globals.gml.
    /// </summary>
    [Serializable]
    public class RoomTable
    {
        public RoomDefinition[] Rooms =
        {
            // obj_startup: no fade; any key or click goes to the title (U.HARE-T51).
            new RoomDefinition(RoomId.Startup, 1920, 1080, 60, FadeKind.None, escapeToTitle: false, fullscreenKey: false),
            // obj_title creates obj_fade_title and answers F1; it has no Esc event.
            new RoomDefinition(RoomId.Title, 1920, 1080, 60, FadeKind.NextRoom, escapeToTitle: false, fullscreenKey: true),
            // obj_intro: its obj_fade is commented out; Esc goes to the title.
            new RoomDefinition(RoomId.Intro, 1920, 1080, 60, FadeKind.None, escapeToTitle: true, fullscreenKey: false),
            // obj_game creates obj_fade_game and answers Esc and F1 in all three play rooms.
            new RoomDefinition(RoomId.GameScreen, 3840, 2160, 120, FadeKind.NextLevel, escapeToTitle: true, fullscreenKey: true),
            new RoomDefinition(RoomId.SideScroll, 12000, 1080, 120, FadeKind.NextLevel, escapeToTitle: true, fullscreenKey: true),
            new RoomDefinition(RoomId.Shaft, 1920, 12000, 120, FadeKind.NextLevel, escapeToTitle: true, fullscreenKey: true),
            // obj_map creates obj_fade_map and answers Esc.
            new RoomDefinition(RoomId.Map, 1920, 1080, 60, FadeKind.NextRoom, escapeToTitle: true, fullscreenKey: false),
            // room_story places obj_fade_story; obj_story answers Esc.
            new RoomDefinition(RoomId.Story, 1920, 1080, 60, FadeKind.NextRoom, escapeToTitle: true, fullscreenKey: false),
            // obj_upgrade creates obj_fade_upgrade; it has no key events.
            new RoomDefinition(RoomId.Upgrade, 1920, 1080, 60, FadeKind.NextRoom, escapeToTitle: false, fullscreenKey: false),
        };

        /// <summary>obj_fade_*'s Create: <c>fadespeed = 0.03</c>, added to or taken from image_alpha each step.</summary>
        public double FadeSpeed = 0.03;
        /// <summary>The first room in HARE.project.gmx's room order, where the game starts.</summary>
        public RoomId FirstRoom = RoomId.Startup;
        /// <summary>Where every Esc event goes: <c>room_goto(room_title)</c>.</summary>
        public RoomId EscapeRoom = RoomId.Title;
        /// <summary>globals.gml: <c>next_room = room_gamescreen</c>.</summary>
        public RoomId StartingNextRoom = RoomId.GameScreen;
        /// <summary>
        /// The rebuild's catch-up limit: a frame runs at most this many room steps. GameMaker never skipped a step (a
        /// slow frame slowed the game down); this keeps a long frame (a scene load) from jumping a fade.
        /// </summary>
        public int MaxStepsPerFrame = 4;

        /// <summary>The definition of <paramref name="room"/>.</summary>
        public RoomDefinition Get(RoomId room)
        {
            foreach (var definition in Rooms)
                if (definition != null && definition.Room == room) return definition;
            throw new KeyNotFoundException($"The room table has no {room}.");
        }

        internal void Validate(List<string> errors, string path)
        {
            var seen = new HashSet<RoomId>();
            for (int i = 0; i < Rooms.Length; i++)
            {
                var room = Rooms[i];
                string entryPath = $"{path}.Rooms[{i}]";
                if (room == null) { errors.Add($"{entryPath}: must not be null."); continue; }
                room.Validate(errors, entryPath);
                if (!seen.Add(room.Room)) errors.Add($"{entryPath}.Room: {room.Room} is listed more than once.");
            }
            foreach (var room in RoomList.All)
                if (!seen.Contains(room)) errors.Add($"{path}.Rooms: {room} ({RoomList.GmlName(room)}) is missing.");

            if (!(FadeSpeed > 0d) || FadeSpeed > 1d) errors.Add($"{path}.FadeSpeed: must be greater than 0 and at most 1 (was {FadeSpeed}).");
            ValidationHelpers.Positive(errors, path + ".MaxStepsPerFrame", MaxStepsPerFrame);
            if (!Enum.IsDefined(typeof(RoomId), FirstRoom)) errors.Add($"{path}.FirstRoom: not a room (was {(int)FirstRoom}).");
            if (!Enum.IsDefined(typeof(RoomId), EscapeRoom)) errors.Add($"{path}.EscapeRoom: not a room (was {(int)EscapeRoom}).");
            if (!Enum.IsDefined(typeof(RoomId), StartingNextRoom)) errors.Add($"{path}.StartingNextRoom: not a room (was {(int)StartingNextRoom}).");
        }
    }
}
