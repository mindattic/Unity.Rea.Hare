using System;
using System.Collections.Generic;
using Unity.Rea.Hare.Content;
using Unity.Rea.Hare.Flow;

namespace Unity.Rea.Hare.Rooms
{
    /// <summary>
    /// room_intro as data (the content registry's <c>intro</c> section, AGENTS.md rule 4): obj_drop_intro and its clouds
    /// and backgrounds (<see cref="Scene"/>), its A and D keys, the tip it makes when it stops (the controls list, the
    /// registry's <c>popups.Tip.IntroText</c>), and where it sends the game once it has fallen out of the room
    /// (<c>gamemode = gm_quickstart; current_scenario = -1; current_level = 1; room_goto(room_gamescreen)</c>).
    /// obj_intro's Esc (<c>room_goto(room_title)</c>) is the room flow's (the rooms table's EscapeToTitle). The defaults
    /// are the original's, read from HARE.gmx.
    /// </summary>
    [Serializable]
    public class IntroTuning
    {
        /// <summary>The falling rabbit, which hangs until a key, its clouds and the backgrounds.</summary>
        public DropSceneTuning Scene = DropSceneTuning.Intro();
        /// <summary>A and D: <c>if(startX &gt; 300) startX -= 1</c>, <c>if(startX &lt; room_width - 300) startX += 1</c>.</summary>
        public int SteerMargin = 300;
        public int SteerStep = 1;
        /// <summary>The quick game it starts: <c>current_level = 1</c>.</summary>
        public int ExitLevel = 1;
        /// <summary><c>room_goto(room_gamescreen)</c>.</summary>
        public RoomId ExitRoom = RoomId.GameScreen;

        internal void Validate(List<string> errors, string path)
        {
            if (Scene == null) errors.Add(path + ".Scene: must not be null.");
            else Scene.Validate(errors, path + ".Scene");
            ValidationHelpers.NonNegative(errors, path + ".SteerMargin", SteerMargin);
            ValidationHelpers.Positive(errors, path + ".SteerStep", SteerStep);
            ValidationHelpers.Positive(errors, path + ".ExitLevel", ExitLevel);
            if (!Enum.IsDefined(typeof(RoomId), ExitRoom)) errors.Add($"{path}.ExitRoom: not a room (was {(int)ExitRoom}).");
        }
    }
}
