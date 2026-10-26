using System;
using System.Collections.Generic;
using Unity.Hare.Content;
using Unity.Hare.Flow;

namespace Unity.Hare.Rooms
{
    /// <summary>What a title button's left pressed event does.</summary>
    public enum TitleAction
    {
        /// <summary>
        /// <c>next_room = Room; obj_fade_title.mode = "fadein"; obj_fade_title.firstPass = true;</c> after its sound and
        /// game mode (obj_btn_quickstart, obj_btn_campaign, obj_btn_level01..08, obj_btn_diff0/100).
        /// </summary>
        FadeToRoom,
        /// <summary>obj_btn_quit: <c>if(!instance_exists(obj_yesno_dialog))</c> make one with its action, text and message.</summary>
        OpenDialog,
        /// <summary>obj_btn_yes: sfx_roit, then the dialog's action (<c>game_end()</c> or <c>room_goto(room_title)</c>).</summary>
        DialogYes,
        /// <summary>obj_btn_no: sfx_roit, the dialog and its buttons destroyed, <c>mouse.on_gui = false</c>.</summary>
        DialogNo,
    }

    /// <summary>The globals a title button sets before its fade: <c>gamemode</c> (and with it the level or difficulty).</summary>
    public enum TitleMode
    {
        /// <summary>No game-mode lines (obj_btn_quickstart sets none).</summary>
        Unchanged,
        /// <summary><c>gamemode = gm_quickstart; current_scenario = -1; current_level = Level;</c> (obj_btn_level01..08).</summary>
        Quickstart,
        /// <summary><c>gamemode = gm_campaign; current_scenario = -1; current_level = 1;</c> (obj_btn_campaign).</summary>
        Campaign,
        /// <summary><c>difficulty = Difficulty; gamemode = gm_generator;</c> (obj_btn_diff0, obj_btn_diff100).</summary>
        Generator,
    }

    /// <summary>obj_yesno_dialog's <c>action</c>: what its Yes does.</summary>
    public enum TitleDialogPurpose
    {
        /// <summary><c>"quitgame"</c>: <c>game_end()</c>.</summary>
        QuitGame,
        /// <summary><c>"mainmenu"</c>: <c>room_goto(room_title)</c>.</summary>
        MainMenu,
    }

    /// <summary>
    /// A button's mouse mask, the sprite's bounding box around its origin in pixels (inclusive, as GameMaker's
    /// <c>bbox_*</c>): every title button's sprite is a rectangle mask (colkind 1) with an automatic full-image box.
    /// </summary>
    [Serializable]
    public class TitleMask
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public TitleMask() { }

        public TitleMask(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        /// <summary>The mask of a <paramref name="width"/> by <paramref name="height"/> sprite with its origin at (<paramref name="xorig"/>, <paramref name="yorig"/>).</summary>
        public static TitleMask Of(int width, int height, int xorig, int yorig) => new TitleMask(-xorig, -yorig, width - 1 - xorig, height - 1 - yorig);

        /// <summary>Whether room pixel (<paramref name="px"/>, <paramref name="py"/>) is over an instance at (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public bool Contains(double x, double y, double px, double py) =>
            px >= x + Left && px <= x + Right && py >= y + Top && py <= y + Bottom;

        internal void Validate(List<string> errors, string path)
        {
            if (Right < Left) errors.Add($"{path}.Right: must not be left of Left (was {Right}).");
            if (Bottom < Top) errors.Add($"{path}.Bottom: must not be above Top (was {Bottom}).");
        }
    }

