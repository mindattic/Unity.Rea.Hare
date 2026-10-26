namespace Unity.Hare.Rooms
{
    /// <summary>
    /// The only way the title changes. A command carries the id of the seat it acts for (no global current player)
    /// and runs in <see cref="TitleScreen.Execute"/>, which returns whether it took effect and what it raised.
    /// </summary>
    public abstract class TitleCommand
    {
        protected TitleCommand(PlayerId id) => Id = id;

        public PlayerId Id { get; }

        /// <summary>Does the work; false when the command was refused (the rules said no).</summary>
        internal abstract bool Apply(TitleScreen screen);
    }

    /// <summary>obj_title's Create (its obj_fade_title and obj_mouse are the room flow's and the view's). Once only.</summary>
    public sealed class CreateTitle : TitleCommand
    {
        public CreateTitle(PlayerId id) : base(id) { }
        internal override bool Apply(TitleScreen screen) => screen.Create();
    }

    /// <summary>One step at the room's room_speed: obj_logo's Step (the captions' alpha). Refused before Create.</summary>
    public sealed class StepTitle : TitleCommand
    {
        public StepTitle(PlayerId id) : base(id) { }
        internal override bool Apply(TitleScreen screen) => screen.Step();
    }

    /// <summary>
    /// obj_title's key press for any key but F1, and its global left button while neither F1 nor Esc is held:
    /// <c>obj_stand_title.phase = "on"</c>, which brings in obj_logo and with it the menu (<see cref="MenuShown"/>).
    /// Refused once the menu is up.
    /// </summary>
    public sealed class CallTitleMenu : TitleCommand
    {
        public CallTitleMenu(PlayerId id) : base(id) { }
        internal override bool Apply(TitleScreen screen) => screen.CallMenu();
    }

    /// <summary>obj_logo's Create: the buttons made, <c>obj_mouse.visible = true</c>, the captions at alpha 0. Once only.</summary>
    public sealed class ShowTitleMenu : TitleCommand
    {
        public ShowTitleMenu(PlayerId id) : base(id) { }
        internal override bool Apply(TitleScreen screen) => screen.ShowMenu();
    }

    /// <summary>
    /// The mouse is at room pixel (<c>x</c>, <c>y</c>): every button's mouse enter and mouse leave events
    /// (<see cref="TitleHoverChanged"/>, sfx_hover on enter).
    /// </summary>
    public sealed class PointTitle : TitleCommand
    {
        readonly double x, y;

        public PointTitle(PlayerId id, double x, double y) : base(id)
        {
            this.x = x;
            this.y = y;
        }

        internal override bool Apply(TitleScreen screen) => screen.Point(x, y);
    }

    /// <summary>
    /// The left button pressed at room pixel (<c>x</c>, <c>y</c>): the left pressed event of every button under it.
    /// <c>fadeIdle</c> is <c>obj_fade_title.mode == "off"</c>, which most of them check first. Refused when no button is hit.
    /// </summary>
    public sealed class PressTitle : TitleCommand
    {
        readonly double x, y;
        readonly bool fadeIdle;

        public PressTitle(PlayerId id, double x, double y, bool fadeIdle) : base(id)
        {
            this.x = x;
            this.y = y;
            this.fadeIdle = fadeIdle;
        }

        internal override bool Apply(TitleScreen screen) => screen.Press(x, y, fadeIdle);
    }

    /// <summary>obj_yesno_dialog's Esc key press: sfx_roit, the dialog and its buttons gone. Refused with no dialog up.</summary>
    public sealed class EscapeTitleDialog : TitleCommand
    {
        public EscapeTitleDialog(PlayerId id) : base(id) { }
        internal override bool Apply(TitleScreen screen) => screen.EscapeDialog();
    }

    /// <summary>obj_yesno_dialog's Enter key press: <c>game_end()</c>, whatever its action. Refused with no dialog up.</summary>
    public sealed class ConfirmTitleDialog : TitleCommand
    {
        public ConfirmTitleDialog(PlayerId id) : base(id) { }
        internal override bool Apply(TitleScreen screen) => screen.ConfirmDialog();
    }
}
