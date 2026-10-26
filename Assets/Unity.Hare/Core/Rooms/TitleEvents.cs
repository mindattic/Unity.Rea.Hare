using Unity.Hare.Flow;

namespace Unity.Hare.Rooms
{
    /// <summary>Something that happened on the title, returned by every <see cref="TitleCommand"/>.</summary>
    public abstract class TitleEvent
    {
        /// <summary>The seat whose command raised it.</summary>
        public PlayerId Id { get; internal set; }
    }

    /// <summary>obj_title's key or click set <c>obj_stand_title.phase = "on"</c>.</summary>
    public sealed class MenuCalled : TitleEvent { }

    /// <summary>obj_logo's Create: the menu's buttons exist and <c>obj_mouse.visible = true</c>.</summary>
    public sealed class MenuShown : TitleEvent { }

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
