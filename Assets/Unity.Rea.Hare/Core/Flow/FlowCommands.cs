namespace Unity.Rea.Hare.Flow
{
    /// <summary>
    /// The only way the room flow changes. A command carries the id of the seat it acts for (no global current
    /// player) and runs in <see cref="RoomFlow.Execute"/>, which returns whether it took effect and the events it raised.
    /// </summary>
    public abstract class FlowCommand
    {
        protected FlowCommand(PlayerId id) => Id = id;

        public PlayerId Id { get; }

        /// <summary>Does the work; false when the command was refused (the rules said no).</summary>
        internal abstract bool Apply(RoomFlow flow);
    }

    /// <summary>room_goto(room): leaves for <c>room</c> at once (<see cref="RoomChanged"/>).</summary>
    public sealed class GoToRoom : FlowCommand
    {
        readonly RoomId room;

        public GoToRoom(PlayerId id, RoomId room) : base(id) => this.room = room;

        internal override bool Apply(RoomFlow flow) => flow.GoTo(room);
    }

    /// <summary>
    /// The room came up without the flow sending it there: room_restart (obj_player's death reloading the room) or a
    /// room scene opened on its own. A fresh fade, as the room's Create events make (<see cref="RoomEntered"/>).
    /// </summary>
    public sealed class EnterRoom : FlowCommand
    {
        readonly RoomId room;

        public EnterRoom(PlayerId id, RoomId room) : base(id) => this.room = room;

        internal override bool Apply(RoomFlow flow) => flow.Reenter(room);
    }

    /// <summary>
    /// A menu's <c>next_room = room; fade.mode = "fadein"; fade.firstPass = true;</c>: the room's fade goes to black
    /// and then to <c>room</c> (<see cref="FadeInStarted"/>, then a <see cref="RoomChanged"/> from a later
    /// <see cref="StepRoom"/>). With <c>onlyWhenIdle</c>, refused while a fade is running (the buttons that check
    /// <c>obj_fade_title.mode == "off"</c> first). Refused in a room with no fade.
    /// </summary>
    public sealed class FadeToRoom : FlowCommand
    {
        readonly RoomId room;
        readonly bool onlyWhenIdle;

        public FadeToRoom(PlayerId id, RoomId room, bool onlyWhenIdle = false) : base(id)
        {
            this.room = room;
            this.onlyWhenIdle = onlyWhenIdle;
        }

        internal override bool Apply(RoomFlow flow) => flow.FadeTo(room, onlyWhenIdle);
    }

    /// <summary>
    /// <c>fade.mode = "fadein"; fade.firstPass = true;</c> with next_room unchanged: in a play room obj_fade_game then
    /// asks for the next level (<see cref="LevelAdvanceRequested"/>). Refused in a room with no fade.
    /// </summary>
    public sealed class StartFadeIn : FlowCommand
    {
        readonly bool onlyWhenIdle;

        public StartFadeIn(PlayerId id, bool onlyWhenIdle = false) : base(id) => this.onlyWhenIdle = onlyWhenIdle;

        internal override bool Apply(RoomFlow flow) => flow.FadeIn(onlyWhenIdle);
    }

    /// <summary>
    /// Esc pressed: obj_map, obj_story, obj_intro and obj_game run <c>room_goto(room_title)</c>; refused in the rooms
    /// whose controller has no Esc event (startup, the title, the garage).
    /// </summary>
    public sealed class PressEscape : FlowCommand
    {
        public PressEscape(PlayerId id) : base(id) { }

        internal override bool Apply(RoomFlow flow) => flow.Escape();
    }

    /// <summary>
    /// F1 pressed: obj_title and obj_game toggle fullscreen (<see cref="FullscreenToggled"/>); refused in the rooms
    /// whose controller has no F1 event.
    /// </summary>
    public sealed class PressFullscreen : FlowCommand
    {
        public PressFullscreen(PlayerId id) : base(id) { }

        internal override bool Apply(RoomFlow flow) => flow.ToggleFullscreen();
    }

    /// <summary>One step of the current room at its room_speed: the fade's Step event.</summary>
    public sealed class StepRoom : FlowCommand
    {
        public StepRoom(PlayerId id) : base(id) { }

        internal override bool Apply(RoomFlow flow) => flow.Step();
    }
}
