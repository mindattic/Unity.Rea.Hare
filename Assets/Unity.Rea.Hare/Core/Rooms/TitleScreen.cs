using System;
using System.Collections.Generic;

namespace Unity.Rea.Hare.Rooms
{
    /// <summary>One title button as it stands: where it is and whether the mouse is over it.</summary>
    public sealed class TitleButton
    {
        internal TitleButton(TitleButtonDefinition definition, double x, double y, bool inDialog)
        {
            Definition = definition;
            X = x;
            Y = y;
            InDialog = inDialog;
        }

        public TitleButtonDefinition Definition { get; }
        /// <summary>GML x, y: room pixels.</summary>
        public double X { get; }
        public double Y { get; }
        /// <summary>One of obj_yesno_dialog's Yes and No (drawn on the GUI layer at alpha 1), not one of obj_logo's.</summary>
        public bool InDialog { get; }
        /// <summary>GML mouseover: the caption is c_cornflowerblue and the hover message is drawn.</summary>
        public bool Hovered { get; internal set; }

        /// <summary>Whether room pixel (<paramref name="px"/>, <paramref name="py"/>) is inside its sprite's mask.</summary>
        public bool Contains(double px, double py) => Definition.Mask.Contains(X, Y, px, py);
    }

    /// <summary>obj_stand_title as it stands: the rabbit on the moon, below the room until it is called up.</summary>
    public sealed class TitleStand
    {
        internal TitleStand(StandTuning tuning)
        {
            Tuning = tuning;
            X = tuning.X;
            Y = tuning.Y;
        }

        public StandTuning Tuning { get; }
        /// <summary>GML x, y: spr_stand's origin, the middle of its bottom edge.</summary>
        public double X { get; }
        public double Y { get; internal set; }
        public double VSpeed { get; internal set; }
        /// <summary>GML <c>phase == "on"</c>: rising toward the room.</summary>
        public bool On { get; internal set; }

        /// <summary>Where its Draw puts spr_moon_surface: <c>(0, y + 250)</c>.</summary>
        public double MoonX => Tuning.MoonX;
        public double MoonY => Y + Tuning.MoonOffsetY;
        /// <summary>Where its Draw puts spr_eyeback: <c>(x - 6, y - 368)</c>.</summary>
        public double EyeX => X + Tuning.EyeOffsetX;
        public double EyeY => Y + Tuning.EyeOffsetY;

        /// <summary>Where its Draw puts spr_pupil with the mouse at room pixel (<paramref name="mouseX"/>, <paramref name="mouseY"/>).</summary>
        public void PupilAt(double mouseX, double mouseY, out double x, out double y)
        {
            x = X + Tuning.PupilOffsetX + mouseX / Tuning.PupilMouseXDivisor;
            y = Y + Tuning.PupilOffsetY + mouseY / Tuning.PupilMouseYDivisor;
        }
    }

    /// <summary>One obj_yesno_dialog: its action, text, message, top left and its Yes and No.</summary>
    public sealed class TitleDialog
    {
        internal TitleDialog(TitleDialogDefinition definition, TitleDialogPurpose purpose, string text, string message, double viewWidth, double viewHeight)
        {
            Definition = definition;
            Purpose = purpose;
            Text = text;
            Message = message;
            // x = view_xview[0] + view_wview[0]/2 - width/2; y = view_yview[0] + view_hview[0]/2 - height/2 (view 0 sits at 0, 0)
            X = viewWidth / 2d - definition.Width / 2d;
            Y = viewHeight / 2d - definition.Height / 2d;
            double wquarter = definition.Width / 4d, hquarter = definition.Height / 4d;
            Yes = new TitleButton(definition.Yes, X + wquarter, Y + hquarter * 3d, inDialog: true);
            No = new TitleButton(definition.No, X + wquarter * 3d, Y + hquarter * 3d, inDialog: true);
        }

        public TitleDialogDefinition Definition { get; }
        /// <summary>GML action.</summary>
        public TitleDialogPurpose Purpose { get; }
        /// <summary>GML text, drawn in fnt_score at (x + width/2, y + margin).</summary>
        public string Text { get; }
        /// <summary>GML message, drawn in fnt_point at (x + width/2, y + height/2).</summary>
        public string Message { get; }
        /// <summary>Its top left in GUI pixels (the title's view is the room, so room pixels too).</summary>
        public double X { get; }
        public double Y { get; }
        public TitleButton Yes { get; }
        public TitleButton No { get; }
    }

