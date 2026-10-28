using System;
using System.Collections.Generic;
using Unity.Hare.Content;
using Unity.Hare.Flow;
using Unity.Hare.Map;

namespace Unity.Hare.Upgrade
{
    /// <summary>
    /// room_upgrade's way around as data (the content registry's <c>upgrades.Screen</c>, AGENTS.md rule 4; U.HARE-T64):
    /// the room's tiled background and obj_upgrade's obj_btn_map, the garage's way back to room_map. What the garage
    /// sells (obj_upgrade_*, the schematics, "Skill Points x") is U.HARE-T24's. The defaults are the original's, read
    /// from room_upgrade, obj_upgrade and obj_btn_map in HARE.gmx.
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

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Background", Background);
            if (MapButton == null) errors.Add(path + ".MapButton: must not be null.");
            else MapButton.Validate(errors, path + ".MapButton");
        }
    }

    /// <summary>Something that happened in room_upgrade, returned by every <see cref="UpgradeCommand"/>.</summary>
    public abstract class UpgradeEvent
    {
        /// <summary>The seat whose command raised it.</summary>
        public PlayerId Id { get; internal set; }
    }

    /// <summary><c>audio_play_sound(</c><see cref="Sound"/><c>, ...)</c>: obj_btn_map's sfx_hover and sfx_click.</summary>
    public sealed class UpgradeSoundRequested : UpgradeEvent { public string Sound; }

    /// <summary>obj_btn_map's mouseover changed (its message is drawn while it is on).</summary>
    public sealed class UpgradeHoverChanged : UpgradeEvent { public bool Hovered; }

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

    /// <summary>The mouse at room pixel (x, y): obj_btn_map's mouse enter and leave events.</summary>
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

    /// <summary>The left button pressed at room pixel (x, y): obj_btn_map's left pressed event. Refused off the button.</summary>
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
    /// room_upgrade's rules, engine-free (U.HARE-T64): the bg_metal scroll and obj_btn_map, which takes the player back
    /// to room_map. The room's view (Runtime's UpgradeRoom) sends the mouse and the left button as commands and plays
    /// what comes back; Esc does nothing here (the rooms table gives room_upgrade no EscapeToTitle), so obj_btn_map is
    /// the room's only way out, as in the original.
    /// </summary>
    public sealed class UpgradeScreen
    {
        readonly List<UpgradeEvent> events = new List<UpgradeEvent>();
        PlayerId issuer;

        public UpgradeScreen(UpgradeScreenTuning tuning)
        {
            Tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        }

        public UpgradeScreenTuning Tuning { get; }

        /// <summary>Whether obj_upgrade's Create has run.</summary>
        public bool Created { get; private set; }

        /// <summary>obj_btn_map's mouseover.</summary>
        public bool MapHovered { get; private set; }

        /// <summary>obj_btn_map was pressed: the room is fading to room_map.</summary>
        public bool Leaving { get; private set; }

        /// <summary>background_x[0] (bg_metal, tiled), moved by its hspeed every step.</summary>
        public int BackgroundX { get; private set; }

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
            bool over = Tuning.MapButton.Contains(x, y);
            if (over == MapHovered) return false;
            MapHovered = over;
            if (over) Emit(new UpgradeSoundRequested { Sound = Tuning.MapButton.HoverSound });
            Emit(new UpgradeHoverChanged { Hovered = over });
            return true;
        }

        internal bool Press(double x, double y)
        {
            if (!Created || !Tuning.MapButton.Contains(x, y)) return false;
            // obj_btn_map's left pressed has no obj_fade_upgrade.mode check, so every press asks for the fade; the
            // room flow decides what a second request during the fade does, as it does for obj_btn_upgrade's.
            Leaving = true;
            Emit(new UpgradeSoundRequested { Sound = Tuning.MapButton.ClickSound });
            Emit(new UpgradeFadeRequested { Room = Tuning.MapButton.Room });
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
