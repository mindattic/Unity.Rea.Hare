using System;
using System.Collections.Generic;
using Unity.Hare.Art;
using Unity.Hare.Content;
using Unity.Hare.Flow;

namespace Unity.Hare.Map
{
    /// <summary>
    /// obj_btn_upgrade, the map's one button (depth -5): spr_button_area_small drawn by its draw_self, "Upgrades" over it
    /// in fnt_point with a size-2 outline, white or c_cornflowerblue under the mouse, and its message at the bottom of the
    /// room while the mouse is over it. Its left press plays sfx_click and fades to room_upgrade.
    /// </summary>
    [Serializable]
    public class MapButtonDefinition
    {
        public string Object = "obj_btn_upgrade";
        /// <summary>obj_map's Create: <c>instance_create(100, 40, obj_btn_upgrade)</c>.</summary>
        public int X = 100;
        public int Y = 40;
        /// <summary>spr_button_area_small (210x35, origin 105,17, a rectangle mask over the whole image).</summary>
        public string Sprite = "spr_button_area_small";
        public int Width = 210;
        public int Height = 35;
        public int XOrigin = 105;
        public int YOrigin = 17;
        /// <summary>Draw: <c>draw_text_outline_color_alpha(x, y-10, 2, "Upgrades", color, c_black, 1)</c>.</summary>
        public string Label = "Upgrades";
        public int LabelOffset = -10;
        public int Outline = 2;
        /// <summary>Create's <c>message</c>, drawn at <c>(room_width / 2, room_height - 30)</c> while the mouse is over it.</summary>
        public string Message = "Upgrade H.A.R.E. Unit's systems";
        public int MessageBottomMargin = 30;
        public string Color = "#FFFFFF";
        public string HoverColor = "#6495ED";
        /// <summary>Mouse enter: <c>audio_play_sound(sfx_hover, 2, 0)</c>.</summary>
        public string HoverSound = "sfx_hover";
        /// <summary>Left pressed: <c>audio_play_sound(sfx_click, 1, 0); next_room = room_upgrade; fade.mode = "fadein"</c>.</summary>
        public string ClickSound = "sfx_click";
        public RoomId Room = RoomId.Upgrade;

