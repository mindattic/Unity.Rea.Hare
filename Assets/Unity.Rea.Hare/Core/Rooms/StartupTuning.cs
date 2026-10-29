using System;
using System.Collections.Generic;
using Unity.Hare.Content;
using Unity.Hare.Flow;

namespace Unity.Hare.Rooms
{
    /// <summary>
    /// One of the startup room's backgrounds, as startup.room.gmx lists it: the GML background, its starting
    /// <c>hspeed</c>/<c>vspeed</c> (pixels a step), and whether obj_startup's alarms re-roll its speeds. Every one is
    /// placed at (0, 0) and tiled both ways.
    /// </summary>
    [Serializable]
    public class StartupBackground
    {
        /// <summary>The GML background's name (bg_circuitboard3, ...).</summary>
        public string Name;
        /// <summary>GML: background_hspeed[i] as the room starts it, pixels a step.</summary>
        public int HSpeed;
        /// <summary>GML: background_vspeed[i] as the room starts it, pixels a step.</summary>
        public int VSpeed;
        /// <summary>obj_startup has an alarm for this background (alarm[i] for background i) that re-rolls its speeds.</summary>
        public bool Drifts;

        public StartupBackground() { }

        public StartupBackground(string name, int hspeed, int vspeed, bool drifts)
        {
            Name = name;
            HSpeed = hspeed;
            VSpeed = vspeed;
            Drifts = drifts;
        }

        internal void Validate(List<string> errors, string path) => ValidationHelpers.Required(errors, path + ".Name", Name);
    }

    /// <summary>
    /// The startup room as data (the content registry's <c>startup</c> section, AGENTS.md rule 4): startup.room.gmx's
    /// backgrounds and obj_startup's Create, Alarm 0-2, Draw, any-key and mouse events. The defaults are the original's.
    /// </summary>
    [Serializable]
    public class StartupTuning
    {
        /// <summary>
        /// startup.room.gmx's backgrounds in its order, the first drawn at the back: the three circuit boards (alarm[0],
        /// [1] and [2] drift backgrounds 0, 1 and 2), then the room's still bg_semiopaque80 veil and the rabbit schematic.
        /// </summary>
        public StartupBackground[] Backgrounds =
        {
            new StartupBackground("bg_circuitboard3", 1, -1, drifts: true),
            new StartupBackground("bg_circuitboard2", 1, 1, drifts: true),
            new StartupBackground("bg_circuitboard", -2, 1, drifts: true),
            new StartupBackground("bg_semiopaque80", 0, 0, drifts: false),
            new StartupBackground("bg_rabbit_schematic_transparent", 0, 0, drifts: false),
        };

        /// <summary>GML: shift_min, the fewest seconds (times room_speed) before a drifting background's next re-roll.</summary>
        public int ShiftMinSeconds = 1;
        /// <summary>GML: shift_max, the most seconds (times room_speed) before a drifting background's next re-roll.</summary>
        public int ShiftMaxSeconds = 12;
        /// <summary>The alarms' <c>irandom_range(-2, 2)</c>: the slowest re-rolled speed, pixels a step.</summary>
        public int DriftSpeedMin = -2;
        /// <summary>The alarms' <c>irandom_range(-2, 2)</c>: the fastest re-rolled speed, pixels a step.</summary>
        public int DriftSpeedMax = 2;

        /// <summary>GML: strText. GameMaker's <c>#</c> line break is a newline here.</summary>
        public string Note =
            "Note: If application is running slowly and you have a wireless Xbox 360 controller plugged in, try turning the controller on.\n" +
            "This is a known issue with the development software.";
        /// <summary>The Draw event's <c>room_height - 60</c>: the note's top, this many pixels above the room's bottom.</summary>
        public int NoteBottomMargin = 60;
        /// <summary>draw_text_outline_color's size argument: the outline reaches this many pixels around the text.</summary>
        public int NoteOutline = 2;
        /// <summary>The note's text colour, c_red, as #RRGGBB.</summary>
        public string NoteColor = "#FF0000";
        /// <summary>The note's outline colour, c_black, as #RRGGBB.</summary>
        public string NoteOutlineColor = "#000000";

        /// <summary>The Create event's <c>window_set_fullscreen(true)</c>.</summary>
        public bool FullscreenOnEntry = true;
        /// <summary>Where any key and the left mouse button go: <c>room_goto(room_title)</c>.</summary>
        public RoomId ContinueRoom = RoomId.Title;

        internal void Validate(List<string> errors, string path)
        {
            if (Backgrounds == null) errors.Add(path + ".Backgrounds: must not be null.");
            else
            {
                for (int i = 0; i < Backgrounds.Length; i++)
                {
                    if (Backgrounds[i] == null) errors.Add($"{path}.Backgrounds[{i}]: must not be null.");
                    else Backgrounds[i].Validate(errors, $"{path}.Backgrounds[{i}]");
                }
            }
            ValidationHelpers.Positive(errors, path + ".ShiftMinSeconds", ShiftMinSeconds);
            if (ShiftMaxSeconds < ShiftMinSeconds) errors.Add($"{path}.ShiftMaxSeconds: must not be less than ShiftMinSeconds (was {ShiftMaxSeconds}).");
            if (DriftSpeedMax < DriftSpeedMin) errors.Add($"{path}.DriftSpeedMax: must not be less than DriftSpeedMin (was {DriftSpeedMax}).");
            ValidationHelpers.Required(errors, path + ".Note", Note);
            ValidationHelpers.NonNegative(errors, path + ".NoteBottomMargin", NoteBottomMargin);
            ValidationHelpers.NonNegative(errors, path + ".NoteOutline", NoteOutline);
            if (!IsHexColor(NoteColor)) errors.Add($"{path}.NoteColor: must be #RRGGBB (was {NoteColor ?? "null"}).");
            if (!IsHexColor(NoteOutlineColor)) errors.Add($"{path}.NoteOutlineColor: must be #RRGGBB (was {NoteOutlineColor ?? "null"}).");
            if (!Enum.IsDefined(typeof(RoomId), ContinueRoom)) errors.Add($"{path}.ContinueRoom: not a room (was {(int)ContinueRoom}).");
        }

        /// <summary>Whether <paramref name="value"/> is a #RRGGBB colour.</summary>
        public static bool IsHexColor(string value)
        {
            if (value == null || value.Length != 7 || value[0] != '#') return false;
            for (int i = 1; i < 7; i++)
            {
                char c = value[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!hex) return false;
            }
            return true;
        }
    }
}