    /// <summary>
    /// One of the title's buttons (obj_btn_*), as its Create, mouse and Draw events define it: no sprite is drawn
    /// (each Draw's <c>draw_self()</c> is commented out), only <see cref="Label"/> in outlined text, white or, while the
    /// mouse is over it, c_cornflowerblue, with its <see cref="Message"/> at the bottom of the room.
    /// </summary>
    [Serializable]
    public class TitleButtonDefinition
    {
        /// <summary>The GML object (obj_btn_campaign, ...).</summary>
        public string Object;
        /// <summary>The caption its Draw writes (obj_btn_quickstart's reads "Story Mode").</summary>
        public string Label;
        /// <summary>The caption in fnt_score (the big face) rather than fnt_point.</summary>
        public bool LargeLabel;
        /// <summary>The caption's top relative to the button's y: <c>y-30</c> in fnt_score, <c>y-12</c> in fnt_point, <c>y-24</c> on Yes/No.</summary>
        public int LabelOffset;
        /// <summary>Create's <c>message</c>, drawn while the mouse is over it.</summary>
        public string Message;
        /// <summary>Where obj_logo's Create puts it (<c>buttonX = room_width/2 + room_width/4</c>); unused for the dialog's two.</summary>
        public int X;
        public int Y;
        /// <summary>Whether obj_logo's Create makes it; obj_btn_quickstart's line is commented out, so it is never placed.</summary>
        public bool Placed = true;
        /// <summary>The object's sprite, whose mask its mouse events use (spr_button_area, spr_button_area_small, spr_yesno_button).</summary>
        public string MaskSprite;
        public TitleMask Mask = new TitleMask();
        /// <summary>What its left pressed event does.</summary>
        public TitleAction Action;
        /// <summary>The sound its left pressed event plays first (sfx_roit, or obj_btn_quickstart's sfx_click); none for obj_btn_quit.</summary>
        public string Sound;
        /// <summary>For <see cref="TitleAction.FadeToRoom"/>: <c>next_room</c>.</summary>
        public RoomId Room;
        /// <summary>For <see cref="TitleAction.FadeToRoom"/>: the press does nothing unless <c>obj_fade_title.mode == "off"</c>.</summary>
        public bool OnlyWhenIdle;
        /// <summary>For <see cref="TitleAction.FadeToRoom"/>: the game-mode globals it sets.</summary>
        public TitleMode Mode;
        /// <summary>For <see cref="TitleMode.Quickstart"/>: <c>current_level</c>.</summary>
        public int Level;
        /// <summary>For <see cref="TitleMode.Generator"/>: <c>difficulty</c>.</summary>
        public int Difficulty;
        /// <summary>For <see cref="TitleAction.OpenDialog"/>: the dialog's <c>action</c>, <c>text</c> and <c>message</c>.</summary>
        public TitleDialogPurpose DialogPurpose;
        public string DialogText;
        public string DialogMessage;

        internal void Validate(List<string> errors, string path, bool inDialog)
        {
            ValidationHelpers.Required(errors, path + ".Object", Object);
            ValidationHelpers.Required(errors, path + ".Label", Label);
            ValidationHelpers.Required(errors, path + ".Message", Message);
            ValidationHelpers.Required(errors, path + ".MaskSprite", MaskSprite);
            if (Mask == null) errors.Add(path + ".Mask: must not be null.");
            else Mask.Validate(errors, path + ".Mask");
            if (!Enum.IsDefined(typeof(TitleAction), Action)) errors.Add($"{path}.Action: not an action (was {(int)Action}).");
            bool dialogAction = Action == TitleAction.DialogYes || Action == TitleAction.DialogNo;
            if (dialogAction != inDialog)
                errors.Add($"{path}.Action: {Action} is {(inDialog ? "not a dialog button's" : "only the dialog's Yes and No")} action.");
            if (Action == TitleAction.FadeToRoom)
            {
                if (!Enum.IsDefined(typeof(RoomId), Room)) errors.Add($"{path}.Room: not a room (was {(int)Room}).");
                if (!Enum.IsDefined(typeof(TitleMode), Mode)) errors.Add($"{path}.Mode: not a game mode (was {(int)Mode}).");
                if (Mode == TitleMode.Quickstart) ValidationHelpers.Positive(errors, path + ".Level", Level);
                if (Mode == TitleMode.Generator) ValidationHelpers.NonNegative(errors, path + ".Difficulty", Difficulty);
            }
            if (Action == TitleAction.OpenDialog)
            {
                if (!Enum.IsDefined(typeof(TitleDialogPurpose), DialogPurpose)) errors.Add($"{path}.DialogPurpose: not a dialog action (was {(int)DialogPurpose}).");
                ValidationHelpers.Required(errors, path + ".DialogText", DialogText);
                ValidationHelpers.Required(errors, path + ".DialogMessage", DialogMessage);
            }
        }
    }

    /// <summary>
    /// obj_yesno_dialog (depth -100, drawn on the GUI layer): spr_tip_box at 0.7 alpha with its top left at the view's
    /// middle less half the box, <c>text</c> in fnt_score centred at <c>(x + width/2, y + margin)</c> and <c>message</c> in
    /// fnt_point centred at <c>(x + width/2, y + height/2)</c>, both with a size-1 outline; obj_btn_yes at
    /// <c>(x + width/4, y + height/4*3)</c> and obj_btn_no at <c>(x + width/4*3, y + height/4*3)</c>. Esc plays sfx_roit and
    /// destroys it; Enter runs <c>game_end()</c>.
    /// </summary>
    [Serializable]
    public class TitleDialogDefinition
    {
        public string Object = "obj_yesno_dialog";
        public string Sprite = "spr_tip_box";
        /// <summary><c>sprite_get_width(spr_tip_box)</c>, <c>sprite_get_height(spr_tip_box)</c>.</summary>
        public int Width = 548;
        public int Height = 250;
        /// <summary>Create's <c>alpha = 0.7</c>, the box's draw alpha.</summary>
        public float Alpha = 0.7f;
        /// <summary>Create's <c>margin = 20</c>: the title text's top below the box's.</summary>
        public int Margin = 20;
        /// <summary>The size argument of both of its draw_text_outline calls.</summary>
        public int Outline = 1;
        /// <summary>Its Esc event's sound.</summary>
        public string EscapeSound = "sfx_roit";

