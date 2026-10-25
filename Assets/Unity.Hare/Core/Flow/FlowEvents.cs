namespace Unity.Hare.Flow
{
    /// <summary>
    /// Something that happened to the room flow, returned by every <see cref="FlowCommand"/>. Presentation plays these
    /// (loads a scene, toggles fullscreen) and never writes back.
    /// </summary>
    public abstract class FlowEvent
    {
        /// <summary>The seat whose command raised it.</summary>
        public PlayerId Id { get; internal set; }
    }

    /// <summary>room_goto: the game left <see cref="From"/> (null for the first room) for <see cref="To"/>.</summary>
    public sealed class RoomChanged : FlowEvent { public RoomId? From; public RoomId To; }

    /// <summary>The room came up again outside the flow (room_restart); its fade starts over.</summary>
    public sealed class RoomEntered : FlowEvent { public RoomId Room; }

    /// <summary>The room's fade started going to black, bound for <see cref="NextRoom"/>.</summary>
    public sealed class FadeInStarted : FlowEvent { public RoomId NextRoom; }

    /// <summary>F1: <c>window_set_fullscreen(!window_get_fullscreen())</c>.</summary>
    public sealed class FullscreenToggled : FlowEvent { }

    /// <summary>
    /// obj_fade_game's fade-in reached black: <c>current_level += 1; setup_level();</c> (the level setup, U.HARE-T57,
    /// plays it).
    /// </summary>
    public sealed class LevelAdvanceRequested : FlowEvent { }
}