    /// <summary>
    /// The title, engine-free: the falling rabbit, its clouds and the backgrounds (<see cref="Scene"/>: obj_drop_title,
    /// obj_drop_emitter, obj_drop_cloud, bg_nightsky under bg_gradient3), obj_stand_title rising with the moon
    /// (<see cref="Stand"/>), obj_title's key and click, obj_logo and the buttons its Create makes (obj_btn_campaign,
    /// obj_btn_level01..08, obj_btn_diff100, obj_btn_diff0, obj_btn_quit; obj_btn_quickstart is ported but never placed),
    /// obj_logo's fade-in, and obj_yesno_dialog with obj_btn_yes and obj_btn_no. There is no global instance: the title
    /// room's view owns one. State changes only through <see cref="Execute"/>; presentation draws the scene, buttons and
    /// dialog and plays the events (sounds, smoke, the session's game mode, the room flow's fade, the quit) and never
    /// writes back.
    ///
    /// The menu comes in one of two ways, as in the original: a key or click destroys the drop and the emitter and turns
    /// obj_stand_title on, or the drop, left alone, hangs for Alarm 1's 200 steps and falls out of the room, which turns
    /// it on itself (with sfx_land). Either way the rabbit rises 40 px a step until it is within 30 px of the room's
    /// bottom, and the step after that obj_logo is made: 40 steps from below the room.
    ///
    /// GameMaker runs every instance's mouse events whatever is drawn over it, so the menu's buttons still answer while
    /// the dialog is up, as in the original; none of them overlaps the dialog's box.
    /// </summary>
    public sealed class TitleScreen
    {
        readonly List<TitleEvent> events = new List<TitleEvent>();
        readonly List<TitleButton> menu = new List<TitleButton>();
        readonly double viewWidth, viewHeight;
        PlayerId issuer;

        /// <param name="tuning">The title data (the registry's <c>title</c> section).</param>
        /// <param name="viewWidth">view_wview[0], the title's 1920 (the view is the room).</param>
        /// <param name="viewHeight">view_hview[0], the title's 1080.</param>
        /// <param name="random">Where the drop's shake and the clouds' irandom_range come from; null deals a fixed seed.</param>
        public TitleScreen(TitleTuning tuning, int viewWidth, int viewHeight, IRandomSource random = null)
        {
            Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            if (viewWidth <= 0) throw new ArgumentOutOfRangeException(nameof(viewWidth), viewWidth, "Must be positive.");
            if (viewHeight <= 0) throw new ArgumentOutOfRangeException(nameof(viewHeight), viewHeight, "Must be positive.");
            this.viewWidth = viewWidth;
            this.viewHeight = viewHeight;
            Scene = new DropScene(tuning.Scene, viewWidth, viewHeight, random ?? new SeededRandom(1));
            Stand = new TitleStand(tuning.Stand);
        }

        public TitleTuning Tuning { get; }

        /// <summary>The falling rabbit, its clouds and the backgrounds.</summary>
        public DropScene Scene { get; }

        /// <summary>obj_stand_title.</summary>
        public TitleStand Stand { get; }

        /// <summary>obj_logo's x, y (spr_logo's origin, its middle), once the menu is shown.</summary>
        public double LogoX => Tuning.Logo.Width / 2d + Tuning.Logo.Margin;
        public double LogoY => Tuning.Logo.Height / 2d + Tuning.Logo.Margin;

        /// <summary>Whether obj_title's Create has run.</summary>
        public bool Created { get; private set; }

        /// <summary>Whether obj_stand_title has been turned on (a key, a click or the drop falling out of the room).</summary>
        public bool MenuCalled { get; private set; }

        /// <summary>Whether obj_logo exists, and with it the buttons.</summary>
        public bool MenuShown { get; private set; }

        /// <summary>obj_logo's image_alpha, the alpha every menu caption is drawn at; 0 until the menu is shown.</summary>
        public double MenuAlpha { get; private set; }

        /// <summary>The buttons obj_logo made, in creation order; empty until the menu is shown.</summary>
        public IReadOnlyList<TitleButton> Menu => menu;

        /// <summary>The obj_yesno_dialog that is up, or null.</summary>
        public TitleDialog Dialog { get; private set; }

        /// <summary>obj_mouse.visible: obj_title makes it hidden and obj_logo shows it.</summary>
        public bool PointerVisible { get; private set; }

        /// <summary>obj_mouse.on_gui: set by the dialog's Create, cleared by its No.</summary>
        public bool PointerOnGui { get; private set; }

        /// <summary>Every button that exists now: the menu's, then the dialog's Yes and No.</summary>
        public IEnumerable<TitleButton> Buttons
        {
            get
            {
                foreach (var button in menu) yield return button;
                if (Dialog != null)
                {
                    yield return Dialog.Yes;
                    yield return Dialog.No;
                }
            }
        }

        /// <summary>The definition of the button whose GML object is <paramref name="obj"/>, or null.</summary>
        public TitleButtonDefinition Find(string obj)
        {
            foreach (var definition in Tuning.Buttons)
                if (definition != null && definition.Object == obj) return definition;
            return null;
        }