        /// <summary>Whether room pixel (<paramref name="px"/>, <paramref name="py"/>) is over the button's mask.</summary>
        public bool Contains(double px, double py) =>
            px >= X - XOrigin && px <= X - XOrigin + Width - 1 && py >= Y - YOrigin && py <= Y - YOrigin + Height - 1;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Object", Object);
            ValidationHelpers.Required(errors, path + ".Sprite", Sprite);
            ValidationHelpers.Positive(errors, path + ".Width", Width);
            ValidationHelpers.Positive(errors, path + ".Height", Height);
            ValidationHelpers.Required(errors, path + ".Label", Label);
            ValidationHelpers.NonNegative(errors, path + ".Outline", Outline);
            ValidationHelpers.Required(errors, path + ".Message", Message);
            Popups.FloatTextDefinition.CheckColor(errors, path + ".Color", Color);
            Popups.FloatTextDefinition.CheckColor(errors, path + ".HoverColor", HoverColor);
            ValidationHelpers.Required(errors, path + ".HoverSound", HoverSound);
            ValidationHelpers.Required(errors, path + ".ClickSound", ClickSound);
            if (!Enum.IsDefined(typeof(RoomId), Room)) errors.Add($"{path}.Room: not a room (was {(int)Room}).");
        }
    }

    /// <summary>
    /// room_map as data (the content registry's <c>map</c> section, AGENTS.md rule 4; U.HARE-T19): obj_map, obj_earthball,
    /// obj_destination, obj_uss_warren's place, obj_white_dot, obj_scenario_details and obj_btn_upgrade. The mission
    /// markers (obj_alert, their degrees, scenarios and levels) are the popups section's; the comm box's line is the story
    /// section's <c>Conversation.MapScript</c>; the tip's text is <c>popups.Tip.MapText</c>. The defaults are the original's,
    /// read from HARE.gmx.
    /// </summary>
    [Serializable]
    public class MapTuning
    {
        /// <summary>room_map's background 0, tiled, scrolled by background_hspeed[0].</summary>
        public string Background = "bg_nightsky";
        /// <summary>D: <c>background_hspeed[0] = 1</c>; A: <c>-1</c>; a reached mission: <c>0</c>.</summary>
        public int SkyScroll = 1;
        /// <summary>A and D's key press: <c>audio_play_sound(sfx_flick, 1, 0)</c>.</summary>
        public string SwitchSound = "sfx_flick";

        /// <summary>obj_earthball: spr_earthball (1066x1062, origin 533,531) at <c>(room_width/2, room_height)</c>.</summary>
        public string EarthSprite = "spr_earthball";
        public int EarthHeight = 1062;
        /// <summary>obj_earthball's Create: <c>turn_speed = 2</c>, degrees a step obj_destination turns it while moving.</summary>
        public double TurnSpeed = 2d;
        /// <summary>obj_earthball's Create: <c>auto_speed = 0.01</c>, taken off image_angle every idle step (0 once launched).</summary>
        public double AutoSpeed = 0.01d;

        /// <summary>obj_uss_warren: placed at x 1143; Create's <c>y = room_height - earth/2 - spr_scanner_beam's 211 - spr_uss_warren's 66 + 100</c>.</summary>
        public string ShipSprite = "spr_uss_warren";
        public int ShipX = 1143;
        public int ScannerBeamHeight = 211;
        public int ShipHeight = 66;
        public int ShipLift = 100;

        /// <summary>obj_destination: placed at x 903; Create's <c>y = room_height - sprite_get_height(spr_earthball)/2</c>. Never drawn.</summary>
        public int DestinationX = 903;
        /// <summary>spr_destination's mask (64x130, origin 32,130, a precise mask opaque over its whole 25..35 x 0..129 box).</summary>
        public GmlPreciseMask DestinationMask = new GmlPreciseMask(32, 130, 25, 0, 35, 129);

        /// <summary>
        /// spr_alertmarker's mask (32x64, origin 16,32, precise with a mask for each of its six frames, its box set by hand to
        /// 0..31 x 23..40): obj_destination's collision with obj_alert tests these pixels.
        /// </summary>
        public GmlPreciseMask MarkerMask = new GmlPreciseMask(16, 32, 0, 23, 31, 40,
            "..............#...##............ .............#.#..##............ ............#..#..##............ ...........#...#..##............ ..........#....#..##............ .........#.....#..############.. ........#......#..############.. .......#.......#..############.. ......#........#..############.. ......#........#..##...##...##.. .......#.......#..##...##...##.. ........#......#..##...##...##.. .........#.....#..##...##...##.. ..........#....#..##...##...##.. ...........#...#..############.. ............#..#..############.. .............#.#..############.. ..............#...############..",
            ".............#....##............ ............#.#...##............ ...........#..#...##............ ..........#...#...##............ .........#....#...##............ ........#.....#...############.. .......#......#...############.. ......#.......#...############.. .....#........#...############.. .....#........#...##...##...##.. ......#.......#...##...##...##.. .......#......#...##...##...##.. ........#.....#...##...##...##.. .........#....#...##...##...##.. ..........#...#...############.. ...........#..#...############.. ............#.#...############.. .............#....############..",
            "............#.....##............ ...........#.#....##............ ..........#..#....##............ .........#...#....##............ ........#....#....##............ .......#.....#....############.. ......#......#....############.. .....#.......#....############.. ....#........#....############.. ....#........#....##...##...##.. .....#.......#....##...##...##.. ......#......#....##...##...##.. .......#.....#....##...##...##.. ........#....#....##...##...##.. .........#...#....############.. ..........#..#....############.. ...........#.#....############.. ............#.....############..",
            "...........#......##............ ..........#.#.....##............ .........#..#.....##............ ........#...#.....##............ .......#....#.....##............ ......#.....#.....############.. .....#......#.....############.. ....#.......#.....############.. ...#........#.....############.. ...#........#.....##...##...##.. ....#.......#.....##...##...##.. .....#......#.....##...##...##.. ......#.....#.....##...##...##.. .......#....#.....##...##...##.. ........#...#.....############.. .........#..#.....############.. ..........#.#.....############.. ...........#......############..",
            "............#.....##............ ...........#.#....##............ ..........#..#....##............ .........#...#....##............ ........#....#....##............ .......#.....#....############.. ......#......#....############.. .....#.......#....############.. ....#........#....############.. ....#........#....##...##...##.. .....#.......#....##...##...##.. ......#......#....##...##...##.. .......#.....#....##...##...##.. ........#....#....##...##...##.. .........#...#....############.. ..........#..#....############.. ...........#.#....############.. ............#.....############..",
            ".............#....##............ ............#.#...##............ ...........#..#...##............ ..........#...#...##............ .........#....#...##............ ........#.....#...############.. .......#......#...############.. ......#.......#...############.. .....#........#...############.. .....#........#...##...##...##.. ......#.......#...##...##...##.. .......#......#...##...##...##.. ........#.....#...##...##...##.. .........#....#...##...##...##.. ..........#...#...############.. ...........#..#...############.. ............#.#...############.. .............#....############..");

        /// <summary>obj_white_dot: spr_dropdot (9x9, origin 4,4, five frames, a rectangle mask over 0..8 x 3..5) at depth 1.</summary>
        public string DotSprite = "spr_dropdot";
        public int DotFrames = 5;
        public GmlPreciseMask DotMask = new GmlPreciseMask(4, 4, 0, 3, 8, 5);
        /// <summary>Create: <c>image_speed = 0.1; fade_speed = 0.003</c>.</summary>
        public double DotImageSpeed = 0.1d;
        public double DotFadeSpeed = 0.003d;
        /// <summary>Step: <c>if(distance_to_point(destX, destY) &gt; 1) move_towards_point(destX, destY, 1)</c>.</summary>
        public double DotSpeed = 1d;
        public double DotArriveDistance = 1d;
        /// <summary>The launch: <c>audio_play_sound(sfx_takeoff, 1, 0)</c>.</summary>
        public string LaunchSound = "sfx_takeoff";
        /// <summary>Its arrival: <c>room_goto(room_intro)</c>.</summary>
        public RoomId LaunchRoom = RoomId.Intro;

        /// <summary>
        /// obj_scenario_details: placed at (92, 179), hidden until a mission is reached, then <c>"Scenario " +
        /// string(scenario_id)</c>. Its Create: spr_tip_box (548x250, origin 0,0), <c>alpha = 0.7</c>, <c>margin = 20</c>;
        /// its Draw GUI: the box at alpha 0.7, then the text at alpha 1, fa_left, fnt_point, size-1 outline at
        /// <c>(x + margin, y + margin)</c>. <see cref="PanelText"/>'s {0} is the scenario; by the owner's direction
        /// (docs/BIBLE.md V42) a second line names the level it launches ({1}, the arena section's level_name) and the
        /// locked boss adds <see cref="LockedText"/>.
        /// </summary>
        public int PanelX = 92;
        public int PanelY = 179;
        public string PanelSprite = "spr_tip_box";
        public float PanelAlpha = 0.7f;
        public int PanelMargin = 20;
        public int PanelOutline = 1;
        public string PanelText = "Scenario {0}#{1}";
        public string LockedText = "#Locked: win every other mission first";
        /// <summary>The locked boss's marker is drawn in this colour (draw_self's image_blend); the others in white.</summary>
        public string LockedColor = "#808080";

        /// <summary>obj_btn_upgrade.</summary>
        public MapButtonDefinition Upgrade = new MapButtonDefinition();

        /// <summary>obj_earthball's Create: <c>y = room_height</c> (x is room_width / 2).</summary>
        public double EarthY(int roomHeight) => roomHeight;

        /// <summary>obj_uss_warren's Create: <c>y = room_height - earth/2 - spr_scanner_beam - spr_uss_warren + 100</c> (372).</summary>
        public double ShipY(int roomHeight) => roomHeight - EarthHeight / 2d - ScannerBeamHeight - ShipHeight + ShipLift;

        /// <summary>obj_destination's Create: <c>y = room_height - sprite_get_height(spr_earthball)/2</c> (549).</summary>
        public double DestinationY(int roomHeight) => roomHeight - EarthHeight / 2d;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Background", Background);
            ValidationHelpers.NonNegative(errors, path + ".SkyScroll", SkyScroll);
            ValidationHelpers.Required(errors, path + ".SwitchSound", SwitchSound);
            ValidationHelpers.Required(errors, path + ".EarthSprite", EarthSprite);
            ValidationHelpers.Positive(errors, path + ".EarthHeight", EarthHeight);
            if (!(TurnSpeed >= 0d)) errors.Add($"{path}.TurnSpeed: must not be negative (was {TurnSpeed}).");
            if (!(AutoSpeed >= 0d)) errors.Add($"{path}.AutoSpeed: must not be negative (was {AutoSpeed}).");
            ValidationHelpers.Required(errors, path + ".ShipSprite", ShipSprite);
            ValidationHelpers.NonNegative(errors, path + ".ScannerBeamHeight", ScannerBeamHeight);
            ValidationHelpers.NonNegative(errors, path + ".ShipHeight", ShipHeight);
            if (DestinationMask == null) errors.Add(path + ".DestinationMask: must not be null.");
            else DestinationMask.Validate(errors, path + ".DestinationMask");
            if (MarkerMask == null) errors.Add(path + ".MarkerMask: must not be null.");
            else MarkerMask.Validate(errors, path + ".MarkerMask");
            ValidationHelpers.Required(errors, path + ".DotSprite", DotSprite);
            ValidationHelpers.Positive(errors, path + ".DotFrames", DotFrames);
            if (DotMask == null) errors.Add(path + ".DotMask: must not be null.");
            else DotMask.Validate(errors, path + ".DotMask");
            if (!(DotImageSpeed >= 0d)) errors.Add($"{path}.DotImageSpeed: must not be negative (was {DotImageSpeed}).");
            if (!(DotFadeSpeed >= 0d)) errors.Add($"{path}.DotFadeSpeed: must not be negative (was {DotFadeSpeed}).");
            if (!(DotSpeed > 0d)) errors.Add($"{path}.DotSpeed: must be greater than 0 (was {DotSpeed}).");
            if (!(DotArriveDistance >= DotSpeed / 2d)) errors.Add($"{path}.DotArriveDistance: must be at least half of DotSpeed, or the dot can step past its destination (was {DotArriveDistance}).");
            ValidationHelpers.Required(errors, path + ".LaunchSound", LaunchSound);
            if (!Enum.IsDefined(typeof(RoomId), LaunchRoom)) errors.Add($"{path}.LaunchRoom: not a room (was {(int)LaunchRoom}).");
            ValidationHelpers.Required(errors, path + ".PanelSprite", PanelSprite);
            if (PanelAlpha < 0f || PanelAlpha > 1f) errors.Add($"{path}.PanelAlpha: must be 0-1 (was {PanelAlpha}).");
            ValidationHelpers.NonNegative(errors, path + ".PanelMargin", PanelMargin);
            ValidationHelpers.NonNegative(errors, path + ".PanelOutline", PanelOutline);
            if (PanelText == null) errors.Add(path + ".PanelText: must not be null.");
            if (LockedText == null) errors.Add(path + ".LockedText: must not be null.");
            Popups.FloatTextDefinition.CheckColor(errors, path + ".LockedColor", LockedColor);
            if (Upgrade == null) errors.Add(path + ".Upgrade: must not be null.");
            else Upgrade.Validate(errors, path + ".Upgrade");
        }
    }
}
