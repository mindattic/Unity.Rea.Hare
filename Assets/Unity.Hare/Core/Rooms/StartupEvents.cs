using Unity.Hare.Flow;

namespace Unity.Hare.Rooms
{
    /// <summary>Something that happened in the startup room, returned by every <see cref="StartupCommand"/>.</summary>
    public abstract class StartupEvent
    {
        /// <summary>The seat whose command raised it.</summary>
        public PlayerId Id { get; internal set; }
    }

    /// <summary>Create's <c>window_set_fullscreen(true)</c>.</summary>
    public sealed class FullscreenRequested : StartupEvent { }

    /// <summary>An alarm re-rolled background <see cref="Layer"/>'s speeds and armed itself for <see cref="NextShiftSteps"/> more steps.</summary>
    public sealed class DriftChanged : StartupEvent
    {
        public int Layer;
        public int HSpeed;
        public int VSpeed;
        public int NextShiftSteps;
    }

    /// <summary>A key or the mouse: <c>room_goto(</c><see cref="Room"/><c>)</c>, which the room flow carries out.</summary>
    public sealed class ContinueRequested : StartupEvent { public RoomId Room; }
}