        public TitleButtonDefinition Yes = new TitleButtonDefinition
        {
            Object = "obj_btn_yes", Label = "Yes", LargeLabel = true, LabelOffset = -24, Message = "Quit the Game",
            MaskSprite = "spr_yesno_button", Mask = TitleMask.Of(145, 78, 72, 39), Action = TitleAction.DialogYes, Sound = "sfx_roit",
        };

        public TitleButtonDefinition No = new TitleButtonDefinition
        {
            Object = "obj_btn_no", Label = "No", LargeLabel = true, LabelOffset = -24, Message = "Return to Main Menu",
            MaskSprite = "spr_yesno_button", Mask = TitleMask.Of(145, 78, 72, 39), Action = TitleAction.DialogNo, Sound = "sfx_roit",
        };

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Object", Object);
            ValidationHelpers.Required(errors, path + ".Sprite", Sprite);
            ValidationHelpers.Positive(errors, path + ".Width", Width);
            ValidationHelpers.Positive(errors, path + ".Height", Height);
            if (!(Alpha >= 0f && Alpha <= 1f)) errors.Add($"{path}.Alpha: must be within 0..1 (was {Alpha}).");
            ValidationHelpers.NonNegative(errors, path + ".Margin", Margin);
            ValidationHelpers.NonNegative(errors, path + ".Outline", Outline);
            if (Yes == null) errors.Add(path + ".Yes: must not be null.");
            else
            {
                Yes.Validate(errors, path + ".Yes", inDialog: true);
                if (Yes.Action != TitleAction.DialogYes) errors.Add(path + ".Yes.Action: must be DialogYes.");
            }
            if (No == null) errors.Add(path + ".No: must not be null.");
            else
            {
                No.Validate(errors, path + ".No", inDialog: true);
                if (No.Action != TitleAction.DialogNo) errors.Add(path + ".No.Action: must be DialogNo.");
            }
        }
    }

    /// <summary>
    /// The title's menu as data (the content registry's <c>title</c> section, AGENTS.md rule 4): obj_btn_quickstart,
    /// obj_btn_campaign, obj_btn_level01..08, obj_btn_diff100, obj_btn_diff0 and obj_btn_quit as obj_logo's Create
    /// places them, obj_yesno_dialog with its Yes and No, and the numbers every one of them shares. The defaults are
    /// the original's, read from those objects and their sprites in HARE.gmx.
    /// </summary>
    [Serializable]
    public class TitleTuning
    {
        static TitleMask Area => TitleMask.Of(493, 69, 246, 34);       // spr_button_area, a fresh one per button
        static TitleMask AreaSmall => TitleMask.Of(210, 35, 105, 17);  // spr_button_area_small

        static TitleButtonDefinition LevelButton(int level, string label, string message, int y) => new TitleButtonDefinition
        {
            Object = "obj_btn_level0" + level, Label = label, LabelOffset = -12, Message = message, X = 1440, Y = y,
            MaskSprite = "spr_button_area_small", Mask = AreaSmall, Action = TitleAction.FadeToRoom, Sound = "sfx_roit",
            Room = RoomId.GameScreen, OnlyWhenIdle = true, Mode = TitleMode.Quickstart, Level = level,
        };

        /// <summary>
        /// The buttons in obj_logo's creation order (the order their events run in), then obj_btn_quickstart, which it
        /// never makes. <c>buttonX = room_width/2 + room_width/4</c> = 1440.
        /// </summary>
        public TitleButtonDefinition[] Buttons =
        {
            new TitleButtonDefinition
            {
                Object = "obj_btn_campaign", Label = "Campaign", LargeLabel = true, LabelOffset = -30, Message = "Play through campaign",
                X = 1440, Y = 150, MaskSprite = "spr_button_area", Mask = Area, Action = TitleAction.FadeToRoom, Sound = "sfx_roit",
                Room = RoomId.Story, OnlyWhenIdle = true, Mode = TitleMode.Campaign,
            },
            LevelButton(1, "Level 01 - Hydras & Saws", "Start a quick game", 250),
            LevelButton(2, "Level 02 - Meatballs & Phalanx", "Start a quick game", 300),
            LevelButton(3, "Level 03 - Sky Worms & Daggers", "Start a quick game", 350),
            LevelButton(4, "Level 04 - Storm Shaft", "Start a quick game", 400),
            LevelButton(5, "Level 05 - Tangerine Islands", "Start a quick game", 450),
            LevelButton(6, "Level 06 - Wraiths", "Start a quick game", 500),
            LevelButton(7, "Level 07 - Trilobytes", "Start a quick game", 550),
            LevelButton(8, "Boss 1 - Scrap Mental", "Fight a boss", 600),
            new TitleButtonDefinition
            {
                Object = "obj_btn_diff100", Label = "Enemy Generator", LabelOffset = -12, Message = "Enemy Generator", X = 1440, Y = 650,
                MaskSprite = "spr_button_area_small", Mask = AreaSmall, Action = TitleAction.FadeToRoom, Sound = "sfx_roit",
                Room = RoomId.GameScreen, OnlyWhenIdle = true, Mode = TitleMode.Generator, Difficulty = 100,
            },
            new TitleButtonDefinition
            {
                Object = "obj_btn_diff0", Label = "Empty Level", LabelOffset = -12, Message = "No enemies", X = 1440, Y = 700,
                MaskSprite = "spr_button_area_small", Mask = AreaSmall, Action = TitleAction.FadeToRoom, Sound = "sfx_roit",
                Room = RoomId.GameScreen, OnlyWhenIdle = true, Mode = TitleMode.Generator, Difficulty = 0,
            },
            new TitleButtonDefinition
            {
                Object = "obj_btn_quit", Label = "Quit Game", LargeLabel = true, LabelOffset = -30, Message = "Quit the game",
                X = 1440, Y = 800, MaskSprite = "spr_button_area", Mask = Area, Action = TitleAction.OpenDialog,
                DialogPurpose = TitleDialogPurpose.QuitGame, DialogText = "Quit Game", DialogMessage = "Are you sure?",
            },
            new TitleButtonDefinition
            {
                Object = "obj_btn_quickstart", Label = "Story Mode", LargeLabel = true, LabelOffset = -30, Message = "Start a quick game",
                Placed = false, MaskSprite = "spr_button_area", Mask = Area, Action = TitleAction.FadeToRoom, Sound = "sfx_click",
                Room = RoomId.Story, OnlyWhenIdle = false, Mode = TitleMode.Unchanged,
            },
        };

        public TitleDialogDefinition Dialog = new TitleDialogDefinition();

        /// <summary>obj_logo's Step: <c>image_alpha += 0.02</c> until 1, the alpha every menu caption is drawn at.</summary>
        public double MenuFadeSpeed = 0.02;
        /// <summary>Every button's mouse enter sound: <c>audio_play_sound(sfx_hover, 2, 0)</c>.</summary>
        public string HoverSound = "sfx_hover";
        /// <summary>A caption's colour, c_white, and while the mouse is over it c_cornflowerblue, as #RRGGBB.</summary>
        public string Color = "#FFFFFF";
        public string HoverColor = "#6495ED";
        /// <summary>The captions' and hover messages' outline: <c>draw_text_outline*(..., 2, ...)</c>.</summary>
        public int Outline = 2;
        /// <summary>The hover message's top: <c>room_height - 30</c>, centred on room_width / 2, in fnt_point.</summary>
        public int MessageBottomMargin = 30;

        internal void Validate(List<string> errors, string path)
        {
            if (Buttons == null) errors.Add(path + ".Buttons: must not be null.");
            else
            {
                var seen = new HashSet<string>();
                for (int i = 0; i < Buttons.Length; i++)
                {
                    string entry = $"{path}.Buttons[{i}]";
                    if (Buttons[i] == null) { errors.Add(entry + ": must not be null."); continue; }
                    Buttons[i].Validate(errors, entry, inDialog: false);
                    if (!string.IsNullOrEmpty(Buttons[i].Object) && !seen.Add(Buttons[i].Object))
                        errors.Add($"{entry}.Object: {Buttons[i].Object} is listed more than once.");
                }
            }
            if (Dialog == null) errors.Add(path + ".Dialog: must not be null.");
            else Dialog.Validate(errors, path + ".Dialog");
            if (!(MenuFadeSpeed > 0d) || MenuFadeSpeed > 1d) errors.Add($"{path}.MenuFadeSpeed: must be greater than 0 and at most 1 (was {MenuFadeSpeed}).");
            ValidationHelpers.Required(errors, path + ".HoverSound", HoverSound);
            if (!StartupTuning.IsHexColor(Color)) errors.Add($"{path}.Color: must be #RRGGBB (was {Color ?? "null"}).");
            if (!StartupTuning.IsHexColor(HoverColor)) errors.Add($"{path}.HoverColor: must be #RRGGBB (was {HoverColor ?? "null"}).");
            ValidationHelpers.NonNegative(errors, path + ".Outline", Outline);
            ValidationHelpers.NonNegative(errors, path + ".MessageBottomMargin", MessageBottomMargin);
        }
    }
}