        /// <summary>Runs <paramref name="command"/> and returns whether it took effect and what it raised.</summary>
        public TitleResult Execute(TitleCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            events.Clear();
            issuer = command.Id;
            bool accepted = command.Apply(this);
            var raised = events.Count == 0 ? Array.Empty<TitleEvent>() : events.ToArray();
            events.Clear();
            return new TitleResult(accepted, raised);
        }

        void Emit(TitleEvent e)
        {
            e.Id = issuer;
            events.Add(e);
        }

        // ---- Rules the commands call ----

        /// <summary>
        /// obj_title's Create (<c>mouse.sprite_index = spr_pointer; mouse.visible = false;</c>) with the room's other
        /// instances: obj_drop_emitter, obj_drop_title and obj_stand_title (phase "off") at their places.
        /// </summary>
        internal bool Create()
        {
            if (Created) return false;
            Created = true;
            PointerVisible = false;
            Scene.Create();
            Stand.Y = Stand.Tuning.Y;
            Stand.VSpeed = 0d;
            Stand.On = false;
            return true;
        }

        /// <summary>
        /// obj_title's any-key and left-button events: <c>with(obj_drop_title) instance_destroy(); with(obj_drop_emitter)
        /// instance_destroy(); obj_stand_title.phase = "on";</c>. Refused while the rabbit is already rising, and once the
        /// menu is up: in the original a second call makes a second obj_logo and a second menu (BIBLE D23).
        /// </summary>
        internal bool CallMenu()
        {
            if (!Created || MenuShown || Stand.On) return false;
            bool dropGone = Scene.DestroyDrop();
            Scene.DestroyEmitter();
            if (dropGone) Emit(new TitleDropRemoved());
            TurnStandOn();
            return true;
        }

        void TurnStandOn()
        {
            Stand.On = true;
            MenuCalled = true;
            Emit(new MenuCalled());
        }

        /// <summary>
        /// obj_logo's Create: <c>image_alpha = 0; obj_mouse.visible = true; background_vspeed[0] = 0;</c> and an
        /// instance_create for each placed button at its x, y. obj_stand_title's Step makes it once the rabbit has risen;
        /// the command runs it at once. Once only (BIBLE D23).
        /// </summary>
        internal bool ShowMenu()
        {
            if (!Created || MenuShown) return false;
            MenuShown = true;
            MenuAlpha = 0d;
            PointerVisible = true;
            Scene.StopBackground(Tuning.Logo.StopsBackground);
            foreach (var definition in Tuning.Buttons)
                if (definition != null && definition.Placed) menu.Add(new TitleButton(definition, definition.X, definition.Y, inDialog: false));
            Emit(new MenuShown());
            return true;
        }

        /// <summary>
        /// One step at the room's room_speed, in GameMaker's order: the drop scene's alarms, the drop's Step (its smoke;
        /// below the room it is destroyed with the emitter, obj_stand_title is turned on and sfx_land plays), the
        /// clouds' Steps, obj_stand_title's Step (rising, or making obj_logo), obj_logo's Step
        /// (<c>if(image_alpha &lt; 1) image_alpha += 0.02; else image_alpha = 1;</c>; not in the step that makes it),
        /// then the motion and the backgrounds' scroll.
        /// </summary>
        internal bool Step()
        {
            if (!Created) return false;
            bool logoExisted = MenuShown;
            var step = Scene.Step((smoke, x, y) => Emit(new TitleSmokePuffed
            {
                Smoke = smoke,
                X = x,
                Y = y,
                Size = Scene.Tuning.SmokeSize,
                Color = smoke == DropSmoke.Trail ? Scene.Tuning.TrailColor : Scene.Tuning.PuffColor,
            }));
            if (step.Below)
            {
                // instance_destroy(); obj_stand_title.phase = "on"; with(obj_drop_emitter) instance_destroy(); audio_play_sound(sfx_land,1,0);
                Scene.DestroyDrop();
                Emit(new TitleDropRemoved());
                if (!MenuShown && !Stand.On) TurnStandOn();
                Scene.DestroyEmitter();
                PlaySound(Scene.Tuning.LandSound);
            }

            if (Stand.On)
            {
                if (Stand.Y > viewHeight - Stand.Tuning.StopAboveBottom) Stand.VSpeed = -Stand.Tuning.RiseSpeed;
                else
                {
                    Stand.VSpeed = 0d;
                    Stand.On = false;
                    ShowMenu();
                }
            }
            Stand.Y += Stand.VSpeed;

            if (logoExisted)
            {
                if (MenuAlpha < 1d) MenuAlpha += Tuning.MenuFadeSpeed;
                else MenuAlpha = 1d;
            }
            return true;
        }

