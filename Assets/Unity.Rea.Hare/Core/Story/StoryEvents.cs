using Unity.Rea.Hare.Flow;

namespace Unity.Rea.Hare.Story
{
    /// <summary>Something that happened in room_story, returned by every <see cref="StoryCommand"/>.</summary>
    public abstract class StoryEvent
    {
        /// <summary>The seat whose command raised it.</summary>
        public PlayerId Id { get; internal set; }
    }

    /// <summary>obj_typewriter_story typed a sounding character: <c>audio_stop_sound(</c><see cref="Sound"/><c>); audio_play_sound(</c><see cref="Sound"/><c>,1,0)</c>.</summary>
    public sealed class StoryTypeSounded : StoryEvent { public string Sound; }

    /// <summary><c>gamemode = gm_campaign; current_scenario = -1; current_level = 1;</c></summary>
    public sealed class StoryCampaignChosen : StoryEvent { }

    /// <summary><c>next_room = </c><see cref="Room"/><c>; obj_fade_story.mode = "fadein"; obj_fade_story.firstPass = true;</c></summary>
    public sealed class StoryFadeRequested : StoryEvent { public RoomId Room; }
}
