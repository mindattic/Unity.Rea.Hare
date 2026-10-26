using System;
using System.Collections.Generic;
using Unity.Hare.Content;
using Unity.Hare.Flow;

namespace Unity.Hare.Arena
{
    /// <summary>
    /// One of a room's views as its .room.gmx defines it: the part of the room it shows (<c>wview</c>/<c>hview</c>),
    /// where on the 1920x1080 window it is drawn (<c>xport</c>/<c>yport</c>/<c>wport</c>/<c>hport</c>, y from the top),
    /// and how it follows obj_player (<c>hborder</c>/<c>vborder</c>, and <c>hspeed</c>/<c>vspeed</c> in pixels a step,
    /// -1 for no limit).
    /// </summary>
    [Serializable]
    public class ArenaViewDefinition
    {
        public int Width;
        public int Height;
        public int PortX;
        public int PortY;
        public int PortWidth;
        public int PortHeight;
        public int HBorder;
        public int VBorder;
        /// <summary>The most the view moves sideways in one step, pixels; -1 for no limit.</summary>
        public int HSpeed = -1;
        /// <summary>The most the view moves up or down in one step, pixels; -1 for no limit.</summary>
        public int VSpeed = -1;

        public ArenaViewDefinition() { }

        public ArenaViewDefinition(int width, int height, int portX, int portY, int portWidth, int portHeight, int hborder, int vborder, int hspeed, int vspeed)
        {
            Width = width;
            Height = height;
            PortX = portX;
            PortY = portY;
            PortWidth = portWidth;
            PortHeight = portHeight;
            HBorder = hborder;
            VBorder = vborder;
            HSpeed = hspeed;
            VSpeed = vspeed;
        }

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Positive(errors, path + ".Width", Width);
            ValidationHelpers.Positive(errors, path + ".Height", Height);
            ValidationHelpers.NonNegative(errors, path + ".PortX", PortX);
            ValidationHelpers.NonNegative(errors, path + ".PortY", PortY);
            ValidationHelpers.Positive(errors, path + ".PortWidth", PortWidth);
            ValidationHelpers.Positive(errors, path + ".PortHeight", PortHeight);
            ValidationHelpers.NonNegative(errors, path + ".HBorder", HBorder);
            ValidationHelpers.NonNegative(errors, path + ".VBorder", VBorder);
            if (HSpeed < -1) errors.Add($"{path}.HSpeed: must be -1 (no limit) or a speed (was {HSpeed}).");
            if (VSpeed < -1) errors.Add($"{path}.VSpeed: must be -1 (no limit) or a speed (was {VSpeed}).");
        }
    }

    /// <summary>One of a room's background slots as its .room.gmx sets it up before any level script runs.</summary>
    [Serializable]
    public class ArenaBackground
    {
        /// <summary>The GML background; empty for a slot with none (noone).</summary>
        public string Name = "";
        public bool Visible = true;
        public bool HTiled;
        public bool VTiled;
        /// <summary>Drawn stretched over the whole room instead of at its own size.</summary>
        public bool Stretch;

        public ArenaBackground() { }

        public ArenaBackground(string name, bool visible, bool htiled, bool vtiled, bool stretch)
        {
            Name = name ?? "";
            Visible = visible;
            HTiled = htiled;
            VTiled = vtiled;
            Stretch = stretch;
        }
    }

    /// <summary>A play room (room_gamescreen, room_sidescroll, room_shaft) as its .room.gmx defines it: background slots 0 and 1, view 0 and the minimap, view 1.</summary>
    [Serializable]
    public class ArenaRoomDefinition
    {
        public RoomId Room;
        public ArenaBackground[] Backgrounds = Array.Empty<ArenaBackground>();
        /// <summary>View 0, the screen: 1920x1080 following obj_player.</summary>
        public ArenaViewDefinition View = new ArenaViewDefinition();
        /// <summary>View 1, the minimap: a 3000x2000 look at the room drawn in a 300x200 port, shown while show_map is set.</summary>
        public ArenaViewDefinition Minimap = new ArenaViewDefinition();

        public ArenaRoomDefinition() { }

        public ArenaRoomDefinition(RoomId room, ArenaBackground[] backgrounds, ArenaViewDefinition view, ArenaViewDefinition minimap)
        {
            Room = room;
            Backgrounds = backgrounds;
            View = view;
            Minimap = minimap;
        }

        internal void Validate(List<string> errors, string path)
        {
            if (!Enum.IsDefined(typeof(RoomId), Room)) errors.Add($"{path}.Room: not a room (was {(int)Room}).");
            if (Backgrounds == null || Backgrounds.Length == 0) errors.Add($"{path}.Backgrounds: a room has at least one background slot.");
            else
                for (int i = 0; i < Backgrounds.Length; i++)
                    if (Backgrounds[i] == null) errors.Add($"{path}.Backgrounds[{i}]: must not be null.");
            if (View == null) errors.Add(path + ".View: must not be null.");
            else View.Validate(errors, path + ".View");
            if (Minimap == null) errors.Add(path + ".Minimap: must not be null.");
            else Minimap.Validate(errors, path + ".Minimap");
        }
    }

    /// <summary>
    /// A level script's setting half (level_01..level_10, level_default, level_enemygenerator; its "Meta Data" and
    /// "Looping" blocks): the room it plays in, its name, the backgrounds it puts in slots 0 and 1, slot 0's scroll
    /// speeds, and whether obj_player wraps around the room's edges instead of losing a life past them.
    /// </summary>
    [Serializable]
    public class LevelSetting
    {
        /// <summary>current_level this script runs for (setup_level's quick-start chain); 0 for level_default and level_enemygenerator.</summary>
        public int Level;
        /// <summary>The GML script (level_01, ...).</summary>
        public string Script = "";
        /// <summary>GML: level_name.</summary>
        public string Name = "";
        /// <summary>The room the script sends the game to (<c>if(room != room_x) room_goto(room_x)</c>).</summary>
        public RoomId Room = RoomId.GameScreen;
        /// <summary>GML: background_index[0].</summary>
        public string Background = "";
        /// <summary>GML: background_index[1]; empty for noone.</summary>
        public string Overlay = "";
        /// <summary>GML: background_hspeed (slot 0), pixels a step.</summary>
        public int HSpeed;
        /// <summary>GML: background_vspeed (slot 0), pixels a step.</summary>
        public int VSpeed;
        /// <summary>GML: background_htiled[0] = background_vtiled[0] = true (level_06 only).</summary>
        public bool Tiled;
        /// <summary>GML: loop_horizontal.</summary>
        public bool LoopHorizontal;
        /// <summary>GML: loop_vertical.</summary>
        public bool LoopVertical;
        /// <summary>GML: randomize_on_loop: a wrap runs random_positions.</summary>
        public bool RandomizeOnLoop;

        public LevelSetting() { }

        public LevelSetting(int level, string script, string name, RoomId room, string background, string overlay = "",
            int hspeed = 0, int vspeed = 0, bool tiled = false, bool loopHorizontal = false, bool loopVertical = false, bool randomizeOnLoop = false)
        {
            Level = level;
            Script = script;
            Name = name;
            Room = room;
            Background = background ?? "";
            Overlay = overlay ?? "";
            HSpeed = hspeed;
            VSpeed = vspeed;
            Tiled = tiled;
            LoopHorizontal = loopHorizontal;
            LoopVertical = loopVertical;
            RandomizeOnLoop = randomizeOnLoop;
        }

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.NonNegative(errors, path + ".Level", Level);
            ValidationHelpers.Required(errors, path + ".Script", Script);
            if (!Enum.IsDefined(typeof(RoomId), Room)) errors.Add($"{path}.Room: not a room (was {(int)Room}).");
        }
    }

    /// <summary>
    /// The arena as data (the content registry's <c>arena</c> section, AGENTS.md rule 4): the play rooms' views and
    /// background slots (room_gamescreen.room.gmx, room_sidescroll.room.gmx, room_shaft.room.gmx), every level
    /// script's setting, globals.gml's buffer_zone and show_map, and the numbers obj_player's Draw event uses for its
    /// arrows. The defaults are the original's. Room sizes are the room table's (<c>rooms</c>).
    /// </summary>
    [Serializable]
    public class ArenaTuning
    {
        /// <summary>globals.gml: buffer_zone, how far past a non-looping edge obj_player may fly before it costs a life.</summary>
        public int BufferZone = 400;
        /// <summary>
        /// obj_player's sprite_width and sprite_height while it flies (reset_player's asset_leap, spr_rabbit_leap, 96x96):
        /// a horizontal wrap puts it this far past the far edge, and reset_player puts it this far above the room.
        /// </summary>
        public int PlayerSize = 96;
        /// <summary>globals.gml: show_map, whether view 1 (the minimap) is visible when obj_game is created.</summary>
        public bool ShowMapOnStart;

        /// <summary>obj_player's Draw: the off-screen spr_big_arrow is drawn this many pixels inside the edge it left by.</summary>
        public int OffscreenArrowInset = 64;
        public string OffscreenArrowSprite = "spr_big_arrow";
        /// <summary>obj_player's Draw: spr_direction_arrow while it spins.</summary>
        public string DirectionArrowSprite = "spr_direction_arrow";
        /// <summary>Both arrows' draw_sprite_ext alpha.</summary>
        public float ArrowAlpha = 0.5f;

        /// <summary>The leap line's dashes (draw_dottedline(..., 4, 1, ...)): dash length and line width, pixels.</summary>
        public int LeapLineDash = 4;
        public int LeapLineWidth = 1;
        /// <summary>The leap line's draw_set_alpha(0.3).</summary>
        public float LeapLineAlpha = 0.3f;
        /// <summary>c_cornflowerblue, globals.gml's make_color_rgb(154, 206, 235).</summary>
        public string LeapLineColor = "#9ACEEB";
        /// <summary>obj_player's Step: <c>Len = -13; gunX = x + lengthdir_x(Len, image_angle)</c>, where the leap line starts.</summary>
        public int GunOffset = -13;
        /// <summary>obj_player Create: show_leap_arrow.</summary>
        public bool ShowLeapArrow = true;

        /// <summary>The GUI layer obj_game and the HUD objects draw on: view 0's size, 1920x1080.</summary>
        public int HudWidth = 1920;
        public int HudHeight = 1080;

        public ArenaRoomDefinition[] Rooms =
        {
            // room_gamescreen.room.gmx: bg_nightsky stretched in slot 0, an empty stretched slot 1; view 0 follows obj_player
            // with 1920/1080 borders at 40 px a step, view 1 (3000x2000) drawn at (1600, 800) 300x200.
            new ArenaRoomDefinition(RoomId.GameScreen,
                new[] { new ArenaBackground("bg_nightsky", true, false, false, true), new ArenaBackground("", true, false, false, true) },
                new ArenaViewDefinition(1920, 1080, 0, 0, 1920, 1080, 1920, 1080, 40, 40),
                new ArenaViewDefinition(3000, 2000, 1600, 800, 300, 200, 1920, 1080, -1, -1)),
            // room_sidescroll.room.gmx: bg_tropicalocean tiled across; both views follow with no speed limit, view 1 at (1300, 860).
            new ArenaRoomDefinition(RoomId.SideScroll,
                new[] { new ArenaBackground("bg_tropicalocean", true, true, false, false), new ArenaBackground("", true, false, false, false) },
                new ArenaViewDefinition(1920, 1080, 0, 0, 1920, 1080, 1920, 1080, -1, -1),
                new ArenaViewDefinition(3000, 2000, 1300, 860, 300, 200, 1920, 1080, -1, -1)),
            // room_shaft.room.gmx: bg_stormclouds tiled both ways.
            new ArenaRoomDefinition(RoomId.Shaft,
                new[] { new ArenaBackground("bg_stormclouds", true, true, true, false), new ArenaBackground("", true, false, false, false) },
                new ArenaViewDefinition(1920, 1080, 0, 0, 1920, 1080, 1920, 1080, -1, -1),
                new ArenaViewDefinition(3000, 2000, 1300, 860, 300, 200, 1920, 1080, -1, -1)),
        };

        /// <summary>level_01..level_10, as setup_level's quick-start chain runs them for current_level 1..10.</summary>
        public LevelSetting[] Levels =
        {
            new LevelSetting(1, "level_01", "Hydras & Saws", RoomId.GameScreen, "bg_worldcurve"),
            new LevelSetting(2, "level_02", "Meatballs & Phalanx", RoomId.GameScreen, "bg_moonview"),
            new LevelSetting(3, "level_03", "Sky Worms & Daggers", RoomId.GameScreen, "bg_uphigh"),
            new LevelSetting(4, "level_04", "Storm Shaft", RoomId.Shaft, "bg_stormclouds", hspeed: -1, loopHorizontal: true, loopVertical: true),
            new LevelSetting(5, "level_05", "Tangerine Islands", RoomId.SideScroll, "bg_tropicalocean", loopHorizontal: true, randomizeOnLoop: true),
            new LevelSetting(6, "level_06", "Wraiths", RoomId.GameScreen, "bg_nightsky", tiled: true),
            new LevelSetting(7, "level_07", "Trilobytes", RoomId.GameScreen, "bg_worldcurve"),
            new LevelSetting(8, "level_08", "Scrap Mental", RoomId.GameScreen, "bg_worldcurve"),
            new LevelSetting(9, "level_09", "Time Out", RoomId.GameScreen, "bg_atmosphere"),
            new LevelSetting(10, "level_10", "Upper Eschelon", RoomId.GameScreen, "bg_atmosphere"),
        };

        /// <summary>level_default: setup_level's quick start for a current_level with no script of its own.</summary>
        public LevelSetting DefaultLevel = new LevelSetting(0, "level_default", "Default Level", RoomId.GameScreen, "bg_orange");

        /// <summary>level_enemygenerator: the generator mode's level, bg_gradient3 over bg_nightsky.</summary>
        public LevelSetting GeneratorLevel = new LevelSetting(0, "level_enemygenerator", "Enemy Generator", RoomId.GameScreen, "bg_nightsky", overlay: "bg_gradient3");

        /// <summary>The views and backgrounds of play room <paramref name="room"/>, or null when it is not one.</summary>
        public ArenaRoomDefinition RoomFor(RoomId room)
        {
            if (Rooms == null) return null;
            foreach (var definition in Rooms)
                if (definition != null && definition.Room == room) return definition;
            return null;
        }

        /// <summary>The script setup_level's quick start runs for <paramref name="level"/>: its own level_NN, else level_default.</summary>
        public LevelSetting SettingFor(int level)
        {
            if (Levels != null)
                foreach (var setting in Levels)
                    if (setting != null && setting.Level == level) return setting;
            return DefaultLevel;
        }

        /// <summary>
        /// The setting to play in <paramref name="room"/> when the current level is <paramref name="level"/>: that level's
        /// own when it plays in this room; otherwise the first level that does (room_shaft opened on its own plays level 4,
        /// room_sidescroll level 5); otherwise the room as its .room.gmx leaves it, with no loops.
        /// </summary>
        public LevelSetting SettingFor(int level, RoomId room)
        {
            var own = SettingFor(level);
            if (own != null && own.Room == room) return own;
            if (Levels != null)
                foreach (var setting in Levels)
                    if (setting != null && setting.Room == room) return setting;
            var definition = RoomFor(room);
            string background = definition != null && definition.Backgrounds.Length > 0 ? definition.Backgrounds[0].Name : "";
            return new LevelSetting(0, RoomList.GmlName(room), "", room, background);
        }

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.NonNegative(errors, path + ".BufferZone", BufferZone);
            ValidationHelpers.Positive(errors, path + ".PlayerSize", PlayerSize);
            ValidationHelpers.NonNegative(errors, path + ".OffscreenArrowInset", OffscreenArrowInset);
            ValidationHelpers.Required(errors, path + ".OffscreenArrowSprite", OffscreenArrowSprite);
            ValidationHelpers.Required(errors, path + ".DirectionArrowSprite", DirectionArrowSprite);
            if (ArrowAlpha < 0f || ArrowAlpha > 1f) errors.Add($"{path}.ArrowAlpha: must be between 0 and 1 (was {ArrowAlpha}).");
            ValidationHelpers.Positive(errors, path + ".LeapLineDash", LeapLineDash);
            ValidationHelpers.Positive(errors, path + ".LeapLineWidth", LeapLineWidth);
            if (LeapLineAlpha < 0f || LeapLineAlpha > 1f) errors.Add($"{path}.LeapLineAlpha: must be between 0 and 1 (was {LeapLineAlpha}).");
            if (!global::Unity.Hare.Rooms.StartupTuning.IsHexColor(LeapLineColor)) errors.Add($"{path}.LeapLineColor: must be #RRGGBB (was {LeapLineColor ?? "null"}).");
            ValidationHelpers.Positive(errors, path + ".HudWidth", HudWidth);
            ValidationHelpers.Positive(errors, path + ".HudHeight", HudHeight);

            if (Rooms == null) errors.Add(path + ".Rooms: must not be null.");
            else
            {
                var seen = new HashSet<RoomId>();
                for (int i = 0; i < Rooms.Length; i++)
                {
                    string entryPath = $"{path}.Rooms[{i}]";
                    if (Rooms[i] == null) { errors.Add(entryPath + ": must not be null."); continue; }
                    Rooms[i].Validate(errors, entryPath);
                    if (!seen.Add(Rooms[i].Room)) errors.Add($"{entryPath}.Room: {Rooms[i].Room} is listed more than once.");
                }
            }

            if (Levels == null) errors.Add(path + ".Levels: must not be null.");
            else
            {
                var seen = new HashSet<int>();
                for (int i = 0; i < Levels.Length; i++)
                {
                    string entryPath = $"{path}.Levels[{i}]";
                    if (Levels[i] == null) { errors.Add(entryPath + ": must not be null."); continue; }
                    Levels[i].Validate(errors, entryPath);
                    ValidationHelpers.Positive(errors, entryPath + ".Level", Levels[i].Level);
                    if (!seen.Add(Levels[i].Level)) errors.Add($"{entryPath}.Level: {Levels[i].Level} is listed more than once.");
                    if (RoomFor(Levels[i].Room) == null) errors.Add($"{entryPath}.Room: {Levels[i].Room} is not a play room in Rooms.");
                }
            }

            if (DefaultLevel == null) errors.Add(path + ".DefaultLevel: must not be null.");
            else DefaultLevel.Validate(errors, path + ".DefaultLevel");
            if (GeneratorLevel == null) errors.Add(path + ".GeneratorLevel: must not be null.");
            else GeneratorLevel.Validate(errors, path + ".GeneratorLevel");
        }
    }
}
