namespace Unity.Hare.Enemies
{
    public enum InsectKind
    {
        Butterfly,
        Firefly,
        Fly
    }

    /// <summary>
    /// What obj_butterfly, obj_firefly and obj_fly (gmlHARE) share. None of the three has a parent or any collision
    /// event, so they are ambient swarm life rather than par_enemy: weapons pass through them, rockets and mines never
    /// home on them, and they never touch the ship. Create drops each one at a random point in the lower half of the
    /// room with dX = dY = 1 and sets Alarm 0 to irandom_range(5, 30); Alarm 0 runs the insect's ai_* script
    /// (<see cref="Think"/>), which picks a new random velocity and applies the bounds rules, then re-arms itself.
    /// Step adds dX/dY to x/y every step. dX/dY are kept in GML units (px/step, y down) so the ai_* numbers read the
    /// same as the source; the position flips y for the world's y-up axis.
    /// </summary>
    public abstract class InsectBody
    {
        /// <summary>Create's alarm[0] = irandom_range(5, 30).</summary>
        public const int FirstThinkMinSteps = 5;
        public const int FirstThinkMaxSteps = 30;
        /// <summary>ai_* bounds: within 10 px of the left/right walls the insect turns back.</summary>
        public const float SideMarginPx = 10f;

        IRandomSource rng;

        public int Id { get; private set; }
        public abstract InsectKind Kind { get; }
        public RoomBounds Room { get; private set; }
        public Vec2 Position { get; internal set; }
        /// <summary>GML dX: px/step, positive right.</summary>
        public float DX { get; protected set; }
        /// <summary>GML dY: px/step, positive down (GML's y axis).</summary>
        public float DY { get; protected set; }
        /// <summary>Steps until Alarm 0 next runs <see cref="Think"/>.</summary>
        public float ThinkCountdownSteps { get; private set; }

        /// <summary>The alarm[0] = irandom_range(5, N) that the Alarm 0 event re-arms with.</summary>
        public abstract int ThinkMaxSteps { get; }

        protected IRandomSource Rng => rng;

        internal void Attach(IRandomSource random, int id, RoomBounds room)
        {
            rng = random;
            Id = id;
            Room = room;
        }

        /// <summary>GML Create. <paramref name="position"/> overrides the random spot in the room's lower half (tests pin it).</summary>
        internal void Create(Vec2? position)
        {
            // x = random_range(0, room_width); y = random_range(room_height/2, room_height)
            Position = position ?? new Vec2(rng.Range(Room.XMin, Room.XMax), rng.Range(Room.YMin, Room.CenterY));
            DX = 1f;
            DY = 1f;
            ThinkCountdownSteps = rng.IRandom(FirstThinkMinSteps, FirstThinkMaxSteps);
            OnCreate();
        }

        protected virtual void OnCreate() { }

        /// <summary>One GML step (Alarm 0, then Step), <paramref name="steps"/> GML steps long.</summary>
        internal void Tick(float steps)
        {
            ThinkCountdownSteps -= steps;
            // Alarms fire on whole steps; the slack absorbs float drift.
            while (ThinkCountdownSteps <= 1e-3f)
            {
                Think();
                ThinkCountdownSteps += rng.IRandom(FirstThinkMinSteps, ThinkMaxSteps);
            }

            // Step: x += dX; y += dY (GML y down, the world's y up).
            Position = new Vec2(Position.X + GameClock.Px(DX * steps), Position.Y - GameClock.Px(DY * steps));
        }

        /// <summary>The insect's ai_* script: a new random velocity, then the bounds rules.</summary>
        internal abstract void Think();

        /// <summary>GML y &lt; room_height/2.</summary>
        protected bool InUpperHalf => Position.Y > Room.CenterY;
        /// <summary>GML y &gt; room_height - <paramref name="px"/>.</summary>
        protected bool NearBottom(float px) => Position.Y < Room.YMin + GameClock.Px(px);
        /// <summary>GML x &lt; 10.</summary>
        protected bool NearLeft => Position.X < Room.XMin + GameClock.Px(SideMarginPx);
        /// <summary>GML x &gt; room_width - 10.</summary>
        protected bool NearRight => Position.X > Room.XMax - GameClock.Px(SideMarginPx);
    }

