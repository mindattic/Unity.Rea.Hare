using System;
using System.Collections.Generic;
using Unity.Rea.Hare.Content;

namespace Unity.Rea.Hare.Arena
{
    /// <summary>obj_moon_surface's numbers (U.HARE-T58).</summary>
    [Serializable]
    public class MoonSurfaceDefinition
    {
        /// <summary>The object's sprite.</summary>
        public string Sprite = "spr_moon_surface";
        /// <summary>The object's depth 1.</summary>
        public int Depth = 1;
        /// <summary>Its Step's <c>vspeed = -40</c>: how fast it rises while below the room, pixels a step.</summary>
        public int RiseSpeed = 40;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Sprite", Sprite);
            ValidationHelpers.Positive(errors, path + ".RiseSpeed", RiseSpeed);
        }
    }

    /// <summary>
    /// obj_moon_surface (U.HARE-T58), engine-free: spr_moon_surface at depth 1 with a <c>phase</c>. Create sets
    /// <c>phase = "off"</c>; while <c>phase == "on"</c> its Step rises it (<c>vspeed = -40</c>) for as long as it is below
    /// the room (<c>y &gt; room_height</c>), and the first step it is not, stops it (<c>vspeed = 0</c>), destroys every
    /// obj_drop_emitter and sets <c>phase = "off"</c>. GameMaker moves it by its vspeed after the Step.
    ///
    /// Nothing in HARE.gmx creates one and nothing sets its phase to "on": the title's moon is drawn by obj_stand_title
    /// (<c>draw_sprite(spr_moon_surface, 0, 0, y + 250)</c>), so this object is content the original never reaches. It is
    /// ported as it is, created by nothing, as obj_oval_explosion is (BIBLE §7, RECONSTRUCTION).
    /// </summary>
    public sealed class MoonSurface
    {
        public MoonSurface(MoonSurfaceDefinition definition, float y)
        {
            Definition = definition ?? new MoonSurfaceDefinition();
            Y = y;
        }

        public MoonSurfaceDefinition Definition { get; }

        /// <summary>GML y, room pixels.</summary>
        public float Y { get; private set; }

        /// <summary>GML vspeed, pixels a step.</summary>
        public float VSpeed { get; private set; }

        /// <summary>GML <c>phase == "on"</c>.</summary>
        public bool On { get; private set; }

        /// <summary><c>phase = "on"</c>: whatever would bring the moon up (nothing in the original does).</summary>
        public void TurnOn() => On = true;

        /// <summary>
        /// One step in a room <paramref name="roomHeight"/> tall: the Step event, then GameMaker's motion. True on the
        /// step it arrives, when the original destroys every obj_drop_emitter.
        /// </summary>
        public bool Step(int roomHeight)
        {
            bool arrived = false;
            if (On)
            {
                if (Y > roomHeight) VSpeed = -Definition.RiseSpeed;
                else
                {
                    VSpeed = 0f;
                    On = false;
                    arrived = true;
                }
            }
            Y += VSpeed;
            return arrived;
        }
    }
}
