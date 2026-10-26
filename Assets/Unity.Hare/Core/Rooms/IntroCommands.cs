namespace Unity.Hare.Rooms
{
    /// <summary>
    /// The only way the intro changes. A command carries the id of the seat it acts for (no global current player) and
    /// runs in <see cref="IntroScreen.Execute"/>, which returns whether it took effect and what it raised.
    /// </summary>
    public abstract class IntroCommand
    {
        protected IntroCommand(PlayerId id) => Id = id;

        public PlayerId Id { get; }

        /// <summary>Does the work; false when the command was refused (the rules said no).</summary>
        internal abstract bool Apply(IntroScreen screen);
    }

    /// <summary>The room comes up: obj_drop_intro, obj_drop_emitter and obj_intro at their places. Once only.</summary>
    public sealed class CreateIntro : IntroCommand
    {
        public CreateIntro(PlayerId id) : base(id) { }
        internal override bool Apply(IntroScreen screen) => screen.Create();
    }

    /// <summary>
    /// One step at the room's room_speed, with A and D as held (obj_drop_intro's keyboard events, which move the line it
    /// shakes about): the drop, its clouds and the backgrounds. Refused before Create.
    /// </summary>
    public sealed class StepIntro : IntroCommand
    {
        readonly bool left, right;

        public StepIntro(PlayerId id, bool left = false, bool right = false) : base(id)
        {
            this.left = left;
            this.right = right;
        }

        internal override bool Apply(IntroScreen screen) => screen.Step(left, right);
    }

    /// <summary>
    /// obj_drop_intro's key press for any key: unless A or D is held (<c>aHeld</c>, <c>dHeld</c>), a hanging rabbit
    /// falls out of the room (<c>phase = "exit"</c>). Refused otherwise.
    /// </summary>
    public sealed class PressIntroKey : IntroCommand
    {
        readonly bool aHeld, dHeld;

        public PressIntroKey(PlayerId id, bool aHeld = false, bool dHeld = false) : base(id)
        {
            this.aHeld = aHeld;
            this.dHeld = dHeld;
        }

        internal override bool Apply(IntroScreen screen) => screen.PressKey(aHeld, dHeld);
    }
}