        /// <summary>Every button's mouse enter (<c>mouseover = true; audio_play_sound(sfx_hover,2,0); color = c_cornflowerblue;</c>) and leave.</summary>
        internal bool Point(double x, double y)
        {
            if (!Created) return false;
            foreach (var button in Snapshot())
            {
                bool over = button.Contains(x, y);
                if (over == button.Hovered) continue;
                button.Hovered = over;
                if (over) Emit(new TitleSoundRequested { Sound = Tuning.HoverSound });
                Emit(new TitleHoverChanged { Button = button, Hovered = over });
            }
            return true;
        }

        /// <summary>The left pressed event of every button under (<paramref name="x"/>, <paramref name="y"/>), in instance order.</summary>
        internal bool Press(double x, double y, bool fadeIdle)
        {
            if (!Created) return false;
            bool hit = false;
            foreach (var button in Snapshot())
            {
                if (!button.Contains(x, y)) continue;
                hit = true;
                Click(button, fadeIdle);
            }
            return hit;
        }

        /// <summary>The buttons as they are before an event runs, so one that opens or closes the dialog changes nothing mid-loop.</summary>
        List<TitleButton> Snapshot() => new List<TitleButton>(Buttons);

        void Click(TitleButton button, bool fadeIdle)
        {
            var d = button.Definition;
            switch (d.Action)
            {
                case TitleAction.FadeToRoom:
                    if (d.OnlyWhenIdle && !fadeIdle) return; // if(obj_fade_title.mode == "off") { ... }
                    PlaySound(d.Sound);
                    if (d.Mode != TitleMode.Unchanged) Emit(new TitleModeChosen { Mode = d.Mode, Level = d.Level, Difficulty = d.Difficulty });
                    Emit(new TitleFadeRequested { Room = d.Room, OnlyWhenIdle = d.OnlyWhenIdle });
                    return;

                case TitleAction.OpenDialog:
                    if (Dialog != null) return; // if(!instance_exists(obj_yesno_dialog))
                    PlaySound(d.Sound);
                    OpenDialog(d.DialogPurpose, d.DialogText, d.DialogMessage);
                    return;

                case TitleAction.DialogYes:
                    PlaySound(d.Sound);
                    if (Dialog == null) return;
                    if (Dialog.Purpose == TitleDialogPurpose.QuitGame) Emit(new QuitRequested());
                    else Emit(new TitleRoomRequested { Room = Flow.RoomId.Title });
                    return;

                case TitleAction.DialogNo:
                    PlaySound(d.Sound);
                    CloseDialog();
                    if (PointerOnGui) SetPointerOnGui(false); // if(mouse.on_gui) { mouse.on_gui = false; }
                    return;
            }
        }

        /// <summary>
        /// obj_yesno_dialog's Create, with the creator's <c>action</c>, <c>text</c> and <c>message</c> set after it:
        /// the box in the view's middle, its Yes and No, <c>mouse.on_gui = true</c>.
        /// </summary>
        void OpenDialog(TitleDialogPurpose purpose, string text, string message)
        {
            Dialog = new TitleDialog(Tuning.Dialog, purpose, text, message, viewWidth, viewHeight);
            Emit(new TitleDialogOpened { Dialog = Dialog });
            SetPointerOnGui(true);
        }

        void CloseDialog()
        {
            if (Dialog == null) return;
            Dialog = null;
            Emit(new TitleDialogClosed());
        }

        void SetPointerOnGui(bool onGui)
        {
            PointerOnGui = onGui;
            Emit(new TitlePointerOnGuiChanged { OnGui = onGui });
        }

        void PlaySound(string sound)
        {
            if (!string.IsNullOrEmpty(sound)) Emit(new TitleSoundRequested { Sound = sound });
        }

        /// <summary>
        /// The dialog's Esc: <c>audio_play_sound(sfx_roit,1,0)</c> and every obj_btn_yes, obj_btn_no and obj_yesno_dialog
        /// destroyed. It leaves <c>mouse.on_gui</c> set, as the original does (on the title the view is the room, so
        /// the pointer is where it was either way).
        /// </summary>
        internal bool EscapeDialog()
        {
            if (Dialog == null) return false;
            PlaySound(Tuning.Dialog.EscapeSound);
            CloseDialog();
            return true;
        }

        /// <summary>The dialog's Enter: <c>game_end()</c>, even for a "mainmenu" dialog.</summary>
        internal bool ConfirmDialog()
        {
            if (Dialog == null) return false;
            Emit(new QuitRequested());
            return true;
        }
    }

    /// <summary>What <see cref="TitleScreen.Execute"/> returns: whether the command took effect and what it raised.</summary>
    public readonly struct TitleResult
    {
        public TitleResult(bool accepted, IReadOnlyList<TitleEvent> events)
        {
            Accepted = accepted;
            Events = events;
        }

        public bool Accepted { get; }
        public IReadOnlyList<TitleEvent> Events { get; }
    }
}
