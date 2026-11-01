using System;
using System.Collections.Generic;
using Unity.Rea.Hare.Content;
using Unity.Rea.Hare.Flow;
using Unity.Rea.Hare.Map;
using Unity.Rea.Hare.Session;

namespace Unity.Rea.Hare.Upgrade
{
    /// <summary>
    /// A sprite's collision box in GameMaker's terms: bbox_left..bbox_right and bbox_top..bbox_bottom, inclusive, from
    /// its origin (both the pips' and the icons' origins are their top-left corners).
    /// </summary>
    [Serializable]
    public class UpgradeMask
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        /// <summary>Whether room pixel (<paramref name="px"/>, <paramref name="py"/>) is inside the box of a sprite at (x, y).</summary>
        public bool Contains(int x, int y, double px, double py) =>
            px >= x + Left && px <= x + Right && py >= y + Top && py <= y + Bottom;

        internal void Validate(List<string> errors, string path)
        {
            if (Right < Left) errors.Add($"{path}: Right ({Right}) is left of Left ({Left}).");
            if (Bottom < Top) errors.Add($"{path}: Bottom ({Bottom}) is above Top ({Top}).");
        }
    }

    /// <summary>
    /// One of room_upgrade's weapon icons (obj_btn_upgrade_vulcan, obj_btn_upgrade_shotgun, obj_upgrade_missle): its
    /// sprite drawn by draw_self, and its Create's <c>message</c> at the bottom of the room while the mouse is over it.
    /// It has no press event and no sound.
    /// </summary>
    [Serializable]
    public class UpgradeIconDefinition
    {
        public string Object = "obj_btn_upgrade_vulcan";
        public string Sprite = "spr_icon_vulcan";
        public int X;
        public int Y;
        public string Message = "";

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Object", Object);
            ValidationHelpers.Required(errors, path + ".Sprite", Sprite);
            ValidationHelpers.Required(errors, path + ".Message", Message);
        }
    }

    /// <summary>One of room_upgrade's obj_label instances: <c>draw_text_outline_color_alpha(x, y-10, 2, text, c_white, c_black, 1)</c>, fa_left, fnt_point.</summary>
    [Serializable]
    public class UpgradeLabelDefinition
    {
        public string Text = "";
        public int X;
        public int Y;

        internal void Validate(List<string> errors, string path) => ValidationHelpers.Required(errors, path + ".Text", Text);
    }

    /// <summary>
    /// room_upgrade's way around and its look as data (the content registry's <c>upgrades.Screen</c>, AGENTS.md rule
    /// 4): the room's tiled background, obj_upgrade's obj_btn_map (U.HARE-T64), the weapon icons, the row labels, the
    /// pips' sprites and boxes, and obj_upgrade's "Skill Points x" line (U.HARE-T24). The pips themselves are the
    /// catalog's <see cref="UpgradeCatalog.Upgrades"/>. The defaults are the original's, read from room_upgrade,
    /// obj_upgrade, obj_btn_map, obj_btn_upgrade_*, obj_upgrade_*, obj_label and their sprites in HARE.gmx.
    /// </summary>
    [Serializable]
    public class UpgradeScreenTuning
    {
        /// <summary>room_upgrade's background 0: bg_metal, tiled both ways.</summary>
        public string Background = "bg_metal";
        /// <summary>room_upgrade's background 0 hspeed: -1, the metal sliding left a pixel a step.</summary>
        public int BackgroundHSpeed = -1;

        /// <summary>
        /// obj_upgrade's Create: <c>instance_create(100, 40, obj_btn_map)</c>. obj_btn_map is obj_btn_upgrade's twin:
        /// spr_button_area_small (depth -5) drawn by draw_self, "Observation Deck" at <c>(x, y-10)</c> in fnt_point with a
        /// size-2 outline, white or c_cornflowerblue under the mouse, and its Create's message (copied unchanged from
        /// obj_btn_upgrade: "Upgrade H.A.R.E. Unit's systems") at the bottom of the room while hovered. Its left press
        /// plays sfx_click and fades to room_map (<c>next_room = room_map; fade.mode = "fadein"</c>).
        /// </summary>
        public MapButtonDefinition MapButton = new MapButtonDefinition
        {
            Object = "obj_btn_map", Label = "Observation Deck", Room = RoomId.Map,
        };

        /// <summary>The weapon icons at x 464, one per row.</summary>
        public UpgradeIconDefinition[] Icons =
        {
            new UpgradeIconDefinition { Object = "obj_btn_upgrade_vulcan", Sprite = "spr_icon_vulcan", X = 464, Y = 352, Message = "A high powered gatling gun which cycles up to maximum firing rate" },
            new UpgradeIconDefinition { Object = "obj_btn_upgrade_shotgun", Sprite = "spr_icon_shotgun", X = 464, Y = 480, Message = "A shotgun that shoots a burst of pellets in a spread" },
            new UpgradeIconDefinition { Object = "obj_upgrade_missle", Sprite = "spr_icon_missle", X = 464, Y = 608, Message = "A fire-and-forget explosive projectile" },
        };

        /// <summary>The row captions (obj_label's creation code sets <c>text</c>).</summary>
        public UpgradeLabelDefinition[] Labels =
        {
            new UpgradeLabelDefinition { Text = "Rate of Fire", X = 592, Y = 336 },
            new UpgradeLabelDefinition { Text = "Reload", X = 592, Y = 464 },
            new UpgradeLabelDefinition { Text = "Spread", X = 784, Y = 464 },
            new UpgradeLabelDefinition { Text = "Power", X = 592, Y = 592 },
        };

        /// <summary>obj_label's Draw: the text at <c>(x, y + LabelOffset)</c> with a size-<see cref="Outline"/> outline.</summary>
        public int LabelOffset = -10;
        public int Outline = 2;

        /// <summary>A pip's sprite before and after it is bought (52x57, origin 0,0).</summary>
        public string PipEmptySprite = "spr_upgrade_circle_empty";
        public string PipFilledSprite = "spr_upgrade_circle_filled";
        /// <summary>The pips' and icons' sprites' size (all 52x57).</summary>
        public int SpriteWidth = 52;
        public int SpriteHeight = 57;
        /// <summary>spr_upgrade_circle_*'s box: 5..46 by 8..49.</summary>
        public UpgradeMask PipMask = new UpgradeMask { Left = 5, Top = 8, Right = 46, Bottom = 49 };
        /// <summary>spr_icon_*'s box: the whole 52x57 image.</summary>
        public UpgradeMask IconMask = new UpgradeMask { Left = 0, Top = 0, Right = 51, Bottom = 56 };
        /// <summary>A pip's mouse enter: <c>audio_play_sound(sfx_hover, 2, 0)</c> (the icons make no sound).</summary>
        public string PipHoverSound = "sfx_hover";

        /// <summary>The icons' and pips' message: <c>draw_text_outline(room_width / 2, room_height - 30, 2, message)</c>.</summary>
        public int MessageBottomMargin = 30;

        /// <summary>
        /// obj_upgrade's Draw GUI: <c>draw_text_outline_color_alpha(room_width - 200, room_height - 60, 2, "Skill Points x"
        /// + string(SKILLPOINTS), c_white, c_black, 1)</c>, fa_center, fnt_score.
        /// </summary>
        public string SkillPointsText = "Skill Points x";
        public int SkillPointsRightMargin = 200;
        public int SkillPointsBottomMargin = 60;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Background", Background);
            if (MapButton == null) errors.Add(path + ".MapButton: must not be null.");
            else MapButton.Validate(errors, path + ".MapButton");
            if (Icons == null) { errors.Add(path + ".Icons: must not be null."); Icons = Array.Empty<UpgradeIconDefinition>(); }
            for (int i = 0; i < Icons.Length; i++)
            {
                if (Icons[i] == null) errors.Add($"{path}.Icons[{i}]: must not be null.");
                else Icons[i].Validate(errors, $"{path}.Icons[{i}]");
            }
            if (Labels == null) { errors.Add(path + ".Labels: must not be null."); Labels = Array.Empty<UpgradeLabelDefinition>(); }
            for (int i = 0; i < Labels.Length; i++)
            {
                if (Labels[i] == null) errors.Add($"{path}.Labels[{i}]: must not be null.");
                else Labels[i].Validate(errors, $"{path}.Labels[{i}]");
            }
            ValidationHelpers.NonNegative(errors, path + ".Outline", Outline);
            ValidationHelpers.Required(errors, path + ".PipEmptySprite", PipEmptySprite);
            ValidationHelpers.Required(errors, path + ".PipFilledSprite", PipFilledSprite);
            ValidationHelpers.Positive(errors, path + ".SpriteWidth", SpriteWidth);
            ValidationHelpers.Positive(errors, path + ".SpriteHeight", SpriteHeight);
            if (PipMask == null) errors.Add(path + ".PipMask: must not be null.");
            else PipMask.Validate(errors, path + ".PipMask");
            if (IconMask == null) errors.Add(path + ".IconMask: must not be null.");
            else IconMask.Validate(errors, path + ".IconMask");
            ValidationHelpers.Required(errors, path + ".PipHoverSound", PipHoverSound);
            ValidationHelpers.Required(errors, path + ".SkillPointsText", SkillPointsText);
        }
    }

    /// <summary>Something that happened in room_upgrade, returned by every <see cref="UpgradeCommand"/>.</summary>
    public abstract class UpgradeEvent
    {
        /// <summary>The seat whose command raised it.</summary>
        public PlayerId Id { get; internal set; }
    }

    /// <summary><c>audio_play_sound(</c><see cref="Sound"/><c>, ...)</c>: obj_btn_map's sfx_hover and sfx_click, a pip's sfx_hover.</summary>
    public sealed class UpgradeSoundRequested : UpgradeEvent { public string Sound; }

    /// <summary>obj_btn_map's mouseover changed (its message is drawn while it is on).</summary>
    public sealed class UpgradeHoverChanged : UpgradeEvent { public bool Hovered; }

    /// <summary>
    /// The icon or pip under the mouse changed: its <see cref="Message"/> is drawn at the bottom of the room (null: none
    /// is under the mouse).
    /// </summary>
    public sealed class UpgradeMessageChanged : UpgradeEvent { public string Message; }

    /// <summary>
    /// A pip was bought (<c>SKILLPOINTS -= 1; sprite_index = spr_upgrade_circle_filled</c>): <see cref="UpgradeId"/>
    /// is now in the campaign's upgrades, <see cref="SkillPoints"/> left. The room's view saves the campaign (what
    /// <c>//Write to Ini here</c> meant to do).
    /// </summary>
    public sealed class UpgradeBought : UpgradeEvent
    {
        public string UpgradeId;
        public int SkillPoints;
    }

    /// <summary><c>next_room = </c><see cref="Room"/><c>; fade.mode = "fadein"; fade.firstPass = true;</c> (obj_fade_upgrade).</summary>
    public sealed class UpgradeFadeRequested : UpgradeEvent { public RoomId Room; }

    /// <summary>
    /// The only way room_upgrade changes. A command carries the id of the seat it acts for (no global current player)
    /// and runs in <see cref="UpgradeScreen.Execute"/>, which returns whether it took effect and what it raised.
    /// </summary>
    public abstract class UpgradeCommand
    {
        protected UpgradeCommand(PlayerId id) => Id = id;

        public PlayerId Id { get; }

        /// <summary>Does the work; false when the command was refused (the rules said no).</summary>
        internal abstract bool Apply(UpgradeScreen screen);
    }

    /// <summary>The room comes up: obj_upgrade's Create (its fade, pointer and obj_btn_map). Once only.</summary>
    public sealed class CreateUpgrade : UpgradeCommand
    {
        public CreateUpgrade(PlayerId id) : base(id) { }
        internal override bool Apply(UpgradeScreen screen) => screen.Create();
    }

    /// <summary>One step at the room's room_speed: background_x[0] by its hspeed. Refused before Create.</summary>
    public sealed class StepUpgrade : UpgradeCommand
    {
        public StepUpgrade(PlayerId id) : base(id) { }
        internal override bool Apply(UpgradeScreen screen) => screen.Step();
    }

    /// <summary>The mouse at room pixel (x, y): the mouse enter and leave events of obj_btn_map, the icons and the pips.</summary>
    public sealed class PointUpgrade : UpgradeCommand
    {
        readonly double x, y;

        public PointUpgrade(PlayerId id, double x, double y) : base(id)
        {
            this.x = x;
            this.y = y;
        }

        internal override bool Apply(UpgradeScreen screen) => screen.Point(x, y);
    }

    /// <summary>
    /// The left button pressed at room pixel (x, y): obj_btn_map's and the pips' left pressed events. Refused off them,
    /// on a pip already bought, and on a pip the skill points do not cover.
    /// </summary>
    public sealed class PressUpgrade : UpgradeCommand
    {
        readonly double x, y;

        public PressUpgrade(PlayerId id, double x, double y) : base(id)
        {
            this.x = x;
            this.y = y;
        }

        internal override bool Apply(UpgradeScreen screen) => screen.Press(x, y);
    }

    /// <summary>
    /// room_upgrade's rules, engine-free (U.HARE-T64, U.HARE-T24): the bg_metal scroll, obj_btn_map back to room_map,
    /// the weapon icons' and pips' hover messages, and buying pips with SKILLPOINTS into the campaign's
    /// <see cref="CampaignProgress"/>. The room's view (Runtime's UpgradeRoom) sends the mouse and the left button as
    /// commands and plays what comes back; Esc does nothing here (the rooms table gives room_upgrade no EscapeToTitle),
    /// so obj_btn_map is the room's only way out, as in the original.
    /// </summary>
    public sealed class UpgradeScreen
    {
        readonly List<UpgradeEvent> events = new List<UpgradeEvent>();
        PlayerId issuer;
        string hoveredKey;

        /// <summary>The room's way around only, with nothing to sell (no catalog pips, nothing saved).</summary>
        public UpgradeScreen(UpgradeScreenTuning tuning)
            : this(new UpgradeCatalog { Screen = tuning ?? throw new ArgumentNullException(nameof(tuning)) }, new CampaignProgress(), 0)
        {
        }

        /// <summary>
        /// The garage selling <paramref name="catalog"/>'s pips into <paramref name="progress"/>, with
        /// <paramref name="startingSkillPoints"/> (globals.gml's SKILLPOINTS = 5) before any mission's.
        /// </summary>
        public UpgradeScreen(UpgradeCatalog catalog, CampaignProgress progress, int startingSkillPoints)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            Tuning = catalog.Screen ?? throw new ArgumentException("The catalog has no screen.", nameof(catalog));
            Progress = progress ?? throw new ArgumentNullException(nameof(progress));
            StartingSkillPoints = startingSkillPoints;
        }

        public UpgradeScreenTuning Tuning { get; }

        /// <summary>What the garage sells and what it does.</summary>
        public UpgradeCatalog Catalog { get; }

        /// <summary>The campaign the bought pips go into.</summary>
        public CampaignProgress Progress { get; }

        public int StartingSkillPoints { get; }

        /// <summary>SKILLPOINTS: what is left to spend (<see cref="CampaignProgress.SkillPoints"/>).</summary>
        public int SkillPoints => Progress.SkillPoints(StartingSkillPoints, Catalog);

        /// <summary>Whether obj_upgrade's Create has run.</summary>
        public bool Created { get; private set; }

        /// <summary>obj_btn_map's mouseover.</summary>
        public bool MapHovered { get; private set; }

        /// <summary>The message of the icon or pip under the mouse; null when none is.</summary>
        public string Message { get; private set; }

        /// <summary>obj_btn_map was pressed: the room is fading to room_map.</summary>
        public bool Leaving { get; private set; }

        /// <summary>background_x[0] (bg_metal, tiled), moved by its hspeed every step.</summary>
        public int BackgroundX { get; private set; }

        /// <summary>Whether the pip <paramref name="upgradeId"/> is bought (drawn spr_upgrade_circle_filled).</summary>
        public bool Owns(string upgradeId) => Progress.Owns(upgradeId);

        /// <summary>The pip whose box holds room pixel (x, y); null off every pip.</summary>
        public UpgradeCost PipAt(double x, double y)
        {
            if (Catalog.Upgrades == null || Tuning.PipMask == null) return null;
            foreach (var pip in Catalog.Upgrades)
                if (pip != null && Tuning.PipMask.Contains(pip.X, pip.Y, x, y)) return pip;
            return null;
        }

        /// <summary>The weapon icon whose box holds room pixel (x, y); null off every icon.</summary>
        public UpgradeIconDefinition IconAt(double x, double y)
        {
            if (Tuning.Icons == null || Tuning.IconMask == null) return null;
            foreach (var icon in Tuning.Icons)
                if (icon != null && Tuning.IconMask.Contains(icon.X, icon.Y, x, y)) return icon;
            return null;
        }

        /// <summary>Runs <paramref name="command"/> and returns whether it took effect and what it raised.</summary>
        public UpgradeResult Execute(UpgradeCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            events.Clear();
            issuer = command.Id;
            bool accepted = command.Apply(this);
            var raised = events.Count == 0 ? Array.Empty<UpgradeEvent>() : events.ToArray();
            events.Clear();
            return new UpgradeResult(accepted, raised);
        }

        void Emit(UpgradeEvent e)
        {
            e.Id = issuer;
            events.Add(e);
        }

        internal bool Create()
        {
            if (Created) return false;
            Created = true;
            MapHovered = false;
            Leaving = false;
            BackgroundX = 0;
            hoveredKey = null;
            Message = null;
            return true;
        }

        internal bool Step()
        {
            if (!Created) return false;
            BackgroundX += Tuning.BackgroundHSpeed;
            return true;
        }

        internal bool Point(double x, double y)
        {
            if (!Created) return false;
            bool changed = false;
            bool over = Tuning.MapButton.Contains(x, y);
            if (over != MapHovered)
            {
                MapHovered = over;
                if (over) Emit(new UpgradeSoundRequested { Sound = Tuning.MapButton.HoverSound });
                Emit(new UpgradeHoverChanged { Hovered = over });
                changed = true;
            }

            // The icons' and pips' mouse enter/leave: one at a time (none of them overlap), each drawing its message.
            var pip = PipAt(x, y);
            var icon = pip == null ? IconAt(x, y) : null;
            string key = pip != null ? "pip:" + pip.Id : icon != null ? "icon:" + icon.Object : null;
            if (key != hoveredKey)
            {
                hoveredKey = key;
                Message = pip != null ? pip.Message : icon?.Message;
                if (pip != null) Emit(new UpgradeSoundRequested { Sound = Tuning.PipHoverSound });
                Emit(new UpgradeMessageChanged { Message = Message });
                changed = true;
            }
            return changed;
        }

        internal bool Press(double x, double y)
        {
            if (!Created) return false;
            if (Tuning.MapButton.Contains(x, y))
            {
                // obj_btn_map's left pressed has no obj_fade_upgrade.mode check, so every press asks for the fade; the
                // room flow decides what a second request during the fade does, as it does for obj_btn_upgrade's.
                Leaving = true;
                Emit(new UpgradeSoundRequested { Sound = Tuning.MapButton.ClickSound });
                Emit(new UpgradeFadeRequested { Room = Tuning.MapButton.Room });
                return true;
            }

            // A pip's left pressed: if(SKILLPOINTS > 0) { SKILLPOINTS -= 1; sprite_index = spr_upgrade_circle_filled; }.
            // A pip already filled is not bought again (the original spent another point on it for nothing; BIBLE V49).
            var pip = PipAt(x, y);
            if (pip == null || Progress.Owns(pip.Id) || SkillPoints < pip.Cost) return false;
            Progress.RecordPurchase(pip.Id);
            Emit(new UpgradeBought { UpgradeId = pip.Id, SkillPoints = SkillPoints });
            return true;
        }
    }

    /// <summary>What <see cref="UpgradeScreen.Execute"/> returns: whether the command took effect and what it raised.</summary>
    public readonly struct UpgradeResult
    {
        public UpgradeResult(bool accepted, IReadOnlyList<UpgradeEvent> events)
        {
            Accepted = accepted;
            Events = events;
        }

        public bool Accepted { get; }
        public IReadOnlyList<UpgradeEvent> Events { get; }
    }
}
