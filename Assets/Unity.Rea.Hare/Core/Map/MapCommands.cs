namespace Unity.Rea.Hare.Map
{
    /// <summary>
    /// The only way the map changes. A command carries the id of the seat it acts for (no global current player) and
    /// runs in <see cref="MapScreen.Execute"/>, which returns whether it took effect and what it raised.
    /// </summary>
    public abstract class MapCommand
    {
        protected MapCommand(PlayerId id) => Id = id;

        public PlayerId Id { get; }

        /// <summary>Does the work; false when the command was refused (the rules said no).</summary>
        internal abstract bool Apply(MapScreen screen);
    }

    /// <summary>
    /// The room comes up: the room's instances' Create events in room order (obj_earthball, obj_uss_warren, obj_scanner,
    /// obj_destination, obj_map with its fade, pointer, Upgrades button, mission markers, conversation box and tip,
    /// obj_scenario_details). Once only.
    /// </summary>
    public sealed class CreateMap : MapCommand
    {
        public CreateMap(PlayerId id) : base(id) { }
        internal override bool Apply(MapScreen screen) => screen.Create();
    }

    /// <summary>One step at the room's room_speed. Refused before Create.</summary>
    public sealed class StepMap : MapCommand
    {
        public StepMap(PlayerId id) : base(id) { }
        internal override bool Apply(MapScreen screen) => screen.Step();
    }

    /// <summary>
    /// obj_map's A key press: while idle and before the launch, sfx_flick, <c>mode = "moving"</c>, counterclockwise,
    /// the sky scrolling left, the next mission and the comm box sliding out. Refused otherwise.
    /// </summary>
    public sealed class PressMapA : MapCommand
    {
        public PressMapA(PlayerId id) : base(id) { }
        internal override bool Apply(MapScreen screen) => screen.Turn(MapTurn.Counterclockwise);
    }

    /// <summary>obj_map's D key press: as <see cref="PressMapA"/>, clockwise, the sky scrolling right, the mission before.</summary>
    public sealed class PressMapD : MapCommand
    {
        public PressMapD(PlayerId id) : base(id) { }
        internal override bool Apply(MapScreen screen) => screen.Turn(MapTurn.Clockwise);
    }

    /// <summary>
    /// obj_map's key press for any key: unless A, D or Esc is held, with a mission reached and no white dot yet, the
    /// white dot leaves the ship for obj_destination, the earth's slow turn stops and sfx_takeoff plays. Refused
    /// otherwise, and (by the owner's direction) on the locked boss's mission.
    /// </summary>
    public sealed class PressMapKey : MapCommand
    {
        readonly bool aHeld, dHeld, escapeHeld;

        public PressMapKey(PlayerId id, bool aHeld = false, bool dHeld = false, bool escapeHeld = false) : base(id)
        {
            this.aHeld = aHeld;
            this.dHeld = dHeld;
            this.escapeHeld = escapeHeld;
        }

        internal override bool Apply(MapScreen screen) => screen.Launch(aHeld, dHeld, escapeHeld);
    }

    /// <summary>The mouse at room pixel (x, y): obj_btn_upgrade's mouse enter and leave events.</summary>
    public sealed class PointMap : MapCommand
    {
        readonly double x, y;

        public PointMap(PlayerId id, double x, double y) : base(id)
        {
            this.x = x;
            this.y = y;
        }

        internal override bool Apply(MapScreen screen) => screen.Point(x, y);
    }

    /// <summary>The left button pressed at room pixel (x, y): obj_btn_upgrade's left pressed event. Refused off the button.</summary>
    public sealed class PressMap : MapCommand
    {
        readonly double x, y;

        public PressMap(PlayerId id, double x, double y) : base(id)
        {
            this.x = x;
            this.y = y;
        }

        internal override bool Apply(MapScreen screen) => screen.Press(x, y);
    }
}
