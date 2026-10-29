namespace Unity.Hare.Rooms
{
    /// <summary>
    /// The only way the startup room changes. A command carries the id of the seat it acts for (no global current
    /// player) and runs in <see cref="StartupScreen.Execute"/>, which returns whether it took effect and what it raised.
    /// </summary>
    public abstract class StartupCommand
    {
        protected StartupCommand(PlayerId id) => Id = id;

        public PlayerId Id { get; }

        /// <summary>Does the work; false when the command was refused (the rules said no).</summary>
        internal abstract bool Apply(StartupScreen screen);
    }

    /// <summary>obj_startup's Create event: fullscreen (<see cref="FullscreenRequested"/>) and the three alarms armed. Once only.</summary>
    public sealed class CreateStartup : StartupCommand
    {
        public CreateStartup(PlayerId id) : base(id) { }

        internal override bool Apply(StartupScreen screen) => screen.Create();
    }

    /// <summary>One step at the room's room_speed: the alarms (<see cref="DriftChanged"/>) and the backgrounds' scroll. Refused before Create.</summary>
    public sealed class StepStartup : StartupCommand
    {
        public StepStartup(PlayerId id) : base(id) { }

        internal override bool Apply(StartupScreen screen) => screen.Step();
    }

    /// <summary>
    /// The key-press event for any key (vk_anykey, which F1 is one of), or the global left button event while the
    /// button is held: <c>room_goto(room_title)</c> (<see cref="ContinueRequested"/>). Refused once already asked.
    /// </summary>
    public sealed class ContinueFromStartup : StartupCommand
    {
        public ContinueFromStartup(PlayerId id) : base(id) { }

        internal override bool Apply(StartupScreen screen) => screen.Continue();
    }
}
