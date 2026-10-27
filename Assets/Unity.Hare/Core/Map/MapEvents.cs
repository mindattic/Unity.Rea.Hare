using Unity.Hare.Flow;

namespace Unity.Hare.Map
{
    /// <summary>Something that happened in room_map, returned by every <see cref="MapCommand"/>.</summary>
    public abstract class MapEvent
    {
        /// <summary>The seat whose command raised it.</summary>
        public PlayerId Id { get; internal set; }
    }

    /// <summary><c>audio_play_sound(</c><see cref="Sound"/><c>, ...)</c>: sfx_flick, sfx_takeoff, sfx_hover, sfx_click.</summary>
    public sealed class MapSoundRequested : MapEvent { public string Sound; }

    /// <summary>obj_map's Create: <c>tip = instance_create(0,0,obj_tip)</c> with "Mission Command".</summary>
    public sealed class MapTipRequested : MapEvent { }

    /// <summary>
    /// A mission marker's Alarm 0 while the map idles: <c>effect_create_above(ef_ring, x, y, </c><see cref="Size"/><c>,
    /// </c><see cref="Color"/><c>)</c> at room pixel (<see cref="X"/>, <see cref="Y"/>).
    /// </summary>
    public sealed class MapMarkerPinged : MapEvent
    {
        public int Mission;
        public double X;
        public double Y;
        public int Size;
        /// <summary>#RRGGBB.</summary>
        public string Color;
    }

    /// <summary>
    /// <c>current_scenario</c> changed: obj_destination cleared it (-1) while the earth turns, or set it on reaching a
    /// mission. The session's <c>SelectScenario</c> follows it.
    /// </summary>
    public sealed class MapScenarioSelected : MapEvent { public int Scenario; }

    /// <summary>
    /// obj_destination touched a mission it had not last stopped on: the map idles, the sky stops, the panel shows it
    /// and the comm box slides in. <see cref="Locked"/> is the boss's lock (owner-directed).
    /// </summary>
    public sealed class MapMissionReached : MapEvent
    {
        public int Mission;
        public int Scenario;
        public int Level;
        public bool Locked;
    }

    /// <summary>The comm box was told to slide in (<see cref="In"/>) or out.</summary>
    public sealed class MapConversationSlid : MapEvent { public bool In; }

    /// <summary>obj_white_dot left the ship for obj_destination.</summary>
    public sealed class MapLaunched : MapEvent { public int Scenario; public int Level; }

    /// <summary>A launch was refused because the reached mission is the locked boss's (owner-directed).</summary>
    public sealed class MapLaunchRefused : MapEvent { public int Scenario; }

    /// <summary>
    /// The white dot arrived: the campaign plays <see cref="Scenario"/>'s <see cref="Level"/> (the session's
    /// <c>ChooseMission</c>; the original discarded it, docs/BIBLE.md V42).
    /// </summary>
    public sealed class MapMissionChosen : MapEvent { public int Scenario; public int Level; }

    /// <summary><c>room_goto(</c><see cref="Room"/><c>)</c>.</summary>
    public sealed class MapRoomRequested : MapEvent { public RoomId Room; }

    /// <summary><c>next_room = </c><see cref="Room"/><c>; fade.mode = "fadein"; fade.firstPass = true</c>.</summary>
    public sealed class MapFadeRequested : MapEvent { public RoomId Room; }

    /// <summary>obj_btn_upgrade's mouseover changed (its message is drawn while it is on).</summary>
    public sealed class MapHoverChanged : MapEvent { public bool Hovered; }
}