    /// <summary>
    /// obj_butterfly and ai_butterfly (gmlHARE). Every irandom_range(5, 60) steps it picks dX, dY in [-2, 2]; in the
    /// room's upper half it is pushed down (dY = 2), within 120 px of the floor up (dY = -2), and within 10 px of a
    /// side wall back in (dX = +/-2), so butterflies stay in the lower half. The sprite faces its dX (image_xscale = 1
    /// when dX &gt; 0, else -1).
    /// </summary>
    public sealed class ButterflyBody : InsectBody
    {
        public const float MaxSpeedPxPerStep = 2f;
        public const float FloorMarginPx = 120f;
        public const int ThinkMaxStepsValue = 60;

        public override InsectKind Kind => InsectKind.Butterfly;
        public override int ThinkMaxSteps => ThinkMaxStepsValue;

        /// <summary>GML image_xscale = 1 (true) or -1.</summary>
        public bool FacingRight { get; private set; } = true;

        internal override void Think()
        {
            DX = Rng.Range(-MaxSpeedPxPerStep, MaxSpeedPxPerStep);
            DY = Rng.Range(-MaxSpeedPxPerStep, MaxSpeedPxPerStep);

            if (InUpperHalf) DY = MaxSpeedPxPerStep;
            else if (NearBottom(FloorMarginPx)) DY = -MaxSpeedPxPerStep;

            if (NearLeft) DX = MaxSpeedPxPerStep;
            else if (NearRight) DX = -MaxSpeedPxPerStep;

            FacingRight = DX > 0f;
        }
    }

    /// <summary>
    /// obj_firefly and ai_firefly (gmlHARE). Every irandom_range(5, 30) steps it picks dX in [-3, 3] and dY in [-1, 1];
    /// in the room's upper half it sinks (dY = 1) and within 10 px of a side wall it turns back (dX = +/-3). There is
    /// no floor rule, so a firefly can drift out of the bottom of the room, as in the original. Each think has a 1-in-5
    /// chance (irandom(4) == 4) to blink: toggle between spr_firefly and spr_firefly_dark.
    /// </summary>
    public sealed class FireflyBody : InsectBody
    {
        public const float MaxSpeedXPxPerStep = 3f;
        public const float MaxSpeedYPxPerStep = 1f;
        public const int ThinkMaxStepsValue = 30;

        public override InsectKind Kind => InsectKind.Firefly;
        public override int ThinkMaxSteps => ThinkMaxStepsValue;

        /// <summary>GML sprite_index == spr_firefly_dark.</summary>
        public bool IsDark { get; private set; }

        protected override void OnCreate() => IsDark = false;

        internal override void Think()
        {
            DX = Rng.Range(-MaxSpeedXPxPerStep, MaxSpeedXPxPerStep);
            DY = Rng.Range(-MaxSpeedYPxPerStep, MaxSpeedYPxPerStep);

            if (InUpperHalf) DY = MaxSpeedYPxPerStep;

            if (NearLeft) DX = MaxSpeedXPxPerStep;
            else if (NearRight) DX = -MaxSpeedXPxPerStep;

            if (Rng.IRandom(0, 4) == 4) IsDark = !IsDark; // blink = irandom(4); if (blink == 4)
        }
    }

    /// <summary>
    /// obj_fly and ai_fly (gmlHARE). Every irandom_range(5, 30) steps it picks dX in [-4, 4] and dY in [-2, 2]; in the
    /// room's upper half it sinks (dY = 1, not 2) and within 10 px of a side wall it turns back (dX = +/-4). Like the
    /// firefly it has no floor rule.
    /// </summary>
    public sealed class FlyBody : InsectBody
    {
        public const float MaxSpeedXPxPerStep = 4f;
        public const float MaxSpeedYPxPerStep = 2f;
        public const float SinkSpeedPxPerStep = 1f;
        public const int ThinkMaxStepsValue = 30;

        public override InsectKind Kind => InsectKind.Fly;
        public override int ThinkMaxSteps => ThinkMaxStepsValue;

        internal override void Think()
        {
            DX = Rng.Range(-MaxSpeedXPxPerStep, MaxSpeedXPxPerStep);
            DY = Rng.Range(-MaxSpeedYPxPerStep, MaxSpeedYPxPerStep);

            if (InUpperHalf) DY = SinkSpeedPxPerStep;

            if (NearLeft) DX = MaxSpeedXPxPerStep;
            else if (NearRight) DX = -MaxSpeedXPxPerStep;
        }
    }
}
