using Unity.Rea.Hare.Enemies;

namespace Unity.Rea.Hare.Helper
{
    /// <summary>obj_helper's <c>state</c>.</summary>
    public enum HelperState
    {
        /// <summary><c>"free"</c>: flying where it points, steered by the arrows (or the mouse, solo), smoking gold.</summary>
        Free,
        /// <summary><c>"return"</c>: it left the view, so it heads straight for the ship, smoking orange, until within 400 px.</summary>
        Return
    }

    /// <summary>
    /// What obj_helper reads from <c>player</c> each step: where the ship is, how fast it flies (<c>player.speed</c>), its
    /// bounding box (for <c>distance_to_object(player)</c>) and where the mouse is (<c>mouse_x</c>, <c>mouse_y</c>, for
    /// solo). World units, y up; the speed GML px a step.
    /// </summary>
    public readonly struct HelperOwner
    {
        public HelperOwner(Vec2 position, float speedPxPerStep, RoomBounds box, Vec2? mouse)
        {
            Position = position;
            SpeedPxPerStep = speedPxPerStep;
            Box = box;
            Mouse = mouse;
        }

        public Vec2 Position { get; }
        public float SpeedPxPerStep { get; }
        /// <summary>The ship's bounding box, world units.</summary>
        public RoomBounds Box { get; }
        /// <summary>The mouse in the world, or null while there is no pointer (the last one seen holds).</summary>
        public Vec2? Mouse { get; }
    }

    /// <summary>
    /// One obj_helper, the helper hawk a ship calls with an arrow key: where it is (world units, y up), GameMaker's
    /// built-in <c>direction</c> (kept in [0, 360) as GameMaker keeps it) and <c>speed</c>, its own <c>Speed</c> (what
    /// the Step puts back into <c>speed</c> each step, so a pause's <c>speed = 0</c> does not stick), <c>image_angle</c>,
    /// <c>state</c>, <c>turning</c>, <c>solo</c> and its alarms. Read-only outside the <see cref="HelperField"/>.
    /// </summary>
    public sealed class HelperHawk
    {
        internal HelperHawk(int id, PlayerId owner, Vec2 position)
        {
            Id = id;
            Owner = owner;
            Position = position;
        }

        public int Id { get; }
        /// <summary>The ship whose arrow key called it (GML <c>player</c>, the one ship there is).</summary>
        public PlayerId Owner { get; }
        public Vec2 Position { get; internal set; }

        /// <summary>GML direction, degrees in [0, 360).</summary>
        public float Direction
        {
            get => direction;
            internal set
            {
                float wrapped = GmlMath.Wrap(value);
                direction = wrapped >= 360f ? 0f : wrapped;
            }
        }

        /// <summary>GML image_angle, degrees (not wrapped: the arrows turn it by turn_speed as they turn direction).</summary>
        public float ImageAngle { get; internal set; }
        /// <summary>GML speed, px a step (0 while paused).</summary>
        public float SpeedPx { get; internal set; }
        /// <summary>GML Speed: the speed the Step restores every unpaused step.</summary>
        public float SpeedSetting { get; internal set; }
        /// <summary>GML last_speed: the speed it had as it left the view, given back once it is home.</summary>
        public float LastSpeed { get; internal set; }
        /// <summary>GML max_speed.</summary>
        public float MaxSpeed { get; internal set; }
        /// <summary>GML turn_speed, degrees a step.</summary>
        public float TurnSpeed { get; internal set; }
        public HelperState State { get; internal set; }
        /// <summary>GML turning: an arrow is turning it (Alarm 1 sparks while it is).</summary>
        public bool Turning { get; internal set; }
        /// <summary>GML solo: Page Down's toggle; while set it steers itself toward the mouse.</summary>
        public bool Solo { get; internal set; }
        /// <summary>GML firstPass: the next free step leaves a white ef_ring.</summary>
        public bool FirstPass { get; internal set; } = true;
        public bool IsGone { get; internal set; }

        /// <summary>GML hspeed/vspeed in world units a step (y up).</summary>
        public Vec2 StepVelocity => GmlMath.Dir(Direction) * GameClock.Px(SpeedPx);

        float direction;
        internal int SmokeAlarm;
        internal int SparkAlarm;
        internal bool CounterclockwiseHeld;
        internal bool ClockwiseHeld;
    }
}
