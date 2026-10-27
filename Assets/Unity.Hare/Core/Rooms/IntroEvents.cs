using Unity.Hare.Flow;

namespace Unity.Hare.Rooms
{
    /// <summary>Something that happened in room_intro, returned by every <see cref="IntroCommand"/>.</summary>
    public abstract class IntroEvent
    {
        /// <summary>The seat whose command raised it.</summary>
        public PlayerId Id { get; internal set; }
    }

    /// <summary>
    /// The drop's <c>effect_create_below(ef_smokeup, x, y+40, </c><see cref="Size"/><c>, </c><see cref="Color"/><c>)</c>
    /// at room pixel (<see cref="X"/>, <see cref="Y"/>).
    /// </summary>
    public sealed class IntroSmokePuffed : IntroEvent
    {
        public DropSmoke Smoke;
        public double X;
        public double Y;
        public int Size;
        /// <summary>#RRGGBB.</summary>
        public string Color;
    }

    /// <summary>The rabbit stopped: <c>tip = instance_create(0,0,obj_tip)</c> with the controls list.</summary>
    public sealed class IntroTipRequested : IntroEvent { }

    /// <summary>A key sent the hanging rabbit on: <c>phase = "exit"</c>.</summary>
    public sealed class IntroDropReleased : IntroEvent { }

    /// <summary><c>gamemode = gm_quickstart; current_scenario = -1; current_level = </c><see cref="Level"/>.</summary>
    public sealed class IntroModeChosen : IntroEvent { public int Level; }

    /// <summary>
    /// The map's mission is kept (U.HARE-T19, owner-directed, docs/BIBLE.md V42): the campaign plays
    /// <see cref="Scenario"/>'s <see cref="Level"/> instead of a quick game at level 1.
    /// </summary>
    public sealed class IntroMissionKept : IntroEvent { public int Scenario; public int Level; }

    /// <summary><c>room_goto(</c><see cref="Room"/><c>)</c>.</summary>
    public sealed class IntroRoomRequested : IntroEvent { public RoomId Room; }
}
