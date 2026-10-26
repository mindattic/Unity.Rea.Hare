namespace Unity.Hare.Arena
{
    /// <summary>
    /// The only way the arena changes. A command carries the id of the seat it acts for (no global current player) and
    /// runs in <see cref="ArenaWorld.Execute"/>, which returns whether it took effect and what it raised.
    /// </summary>
    public abstract class ArenaCommand
    {
        protected ArenaCommand(PlayerId id) => Id = id;

        public PlayerId Id { get; }

        /// <summary>Does the work; false when the command was refused (the rules said no).</summary>
        internal abstract bool Apply(ArenaWorld arena);
    }

    /// <summary>obj_game's Create: the minimap shown or not from show_map (<see cref="MinimapToggled"/>). Once only.</summary>
    public sealed class CreateArena : ArenaCommand
    {
        public CreateArena(PlayerId id) : base(id) { }

        internal override bool Apply(ArenaWorld arena) => arena.Create();
    }

    /// <summary>One step at the room's room_speed: the backgrounds scroll by their speeds. Refused before Create.</summary>
    public sealed class StepArena : ArenaCommand
    {
        public StepArena(PlayerId id) : base(id) { }

        internal override bool Apply(ArenaWorld arena) => arena.Step();
    }

    /// <summary>
    /// obj_player's Step boundary check for the seat's player at (x, y), room pixels: a wrap
    /// (<see cref="PlayerWrapped"/>) or a lost life (<see cref="PlayerOutOfBounds"/>). Refused (false) when the player is
    /// where it may be.
    /// </summary>
    public sealed class CheckPlayerBounds : ArenaCommand
    {
        readonly float x, y;

        public CheckPlayerBounds(PlayerId id, float x, float y) : base(id)
        {
            this.x = x;
            this.y = y;
        }

        internal override bool Apply(ArenaWorld arena) => arena.CheckPlayer(x, y);
    }

    /// <summary>The views' update for a step: view 0 and the minimap follow the seat's player at (x, y), room pixels. Refused before Create.</summary>
    public sealed class FollowPlayer : ArenaCommand
    {
        readonly float x, y;

        public FollowPlayer(PlayerId id, float x, float y) : base(id)
        {
            this.x = x;
            this.y = y;
        }

        internal override bool Apply(ArenaWorld arena) => arena.Follow(x, y);
    }

    /// <summary>
    /// obj_surface_reflection's Alarm 0 and Step for the seat's ship at (x, y), room pixels, turned to imageAngle and
    /// heading direction (degrees): a splash on the water line (<see cref="SurfaceSplashed"/>) and the water's floor
    /// (<see cref="ShipSkimmedSurface"/>). Refused before Create and in a room with no reflection.
    /// </summary>
    public sealed class StepReflection : ArenaCommand
    {
        readonly float x, y, imageAngle, direction;

        public StepReflection(PlayerId id, float x, float y, float imageAngle, float direction) : base(id)
        {
            this.x = x;
            this.y = y;
            this.imageAngle = imageAngle;
            this.direction = direction;
        }

        internal override bool Apply(ArenaWorld arena) => arena.StepReflection(x, y, imageAngle, direction);
    }

    /// <summary>obj_game's M key: show_map flips and view 1 with it (<see cref="MinimapToggled"/>).</summary>
    public sealed class ToggleMinimap : ArenaCommand
    {
        public ToggleMinimap(PlayerId id) : base(id) { }

        internal override bool Apply(ArenaWorld arena) => arena.ToggleMinimap();
    }

    /// <summary>
    /// random_positions for the room's <c>count</c> rocks, structures and enemies (<see cref="PositionsScattered"/>, one
    /// point each, in the order the view counted them).
    /// </summary>
    public sealed class ScatterObjects : ArenaCommand
    {
        readonly int count;

        public ScatterObjects(PlayerId id, int count) : base(id) => this.count = count;

        internal override bool Apply(ArenaWorld arena) => arena.Scatter(count);
    }
}
