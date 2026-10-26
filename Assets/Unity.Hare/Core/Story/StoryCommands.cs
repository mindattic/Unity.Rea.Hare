namespace Unity.Hare.Story
{
    /// <summary>
    /// The only way the story room changes. A command carries the id of the seat it acts for (no global current player)
    /// and runs in <see cref="StoryScreen.Execute"/>, which returns whether it took effect and what it raised.
    /// </summary>
    public abstract class StoryCommand
    {
        protected StoryCommand(PlayerId id) => Id = id;

        public PlayerId Id { get; }

        /// <summary>Does the work; false when the command was refused (the rules said no).</summary>
        internal abstract bool Apply(StoryScreen screen);
    }

    /// <summary>The room comes up: obj_scroll_world, obj_story with its typewriter, at their places. Once only.</summary>
    public sealed class CreateStory : StoryCommand
    {
        public CreateStory(PlayerId id) : base(id) { }
        internal override bool Apply(StoryScreen screen) => screen.Create();
    }

    /// <summary>One step at the room's room_speed: the scroll, the typing and the earth's turn. Refused before Create.</summary>
    public sealed class StepStory : StoryCommand
    {
        public StepStory(PlayerId id) : base(id) { }
        internal override bool Apply(StoryScreen screen) => screen.Step();
    }

    /// <summary>
    /// obj_story's key press for any key: unless Esc is held (<c>escapeHeld</c>), the campaign and the fade to room_map
    /// (<see cref="StoryCampaignChosen"/>, <see cref="StoryFadeRequested"/>), on every press. Refused for Esc.
    /// </summary>
    public sealed class PressStoryKey : StoryCommand
    {
        readonly bool escapeHeld;

        public PressStoryKey(PlayerId id, bool escapeHeld = false) : base(id) => this.escapeHeld = escapeHeld;

        internal override bool Apply(StoryScreen screen) => screen.PressKey(escapeHeld);
    }
}
