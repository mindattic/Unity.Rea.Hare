using System;
using System.Collections.Generic;
using Unity.Hare.Content;
using Unity.Hare.Enemies;
using Unity.Hare.Flow;
using Unity.Hare.Session;
using Unity.Hare.Weather;

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
    /// A level script as data (level_01..level_10, level_default, level_enemygenerator). Its setting ("Meta Data" and
    /// "Looping": the room it plays in, its name, the backgrounds it puts in slots 0 and 1, slot 0's scroll speeds, and
    /// whether obj_player wraps around the room's edges instead of losing a life past them, U.HARE-T52), its
    /// "Surfaces" (rock_min/max, stationary_percent, structure_min/max, U.HARE-T56), its "Enemies" (each kind's
    /// X_min/X_max, <see cref="Enemies"/>) and its "Objective" (<see cref="Objective"/>), all U.HARE-T20. setup_level zeroes
    /// the rock, structure and enemy ranges before the script runs, so a block the script leaves out is 0; it does not
    /// reset stationary_percent (<see cref="StationaryPercent"/>).
    /// </summary>
    [Serializable]
    public class LevelSetting
    {
        /// <summary><see cref="StationaryPercent"/> for a script that does not set stationary_percent.</summary>
        public const int KeepsStationaryPercent = -1;

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
        /// <summary>
        /// GML: structure_min / structure_max (the script's "Surfaces" block, U.HARE-T56): setup_level places
        /// irandom_range(structure_min, structure_max) par_structure, each with its par_structure_cover. 1-3 in level_02 and
        /// level_04 (the shaft), 2-4 in level_07, none anywhere else.
        /// </summary>
        public int StructureMin;
        public int StructureMax;
        /// <summary>
        /// The weather emitters the level runs, in order (U.HARE-T18). Every play room places obj_cloud_emitter itself, so
        /// every level has <see cref="WeatherKind.Cloud"/>; obj_game's <c>instance_create(0,0, obj_snow_emitter)</c> and
        /// <c>obj_fog_emitter</c> are commented out and nothing makes rain or sand, so no shipped level names another kind.
        /// </summary>
        public WeatherKind[] Weather = { WeatherKind.Cloud };

        /// <summary>GML: rock_min / rock_max: setup_level places irandom_range(rock_min, rock_max) par_rock (U.HARE-T57 places them).</summary>
        public int RockMin;
        public int RockMax;
        /// <summary>
        /// GML: stationary_percent, par_rock's chance in 100 of standing still. A globalvar (50 at game start,
        /// <see cref="ArenaTuning.StartingStationaryPercent"/>) that setup_level never resets, so a script without the line
        /// (<see cref="KeepsStationaryPercent"/>: level_04, 05, 09, 10 and level_default) plays with whatever the level
        /// before it set; <see cref="StationaryPercentAfter"/>.
        /// </summary>
        public int StationaryPercent = KeepsStationaryPercent;
        /// <summary>The script's "Enemies" block.</summary>
        public LevelEnemies Enemies = new LevelEnemies();
        /// <summary>The script's "Objective" block (and level_08's "Spawn Boss").</summary>
        public LevelObjectiveDefinition Objective = new LevelObjectiveDefinition();

        public LevelSetting() { }

        public LevelSetting(int level, string script, string name, RoomId room, string background, string overlay = "",
            int hspeed = 0, int vspeed = 0, bool tiled = false, bool loopHorizontal = false, bool loopVertical = false, bool randomizeOnLoop = false,
            int structureMin = 0, int structureMax = 0)
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
            StructureMin = structureMin;
            StructureMax = structureMax;
        }

        /// <summary>The script's "Surfaces" rock lines.</summary>
        public LevelSetting WithRocks(int rockMin, int rockMax, int stationaryPercent = KeepsStationaryPercent)
        {
            RockMin = rockMin;
            RockMax = rockMax;
            StationaryPercent = stationaryPercent;
            return this;
        }

        /// <summary>The script's "Enemies" block.</summary>
        public LevelSetting WithEnemies(LevelEnemies enemies)
        {
            Enemies = enemies;
            return this;
        }

        /// <summary>The script's "Objective" block.</summary>
        public LevelSetting WithObjective(LevelObjectiveDefinition objective)
        {
            Objective = objective;
            return this;
        }

        /// <summary>stationary_percent once this script has run, when it was <paramref name="previous"/> before.</summary>
        public int StationaryPercentAfter(int previous) => StationaryPercent == KeepsStationaryPercent ? previous : StationaryPercent;

        /// <summary>The enemy ranges as setup_level's Generate blocks take them.</summary>
        public EnemyComposition ToComposition() => (Enemies ?? new LevelEnemies()).ToComposition();

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.NonNegative(errors, path + ".Level", Level);
            ValidationHelpers.NonNegative(errors, path + ".StructureMin", StructureMin);
            if (StructureMax < StructureMin) errors.Add($"{path}.StructureMax: must be at least StructureMin ({StructureMin}) (was {StructureMax}).");
            ValidationHelpers.NonNegative(errors, path + ".RockMin", RockMin);
            if (RockMax < RockMin) errors.Add($"{path}.RockMax: must be at least RockMin ({RockMin}) (was {RockMax}).");
            if (StationaryPercent != KeepsStationaryPercent && (StationaryPercent < 0 || StationaryPercent > 100))
                errors.Add($"{path}.StationaryPercent: must be -1 (the script leaves it) or 0-100 (was {StationaryPercent}).");
            if (Enemies == null) errors.Add(path + ".Enemies: must not be null.");
            else Enemies.Validate(errors, path + ".Enemies");
            if (Objective == null) errors.Add(path + ".Objective: must not be null.");
            else Objective.Validate(errors, path + ".Objective");
            ValidationHelpers.Required(errors, path + ".Script", Script);
            if (!Enum.IsDefined(typeof(RoomId), Room)) errors.Add($"{path}.Room: not a room (was {(int)Room}).");
            if (Weather == null) errors.Add(path + ".Weather: must not be null (an empty list for none).");
            else
            {
                var seen = new HashSet<WeatherKind>();
                foreach (var kind in Weather)
                    if (!seen.Add(kind)) errors.Add($"{path}.Weather: {kind} is listed more than once.");
            }
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

        /// <summary>obj_levelstart's Create: audio_play_sound(sfx_switch, 1, 0) (U.HARE-T57).</summary>
        public string LevelStartSound = "sfx_switch";
        /// <summary>obj_levelstart's alarm[0] = 120: the steps before it starts to fade.</summary>
        public int LevelStartFadeDelaySteps = 120;
        /// <summary>obj_levelstart's rate = 2: alarm[1], which takes a step off its alpha while it fades, every this many steps.</summary>
        public int LevelStartFadeRateSteps = 2;
        /// <summary>obj_levelstart's Alarm 1: alpha -= 0.01.</summary>
        public float LevelStartFadeStep = 0.01f;
        /// <summary>obj_levelstart's Draw GUI: draw_text_outline(x, y + 65, 2, strObjective) under level_name.</summary>
        public int LevelStartObjectiveOffsetY = 65;
        /// <summary>obj_levelstart's Draw GUI: both lines' draw_text_outline size.</summary>
        public int LevelStartOutline = 2;
        /// <summary>obj_game's Step: audio_play_sound(sfx_beep, 1, 0) for each of a time limit's last seconds (U.HARE-T57).</summary>
        public string TimeLimitBeepSound = "sfx_beep";

        /// <summary>globals.gml: stationary_percent = 50, what it is until a level script sets it.</summary>
        public int StartingStationaryPercent = 50;

        /// <summary>
        /// level_01..level_10, as setup_level's quick-start chain runs them for current_level 1..10 (and load_scenario
        /// for the campaign: scenario 1's levels 1-5 are level_01..05, every other scenario's level_06..10).
        /// </summary>
        public LevelSetting[] Levels =
        {
            new LevelSetting(1, "level_01", "Hydras & Saws", RoomId.GameScreen, "bg_worldcurve")
                .WithRocks(15, 30, 50)
                .WithEnemies(new LevelEnemies().With(EnemyKind.Hydra, 1, 3).With(EnemyKind.Sawblade, 1, 2).With(EnemyKind.Mimic, 1, 3).With(EnemyKind.Dagger, 1, 2))
                .WithObjective(new LevelObjectiveDefinition(ObjectiveKind.Points, 500)),
            new LevelSetting(2, "level_02", "Meatballs & Phalanx", RoomId.GameScreen, "bg_moonview", structureMin: 1, structureMax: 3)
                .WithRocks(15, 30, 25)
                .WithEnemies(new LevelEnemies().With(EnemyKind.Sawblade, 1, 2).With(EnemyKind.Meatball, 2, 2).With(EnemyKind.Phalanx, 2, 3).With(EnemyKind.Mimic, 3, 5))
                .WithObjective(new LevelObjectiveDefinition(ObjectiveKind.Points, 500)),
            new LevelSetting(3, "level_03", "Sky Worms & Daggers", RoomId.GameScreen, "bg_uphigh")
                .WithRocks(15, 20, 15)
                .WithEnemies(new LevelEnemies().With(EnemyKind.Mimic, 3, 5).With(EnemyKind.Worm, 2, 2).With(EnemyKind.Dagger, 3, 4))
                .WithObjective(new LevelObjectiveDefinition(ObjectiveKind.Points, 500)),
            new LevelSetting(4, "level_04", "Storm Shaft", RoomId.Shaft, "bg_stormclouds", hspeed: -1, loopHorizontal: true, loopVertical: true,
                    structureMin: 1, structureMax: 3)
                .WithRocks(20, 20)
                .WithEnemies(new LevelEnemies().With(EnemyKind.Hydra, 8, 10).With(EnemyKind.Sawblade, 5, 7).With(EnemyKind.Mimic, 10, 10))
                .WithObjective(new LevelObjectiveDefinition(ObjectiveKind.Points, 500)),
            new LevelSetting(5, "level_05", "Tangerine Islands", RoomId.SideScroll, "bg_tropicalocean", loopHorizontal: true, randomizeOnLoop: true)
                .WithRocks(0, 0)
                .WithEnemies(new LevelEnemies().With(EnemyKind.Hydra, 3, 4).With(EnemyKind.Sawblade, 2, 3).With(EnemyKind.Meatball, 1, 1).With(EnemyKind.Phalanx, 1, 2).With(EnemyKind.Mimic, 15, 20))
                .WithObjective(new LevelObjectiveDefinition(ObjectiveKind.TimeLimit, 500, 90)),
            new LevelSetting(6, "level_06", "Wraiths", RoomId.GameScreen, "bg_nightsky", tiled: true)
                .WithRocks(15, 30, 75)
                .WithEnemies(new LevelEnemies().With(EnemyKind.Hydra, 1, 2).With(EnemyKind.Sawblade, 2, 3).With(EnemyKind.Wraith, 2, 2).With(EnemyKind.Mimic, 2, 3))
                .WithObjective(new LevelObjectiveDefinition(ObjectiveKind.TimeLimit, 500, 90)),
            new LevelSetting(7, "level_07", "Trilobytes", RoomId.GameScreen, "bg_worldcurve", structureMin: 2, structureMax: 4)
                .WithRocks(15, 30, 30)
                .WithEnemies(new LevelEnemies().With(EnemyKind.Hydra, 1, 2).With(EnemyKind.Sawblade, 3, 4).With(EnemyKind.Mimic, 3, 5).With(EnemyKind.Dagger, 3, 4).With(EnemyKind.Trilobyte, 10, 12))
                .WithObjective(new LevelObjectiveDefinition(ObjectiveKind.TimeLimit, 500, 90)),
            new LevelSetting(8, "level_08", "Scrap Mental", RoomId.GameScreen, "bg_worldcurve")
                .WithRocks(20, 24, 60)
                .WithObjective(new LevelObjectiveDefinition(ObjectiveKind.Defeat, 500, targetName: "Scrap Mental", targetObject: "obj_scrapmental")),
            new LevelSetting(9, "level_09", "Time Out", RoomId.GameScreen, "bg_atmosphere")
                .WithRocks(15, 30)
                .WithEnemies(new LevelEnemies().With(EnemyKind.Hydra, 8, 10).With(EnemyKind.Sawblade, 20, 20).With(EnemyKind.Meatball, 1, 1))
                .WithObjective(new LevelObjectiveDefinition(ObjectiveKind.Points, 500, 90)),
            new LevelSetting(10, "level_10", "Upper Eschelon", RoomId.GameScreen, "bg_atmosphere")
                .WithRocks(15, 30)
                .WithEnemies(new LevelEnemies().With(EnemyKind.Hydra, 10, 12).With(EnemyKind.Sawblade, 20, 20).With(EnemyKind.Meatball, 2, 3))
                .WithObjective(new LevelObjectiveDefinition(ObjectiveKind.Points, 18000, 90, resetScore: false)),
        };

        /// <summary>level_default: setup_level's quick start for a current_level with no script of its own (and load_scenario's for an unknown scenario).</summary>
        public LevelSetting DefaultLevel = new LevelSetting(0, "level_default", "Default Level", RoomId.GameScreen, "bg_orange")
            .WithRocks(15, 30)
            .WithEnemies(new LevelEnemies().With(EnemyKind.Hydra, 3, 8).With(EnemyKind.Sawblade, 1, 6).With(EnemyKind.Meatball, 1, 3))
            .WithObjective(new LevelObjectiveDefinition(ObjectiveKind.Points, 10000, resetClock: false));

        /// <summary>level_enemygenerator: the generator mode's level, bg_gradient3 over bg_nightsky; its enemies are generate_enemy_list's.</summary>
        public LevelSetting GeneratorLevel = new LevelSetting(0, "level_enemygenerator", "Enemy Generator", RoomId.GameScreen, "bg_nightsky", overlay: "bg_gradient3")
            .WithRocks(15, 30, 50)
            .WithObjective(new LevelObjectiveDefinition(ObjectiveKind.Survive, 0));

        /// <summary>level_setting_00 and level_setting_01, load_level's first argument.</summary>
        public LevelSettingPart[] SettingParts =
        {
            new LevelSettingPart { Script = "level_setting_00", Room = RoomId.GameScreen, Background = "bg_worldcurve" },
            new LevelSettingPart { Script = "level_setting_01", Room = RoomId.GameScreen, Background = "bg_worldcurve" },
        };

        /// <summary>level_objective_00 and level_objective_01, load_level's second argument.</summary>
        public LevelObjectiveDefinition[] ObjectiveParts =
        {
            new LevelObjectiveDefinition(ObjectiveKind.Points, 500, resetClock: false, name: "Welcome to H.O.P",
                dialog: new[] { "Go get'em Hutch!#The planet is counting on you!" }),
            new LevelObjectiveDefinition(ObjectiveKind.Points, 500, resetScore: false, resetClock: false, name: "Level 02",
                dialog: new[] { "Go get'em Hutch!#The planet is counting on you!" }),
        };

        /// <summary>level_composition_00 and level_composition_01, load_level's third argument.</summary>
        public LevelComposition[] CompositionParts =
        {
            new LevelComposition { Script = "level_composition_00", RockMin = 20, RockMax = 25,
                Enemies = new LevelEnemies().With(EnemyKind.Hydra, 3, 5).With(EnemyKind.Sawblade, 2, 3).With(EnemyKind.Mimic, 1, 3).With(EnemyKind.Dagger, 2, 3) },
            new LevelComposition { Script = "level_composition_01", RockMin = 20, RockMax = 25,
                Enemies = new LevelEnemies().With(EnemyKind.Sawblade, 6, 8).With(EnemyKind.Meatball, 1, 2) },
        };

        /// <summary>
        /// load_level(setting, objective, composition): the level its three numbered parts make, on top of
        /// <paramref name="before"/> (what the globals held; setup_level's reset when null). A number with no part of
        /// its own (load_level knows only 0 and 1) leaves that half as it was. Nothing in the original calls load_level.
        /// </summary>
        public LevelSetting LoadLevel(int setting, int objective, int composition, LevelSetting before = null)
        {
            var level = new LevelSetting(0, "load_level", before?.Name ?? "", before?.Room ?? RoomId.GameScreen, before?.Background ?? "", before?.Overlay ?? "",
                before?.HSpeed ?? 0, before?.VSpeed ?? 0, before?.Tiled ?? false, before?.LoopHorizontal ?? false, before?.LoopVertical ?? false,
                before?.RandomizeOnLoop ?? false)
            {
                StationaryPercent = LevelSetting.KeepsStationaryPercent,
                Objective = before?.Objective ?? new LevelObjectiveDefinition(),
            };
            if (before?.Weather != null) level.Weather = (WeatherKind[])before.Weather.Clone();

            if (SettingParts != null && setting >= 0 && setting < SettingParts.Length && SettingParts[setting] != null)
            {
                var part = SettingParts[setting];
                level.Room = part.Room;
                level.Background = part.Background;
                level.HSpeed = part.HSpeed;
                level.VSpeed = part.VSpeed;
                level.LoopHorizontal = part.LoopHorizontal;
                level.LoopVertical = part.LoopVertical;
                level.RandomizeOnLoop = part.RandomizeOnLoop;
            }
            if (ObjectiveParts != null && objective >= 0 && objective < ObjectiveParts.Length && ObjectiveParts[objective] != null)
            {
                level.Objective = ObjectiveParts[objective];
                if (!string.IsNullOrEmpty(level.Objective.Name)) level.Name = level.Objective.Name;
            }
            if (CompositionParts != null && composition >= 0 && composition < CompositionParts.Length && CompositionParts[composition] != null)
            {
                var part = CompositionParts[composition];
                level.RockMin = part.RockMin;
                level.RockMax = part.RockMax;
                level.StructureMin = part.StructureMin;
                level.StructureMax = part.StructureMax;
                level.Enemies = part.Enemies.Clone();
            }
            return level;
        }

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
            ValidationHelpers.Required(errors, path + ".LevelStartSound", LevelStartSound);
            ValidationHelpers.Positive(errors, path + ".LevelStartFadeDelaySteps", LevelStartFadeDelaySteps);
            ValidationHelpers.Positive(errors, path + ".LevelStartFadeRateSteps", LevelStartFadeRateSteps);
            ValidationHelpers.Positive(errors, path + ".LevelStartFadeStep", LevelStartFadeStep);
            ValidationHelpers.NonNegative(errors, path + ".LevelStartOutline", LevelStartOutline);
            ValidationHelpers.Required(errors, path + ".TimeLimitBeepSound", TimeLimitBeepSound);

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

            if (StartingStationaryPercent < 0 || StartingStationaryPercent > 100)
                errors.Add($"{path}.StartingStationaryPercent: must be 0-100 (was {StartingStationaryPercent}).");
            ValidateParts(errors, path + ".SettingParts", SettingParts, (p, e, at) => p.Validate(e, at));
            ValidateParts(errors, path + ".ObjectiveParts", ObjectiveParts, (p, e, at) => p.Validate(e, at));
            ValidateParts(errors, path + ".CompositionParts", CompositionParts, (p, e, at) => p.Validate(e, at));
        }

        static void ValidateParts<T>(List<string> errors, string path, T[] parts, Action<T, List<string>, string> validate) where T : class
        {
            if (parts == null) { errors.Add(path + ": must not be null (an empty list for none)."); return; }
            for (int i = 0; i < parts.Length; i++)
            {
                string entryPath = $"{path}[{i}]";
                if (parts[i] == null) errors.Add(entryPath + ": must not be null.");
                else validate(parts[i], errors, entryPath);
            }
        }
    }
}
