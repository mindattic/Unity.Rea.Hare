using Unity.Rea.Hare.Flow;

namespace Unity.Rea.Hare.Rooms
{
    /// <summary>Something that happened on the title, returned by every <see cref="TitleCommand"/>.</summary>
    public abstract class TitleEvent
    {
        /// <summary>The seat whose command raised it.</summary>
        public PlayerId Id { get; internal set; }
    }

    /// <summary>
    /// <c>obj_stand_title.phase = "on"</c>: obj_title's key or click, or obj_drop_title falling out of the room. The
    /// rabbit rises and obj_logo follows (<see cref="MenuShown"/>).
    /// </summary>
    public sealed class MenuCalled : TitleEvent { }

    /// <summary>obj_logo's Create: the menu's buttons exist, <c>obj_mouse.visible = true</c> and the night sky stops.</summary>
    public sealed class MenuShown : TitleEvent { }

    /// <summary>obj_drop_title was destroyed (a key or click, or it fell out of the room).</summary>
    public sealed class TitleDropRemoved : TitleEvent { }

    /// <summary>
    /// The drop's <c>effect_create_below(ef_smokeup, x, y+40, </c><see cref="Size"/><c>, </c><see cref="Color"/><c>)</c>
    /// at room pixel (<see cref="X"/>, <see cref="Y"/>): the orange trail every step or Alarm 0's black puff.
    /// </summary>
    public sealed class TitleSmokePuffed : TitleEvent
    {
        public DropSmoke Smoke;
        public double X;
        public double Y;
        public int Size;
        /// <summary>#RRGGBB.</summary>
        public string Color;
    }

    /// <summary><c>audio_play_sound(</c><see cref="Sound"/><c>, ...)</c>.</summary>
    public sealed class TitleSoundRequested : TitleEvent { public string Sound; }

    /// <summary>A button's mouse enter (<see cref="Hovered"/> true) or mouse leave event: its colour and hover message.</summary>
    public sealed class TitleHoverChanged : TitleEvent { public TitleButton Button; public bool Hovered; }

    /// <summary>A button set the game-mode globals (<c>gamemode</c>, and the level or difficulty with it).</summary>
    public sealed class TitleModeChosen : TitleEvent { public TitleMode Mode; public int Level; public int Difficulty; }

    /// <summary>
    /// <c>next_room = </c><see cref="Room"/><c>; obj_fade_title.mode = "fadein"; obj_fade_title.firstPass = true;</c>,
    /// which the room flow carries out (<see cref="FadeToRoom"/>).
    /// </summary>
    public sealed class TitleFadeRequested : TitleEvent { public RoomId Room; public bool OnlyWhenIdle; }

    /// <summary>An obj_yesno_dialog was made (<see cref="Dialog"/>), with <c>mouse.on_gui = true</c>.</summary>
    public sealed class TitleDialogOpened : TitleEvent { public TitleDialog Dialog; }

    /// <summary>The dialog and its Yes and No were destroyed.</summary>
    public sealed class TitleDialogClosed : TitleEvent { }

    /// <summary><c>mouse.on_gui = </c><see cref="OnGui"/>.</summary>
    public sealed class TitlePointerOnGuiChanged : TitleEvent { public bool OnGui; }

    /// <summary><c>game_end()</c>.</summary>
    public sealed class QuitRequested : TitleEvent { }

    /// <summary><c>room_goto(</c><see cref="Room"/><c>)</c> (a "mainmenu" dialog's Yes).</summary>
    public sealed class TitleRoomRequested : TitleEvent { public RoomId Room; }
}
