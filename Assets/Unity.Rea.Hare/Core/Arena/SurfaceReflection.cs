using System;

namespace Unity.Rea.Hare.Arena
{
    /// <summary>
    /// obj_surface_reflection as it stands (U.HARE-T58): room_sidescroll's water line over bg_tropicalocean and the ship's
    /// reflection in it. It is state only; <see cref="ArenaWorld"/> steps it through <see cref="StepReflection"/> and the
    /// view draws it. All positions are room pixels, y growing downward.
    ///
    /// The original (obj_surface_reflection.object.gmx, depth 2, no sprite of its own):
    /// <list type="bullet">
    /// <item>Create: <c>range = 250; surface_y = room_height - range; alpha = 0.3; xscale = 1; yscale = 0.5;
    /// flipped = false; rate = 5; alarm[0] = rate</c>.</item>
    /// <item>Alarm 0: <c>if(player.y &gt; surface_y - 32) effect_create_below(ef_firework, player.x + lengthdir_x(32,
    /// image_angle), surface_y, 2, c_cornflowerblue)</c>, then <c>alarm[0] = rate</c>.</item>
    /// <item>Step: <c>y = surface_y + (surface_y - player.y)</c>, never above the water line; <c>angle =
    /// player.image_angle</c>; and the water is a floor: <c>if(player.y &gt; surface_y) { player.y = surface_y;
    /// player.direction = player.direction &gt; 270 ? 0 : 180; player.image_angle = player.direction; }</c>.</item>
    /// <item>Draw: <c>draw_sprite_ext(player.sprite_index, 0, player.x, y, xscale, yscale, angle, c_white, alpha)</c>.</item>
    /// </list>
    /// </summary>
    public sealed class SurfaceReflection
    {
        int splashAlarm;

        internal SurfaceReflection(SurfaceReflectionDefinition definition, int roomHeight)
        {
            Definition = definition ?? new SurfaceReflectionDefinition();
            SurfaceY = roomHeight - Definition.Range;
            Y = SurfaceY;
            XScale = Definition.XScale;
            YScale = Definition.YScale;
            splashAlarm = Definition.SplashRateSteps;
        }

        public SurfaceReflectionDefinition Definition { get; }

        /// <summary>Whether the room has an obj_surface_reflection.</summary>
        public bool Active => Definition.Enabled;

        /// <summary>GML surface_y: the water line, <c>room_height - range</c> (830 in the 1080 px side-scroll).</summary>
        public float SurfaceY { get; }

        /// <summary>Where the reflection is drawn: GML <c>player.x</c>.</summary>
        public float X { get; private set; }

        /// <summary>GML y: as far below the water line as the ship is above it.</summary>
        public float Y { get; private set; }

        /// <summary>GML angle: the ship's image_angle.</summary>
        public float Angle { get; private set; }

        /// <summary>GML xscale and yscale.</summary>
        public float XScale { get; }
        public float YScale { get; }

        /// <summary>GML alpha.</summary>
        public float Alpha => Definition.Alpha;

        /// <summary>Whether the reflection has been placed (it has had a step with a ship to follow).</summary>
        public bool Placed { get; private set; }

        /// <summary>Alarm 0's countdown; true on the step it rings (and it is set again to rate).</summary>
        internal bool TickSplashAlarm()
        {
            if (--splashAlarm > 0) return false;
            splashAlarm = Definition.SplashRateSteps;
            return true;
        }

        /// <summary>Alarm 0's test: the ship at <paramref name="y"/> is within the splash band above the water line (or below it).</summary>
        public bool Splashes(float y) => y > SurfaceY - Definition.SplashBand;

        /// <summary>Alarm 0's <c>splashX = player.x + lengthdir_x(Len, image_angle)</c>.</summary>
        public float SplashX(float x, float imageAngle) => x + Definition.SplashLead * (float)Math.Cos(imageAngle * Math.PI / 180.0);

        /// <summary>The Step's reflection: <c>y = surface_y + (surface_y - player.y)</c>, kept at or below the water line.</summary>
        internal void Follow(float x, float y, float imageAngle)
        {
            X = x;
            Y = Math.Max(SurfaceY, SurfaceY + (SurfaceY - y));
            Angle = imageAngle;
            Placed = true;
        }

        /// <summary>
        /// The Step's floor: the heading a ship at <paramref name="direction"/> takes when it reaches the water line,
        /// <c>if(player.direction &gt; 270) 0 else 180</c> (GameMaker keeps direction in [0, 360)).
        /// </summary>
        public static float SkimDirection(float direction)
        {
            float wrapped = direction % 360f;
            if (wrapped < 0f) wrapped += 360f;
            return wrapped > 270f ? 0f : 180f;
        }
    }
}
