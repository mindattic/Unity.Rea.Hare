using System;

namespace Unity.Hare.Flow
{
    /// <summary>
    /// The original's rooms, in HARE.project.gmx's room order. room_boss1 is left out: no script or object ever
    /// enters it (the boss is level 8 in room_gamescreen; docs/RECONSTRUCTION.md, "Content the original never
    /// reaches").
    /// </summary>
    public enum RoomId
    {
        Startup,
        Title,
        Intro,
        GameScreen,
        SideScroll,
        Shaft,
        Map,
        Story,
        Upgrade,
    }

    /// <summary>
    /// What a room's <c>obj_fade_*</c> does once its fade-in turns the screen opaque. obj_fade_title, obj_fade_story,
    /// obj_fade_map and obj_fade_upgrade run <c>room_goto(next_room)</c>; obj_fade_game runs
    /// <c>current_level += 1; setup_level()</c>. startup and room_intro have no fade at all.
    /// </summary>
    public enum FadeKind
    {
        None,
        NextRoom,
        NextLevel,
    }

    /// <summary>The GML names of the rooms, which the generated room scenes are named after.</summary>
    public static class RoomList
    {
        /// <summary>Every room, in HARE.project.gmx's order (the order Build Settings lists their scenes in).</summary>
        public static readonly RoomId[] All =
        {
            RoomId.Startup, RoomId.Title, RoomId.Intro, RoomId.GameScreen, RoomId.SideScroll, RoomId.Shaft,
            RoomId.Map, RoomId.Story, RoomId.Upgrade,
        };

        /// <summary>The room's name in HARE.gmx (startup, room_title, ...).</summary>
        public static string GmlName(RoomId room)
        {
            switch (room)
            {
                case RoomId.Startup: return "startup";
                case RoomId.Title: return "room_title";
                case RoomId.Intro: return "room_intro";
                case RoomId.GameScreen: return "room_gamescreen";
                case RoomId.SideScroll: return "room_sidescroll";
                case RoomId.Shaft: return "room_shaft";
                case RoomId.Map: return "room_map";
                case RoomId.Story: return "room_story";
                case RoomId.Upgrade: return "room_upgrade";
                default: throw new ArgumentOutOfRangeException(nameof(room), room, "Not a room.");
            }
        }

        /// <summary>The room called <paramref name="gmlName"/>, if there is one.</summary>
        public static bool TryParse(string gmlName, out RoomId room)
        {
            foreach (var candidate in All)
            {
                if (string.Equals(GmlName(candidate), gmlName, StringComparison.Ordinal))
                {
                    room = candidate;
                    return true;
                }
            }
            room = default;
            return false;
        }
    }
}
