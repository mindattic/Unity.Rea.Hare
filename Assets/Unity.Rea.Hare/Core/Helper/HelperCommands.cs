namespace Unity.Rea.Hare.Helper
{
    /// <summary>The only way the helper field changes. Runs in <see cref="HelperField.Execute"/>.</summary>
    public abstract class HelperCommand
    {
        /// <summary>Does the work; false when the command was refused.</summary>
        internal abstract bool Apply(HelperField field);
    }

    /// <summary>
    /// obj_player's arrow key presses (37-40): create_helper, <c>if (!instance_exists(obj_helper)) helper =
    /// instance_create(x, y, obj_helper)</c>, heading the ship's <c>image_angle</c>. Refused while the ship's helper exists.
    /// </summary>
    public sealed class CallHelper : HelperCommand
    {
        readonly PlayerId owner;
        readonly Vec2 at;
        readonly float imageAngle;

        public CallHelper(PlayerId owner, Vec2 at, float imageAngle)
        {
            this.owner = owner;
            this.at = at;
            this.imageAngle = imageAngle;
        }

        internal override bool Apply(HelperField field) => field.Call(owner, at, imageAngle) != null;
    }

    /// <summary>
    /// Which of obj_helper's held arrows are down: left (37) <c>direction += turn_speed</c>, right (39) <c>direction -=
    /// turn_speed</c>, each step it is held, free and unpaused, setting <c>turning</c>. Letting either go is its key-release
    /// event, <c>turning = false</c> (paused or not).
    /// </summary>
    public sealed class HoldHelperTurn : HelperCommand
    {
        readonly PlayerId owner;
        readonly bool counterclockwise, clockwise;

        public HoldHelperTurn(PlayerId owner, bool counterclockwise, bool clockwise)
        {
            this.owner = owner;
            this.counterclockwise = counterclockwise;
            this.clockwise = clockwise;
        }

        internal override bool Apply(HelperField field) => field.HoldTurn(owner, counterclockwise, clockwise);
    }

    /// <summary>obj_helper's Up press (38): <c>if (speed &lt; max_speed) { speed += 1; Speed = speed }</c>, free and unpaused.</summary>
    public sealed class PressHelperFaster : HelperCommand
    {
        readonly PlayerId owner;
        public PressHelperFaster(PlayerId owner) => this.owner = owner;
        internal override bool Apply(HelperField field) => field.ChangeSpeed(owner, +1);
    }

    /// <summary>obj_helper's Down press (40): <c>if (speed &gt; 1) { speed -= 1; Speed = speed }</c>, free and unpaused.</summary>
    public sealed class PressHelperSlower : HelperCommand
    {
        readonly PlayerId owner;
        public PressHelperSlower(PlayerId owner) => this.owner = owner;
        internal override bool Apply(HelperField field) => field.ChangeSpeed(owner, -1);
    }

    /// <summary>obj_helper's Page Down press (34): <c>solo = !solo</c> (no pause check).</summary>
    public sealed class ToggleHelperSolo : HelperCommand
    {
        readonly PlayerId owner;
        public ToggleHelperSolo(PlayerId owner) => this.owner = owner;
        internal override bool Apply(HelperField field) => field.ToggleSolo(owner);
    }

    /// <summary>obj_helper's Delete press (46): it goes off as an obj_rocket_explosion (no pause check).</summary>
    public sealed class DetonateHelper : HelperCommand
    {
        readonly PlayerId owner;
        public DetonateHelper(PlayerId owner) => this.owner = owner;
        internal override bool Apply(HelperField field) => field.Detonate(owner);
    }

    /// <summary>What the helper of ship <c>id</c> reads from <c>player</c> this step.</summary>
    public sealed class SetHelperOwner : HelperCommand
    {
        readonly PlayerId id;
        readonly HelperOwner owner;

        public SetHelperOwner(PlayerId id, HelperOwner owner)
        {
            this.id = id;
            this.owner = owner;
        }

        internal override bool Apply(HelperField field) { field.SetOwner(id, owner); return true; }
    }

    /// <summary>View 0 in the world, for in_view(x, y); null when there is no view (everything counts as in it).</summary>
    public sealed class SetHelperView : HelperCommand
    {
        readonly RoomBounds? view;
        public SetHelperView(RoomBounds? view) => this.view = view;
        internal override bool Apply(HelperField field) { field.View = view; return true; }
    }

    /// <summary>GML paused: the helper stops (<c>speed = 0</c>) and its gated work waits; its alarms keep counting.</summary>
    public sealed class SetHelpersPaused : HelperCommand
    {
        readonly bool paused;
        public SetHelpersPaused(bool paused) => this.paused = paused;
        internal override bool Apply(HelperField field)
        {
            if (field.Paused == paused) return false;
            field.Paused = paused;
            return true;
        }
    }

    /// <summary><c>dt</c> seconds of steps: each whole step the alarms, the held arrows, the Step and the built-in motion.</summary>
    public sealed class TickHelpers : HelperCommand
    {
        readonly float dt;
        public TickHelpers(float dt) => this.dt = dt;
        internal override bool Apply(HelperField field) { field.Tick(dt * GameClock.StepsPerSecond); return true; }
    }

    /// <summary>A helper's view was destroyed from outside (its room unloading): it leaves the field quietly.</summary>
    public sealed class ForgetHelper : HelperCommand
    {
        readonly int id;
        public ForgetHelper(int id) => this.id = id;
        internal override bool Apply(HelperField field) => field.Forget(id);
    }
}
