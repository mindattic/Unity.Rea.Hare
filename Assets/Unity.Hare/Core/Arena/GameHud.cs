using System;
using System.Collections.Generic;
using System.Globalization;
using Unity.Hare.Content;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Arena
{
    /// <summary>
    /// The four HUD objects obj_game's Create makes (U.HARE-T25): obj_healthbar, obj_weapon_gui, obj_energybar and
    /// obj_speedometer, their numbers and sprites as their Create and Draw GUI events have them. Every position is on the
    /// 1920x1080 GUI layer (view_wview x view_hview), measured from its bottom or right edge as the original measures it.
    /// The health pips' column, size and margin are <see cref="HealthTuning"/>'s (obj_losthealth shares them).
    /// </summary>
    [Serializable]
    public class HudDefinition
    {
        // ---- obj_healthbar ----

        /// <summary>Create: <c>column0 = 0</c>, where spr_extralives is drawn.</summary>
        public int Column0 = 0;
        /// <summary>Create: <c>column1 = 64</c>, the lives, clock and coins column.</summary>
        public int Column1 = 64;
        /// <summary>The extra-lives icon, drawn at <c>(column0, view_hview - sprite_get_height(spr_extralives))</c>.</summary>
        public string ExtraLivesSprite = "spr_extralives";
        /// <summary>spr_extralives' height (64x102, origin 0,0).</summary>
        public int ExtraLivesHeight = 102;
        /// <summary><c>draw_text_outline(column1, view_hview-108, 1, "x " + string(LIVES))</c> in fnt_score.</summary>
        public int LivesOffsetY = 108;
        public string LivesPrefix = "x ";
        /// <summary><c>draw_text_outline(column1, view_hview-50, 1, string(strTime))</c> in fnt_point.</summary>
        public int ClockOffsetY = 50;
        /// <summary><c>draw_text_outline(column1+22, view_hview-27, 1, string(COINS))</c> in fnt_point.</summary>
        public int CoinsOffsetX = 22;
        public int CoinsOffsetY = 27;
        /// <summary><c>draw_sprite_ext(spr_coin_small, 0, column1, view_hview-23, ...)</c>.</summary>
        public string CoinSprite = "spr_coin_small";
        public int CoinIconOffsetY = 23;
        /// <summary>A full pip, and an empty one: spr_health_pip_back at 0.95 alpha under spr_health_pip_empty.</summary>
        public string PipSprite = "spr_health_pip";
        public string PipBackSprite = "spr_health_pip_back";
        public string PipEmptySprite = "spr_health_pip_empty";
        public float PipBackAlpha = 0.95f;
        /// <summary>Every HUD line's draw_text_outline size.</summary>
        public int Outline = 1;

        // ---- obj_energybar ----

        /// <summary>Create: <c>column2 = 145; x = column2; y = view_hview - sprite_height - 3</c> (spr_energybar_fill, 254x30).</summary>
        public int EnergyX = 145;
        public int EnergyBottomMargin = 3;
        public int EnergyWidth = 254;
        public int EnergyHeight = 30;
        public string EnergyBackSprite = "spr_energybar_back";
        public string EnergyFillSprite = "spr_energybar_fill";
        public string EnergyCoverSprite = "spr_energybar_cover";
        /// <summary>Draw GUI: <c>draw_set_alpha(0.95); draw_sprite(spr_energybar_back, ...)</c>.</summary>
        public float EnergyBackAlpha = 0.95f;
        /// <summary>Create: <c>energyY = y + 4</c>, the percentage's top (centred on <c>x + sprite_width/2</c>).</summary>
        public int EnergyTextOffsetY = 4;
        public string EnergyTextSuffix = "%";
        /// <summary>Create's <c>image_blend = c_green</c>, until the first End Step.</summary>
        public string EnergyStartColor = "#008000";
        /// <summary>End Step: c_red at or below 0.33 of maxValue, c_yellow at or below 0.66, else globals.gml's c_lightgreen (168, 157, 4).</summary>
        public float EnergyLowFraction = 0.33f;
        public float EnergyMidFraction = 0.66f;
        public string EnergyLowColor = "#FF0000";
        public string EnergyMidColor = "#FFFF00";
        public string EnergyHighColor = "#A89D04";

        // ---- obj_weapon_gui ----

        /// <summary>Create: <c>buffer = 20</c>; spr_icon_vulcan's size (every icon and spr_weapon_select are 52x57, origin 0,0).</summary>
        public int WeaponBuffer = 20;
        public int WeaponIconWidth = 52;
        public int WeaponIconHeight = 57;
        public string WeaponSelectSprite = "spr_weapon_select";
        /// <summary>next_ground_weapon's ground_asset for each ground primary, in <see cref="GroundWeapon"/> order.</summary>
        public string[] GroundIcons = { "spr_icon_vulcan", "spr_icon_laser", "spr_icon_railgun", "spr_icon_shotgun", "spr_icon_flamer" };
        /// <summary>next_air_weapon's air_asset for each air weapon, in <see cref="AirWeapon"/> order.</summary>
        public string[] AirIcons = { "spr_icon_rocket", "spr_icon_mine" };
        /// <summary>Create: <c>ground_asset_secondary = spr_icon_shotgun; air_asset_secondary = spr_icon_mine</c>.</summary>
        public string GroundSecondaryIcon = "spr_icon_shotgun";
        public string AirSecondaryIcon = "spr_icon_mine";
        public string AmmoSeparator = " / ";

        // ---- obj_speedometer ----

        /// <summary>Create: <c>speed_coefficent = 33.79</c>; metric divides by 0.62137.</summary>
        public float SpeedCoefficient = 33.79f;
        public float MetricDivisor = 0.62137f;
        /// <summary>Step while perched: <c>if(CurrentSpeed &gt; 1) CurrentSpeed *= 0.95; else CurrentSpeed = 0</c>.</summary>
        public float SpeedDecay = 0.95f;
        public float SpeedFloor = 1f;
        /// <summary>Create: <c>x = view_wview / 2; y = view_hview - 60; unitY = view_hview - 25</c>.</summary>
        public int SpeedOffsetY = 60;
        public int SpeedUnitOffsetY = 25;
        public string ImperialUnit = " mph";
        public string MetricUnit = " kph";

        // ---- obj_game's Draw GUI (U.HARE-T59) ----

        /// <summary><c>draw_text_outline(view_wview - 200, view_hview - 90, 1, string(current_score))</c>, fa_right, fnt_score.</summary>
        public int ScoreOffsetX = 200;
        public int ScoreOffsetY = 90;
        /// <summary><c>draw_text_outline(view_wview - 200, view_hview - 35, 1, strObjective)</c>, fa_right, fnt_point.</summary>
        public int ObjectiveOffsetY = 35;
        /// <summary>The debug overlay's column (<c>draw_text_outline(20, ...)</c>, fa_left, fnt_point).</summary>
        public int DebugX = 20;
        /// <summary>"FPS: " at y 20.</summary>
        public int DebugFpsY = 20;
        /// <summary>The ship's lines, from "X,Y: " at y 60 down to "Current Rock Spin: " at y 200, 20 apart.</summary>
        public int DebugPlayerY = 60;
        public int DebugLineHeight = 20;
        /// <summary>"Current Track: " at y 300.</summary>
        public int DebugTrackY = 300;
        /// <summary>
        /// Each debug line's caption, in draw order: FPS, X,Y, Direction, Image Angle, State, Spin, Current Rock, Current
        /// Rock X,Y, Current Rock Spin, Current Track (the original's own spellings, "Spin : " with its space).
        /// </summary>
        public string[] DebugLabels =
        {
            "FPS: ", "X,Y: ", "Direction: ", "Image Angle: ", "State: ", "Spin : ", "Current Rock: ", "Current Rock X,Y: ",
            "Current Rock Spin: ", "Current Track: "
        };
        /// <summary>What the rock lines show without a current_rock: <c>string("Null")</c>.</summary>
        public string DebugNone = "Null";
        /// <summary>get_state_string for state_idle, state_moving, state_dead and state_leave, in <see cref="PlayerFlightState"/> order.</summary>
        public string[] DebugStates = { "Idle", "Moving", "Dead", "Leaving" };

        internal void Validate(List<string> errors, string path)
        {
            if (DebugLabels == null || DebugLabels.Length != DebugLabelCount)
                errors.Add($"{path}.DebugLabels: needs {DebugLabelCount} captions.");
            if (DebugStates == null || DebugStates.Length != Enum.GetValues(typeof(PlayerFlightState)).Length)
                errors.Add($"{path}.DebugStates: needs one name per flight state ({Enum.GetValues(typeof(PlayerFlightState)).Length}).");
            if (DebugNone == null) errors.Add(path + ".DebugNone: must not be null.");
            ValidationHelpers.Positive(errors, path + ".DebugLineHeight", DebugLineHeight);
            ValidationHelpers.Required(errors, path + ".ExtraLivesSprite", ExtraLivesSprite);
            ValidationHelpers.Required(errors, path + ".CoinSprite", CoinSprite);
            ValidationHelpers.Required(errors, path + ".PipSprite", PipSprite);
            ValidationHelpers.Required(errors, path + ".PipBackSprite", PipBackSprite);
            ValidationHelpers.Required(errors, path + ".PipEmptySprite", PipEmptySprite);
            ValidationHelpers.Required(errors, path + ".EnergyBackSprite", EnergyBackSprite);
            ValidationHelpers.Required(errors, path + ".EnergyFillSprite", EnergyFillSprite);
            ValidationHelpers.Required(errors, path + ".EnergyCoverSprite", EnergyCoverSprite);
            ValidationHelpers.Required(errors, path + ".WeaponSelectSprite", WeaponSelectSprite);
            ValidationHelpers.Required(errors, path + ".GroundSecondaryIcon", GroundSecondaryIcon);
            ValidationHelpers.Required(errors, path + ".AirSecondaryIcon", AirSecondaryIcon);
            ValidationHelpers.NonNegative(errors, path + ".Outline", Outline);
            ValidationHelpers.Positive(errors, path + ".EnergyWidth", EnergyWidth);
            ValidationHelpers.Positive(errors, path + ".EnergyHeight", EnergyHeight);
            ValidationHelpers.Positive(errors, path + ".WeaponIconWidth", WeaponIconWidth);
            ValidationHelpers.Positive(errors, path + ".WeaponIconHeight", WeaponIconHeight);
            ValidationHelpers.Positive(errors, path + ".SpeedCoefficient", SpeedCoefficient);
            ValidationHelpers.Positive(errors, path + ".MetricDivisor", MetricDivisor);
            if (PipBackAlpha < 0f || PipBackAlpha > 1f) errors.Add($"{path}.PipBackAlpha: must be 0-1 (was {PipBackAlpha}).");
            if (EnergyBackAlpha < 0f || EnergyBackAlpha > 1f) errors.Add($"{path}.EnergyBackAlpha: must be 0-1 (was {EnergyBackAlpha}).");
            if (SpeedDecay < 0f || SpeedDecay >= 1f) errors.Add($"{path}.SpeedDecay: must be at least 0 and below 1 (was {SpeedDecay}).");
            if (!(EnergyLowFraction < EnergyMidFraction)) errors.Add($"{path}.EnergyLowFraction: must be below EnergyMidFraction.");
            if (GroundIcons == null || GroundIcons.Length != Enum.GetValues(typeof(GroundWeapon)).Length)
                errors.Add($"{path}.GroundIcons: needs one icon per ground weapon ({Enum.GetValues(typeof(GroundWeapon)).Length}).");
            if (AirIcons == null || AirIcons.Length != Enum.GetValues(typeof(AirWeapon)).Length)
                errors.Add($"{path}.AirIcons: needs one icon per air weapon ({Enum.GetValues(typeof(AirWeapon)).Length}).");
        }

        /// <summary>ground_asset for <paramref name="weapon"/> (Create's spr_icon_vulcan when the ship holds none).</summary>
        public string GroundIcon(GroundWeapon? weapon) => GroundIcons[(int)(weapon ?? GroundWeapon.Vulcan)];

        /// <summary>air_asset for <paramref name="weapon"/>.</summary>
        public string AirIcon(AirWeapon weapon) => AirIcons[(int)weapon];

        /// <summary>How many captions <see cref="DebugLabels"/> holds.</summary>
        public const int DebugLabelCount = 10;
    }

    /// <summary>GameMaker's string() of a number: a whole number prints bare, anything else rounded to two places.</summary>
    public static class GmlText
    {
        public static string Real(double value)
        {
            if (Math.Abs(value - Math.Round(value)) < 1e-9) return Math.Round(value).ToString("0", CultureInfo.InvariantCulture);
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>obj_energybar's image_blend.</summary>
    public enum EnergyShade
    {
        /// <summary>Create's c_green, before the first End Step.</summary>
        Start,
        Low,
        Mid,
        High
    }

    /// <summary>
    /// obj_energybar's End Step, engine-free: unless paused or the ship is dead, <c>image_xscale = currentValue /
    /// maxValue</c> and the blend by thirds (c_red, c_yellow, c_lightgreen). The bar's value is the ship's energy, which
    /// boost, brake and the shield drain and which refills on its own (<see cref="ShipFlight.Energy"/>); the percentage its
    /// Draw GUI prints (<c>string(ceil(currentValue)) + "%"</c>) is read every frame, paused or not.
    /// </summary>
    public sealed class EnergyGauge
    {
        readonly HudDefinition definition;

        public EnergyGauge(HudDefinition definition) { this.definition = definition ?? new HudDefinition(); }

        /// <summary>GML image_xscale: how much of spr_energybar_fill is drawn, 0..1.</summary>
        public float Fraction { get; private set; } = 1f;
        public EnergyShade Shade { get; private set; } = EnergyShade.Start;

        /// <summary>One End Step with the bar at <paramref name="energy"/> of <paramref name="maxEnergy"/>.</summary>
        public void Step(float energy, float maxEnergy, bool paused, bool dead)
        {
            if (paused || dead || maxEnergy <= 0f) return;
            float value = Math.Max(0f, Math.Min(maxEnergy, energy));
            Fraction = value / maxEnergy;
            if (value <= maxEnergy * definition.EnergyLowFraction) Shade = EnergyShade.Low;
            else if (value <= maxEnergy * definition.EnergyMidFraction) Shade = EnergyShade.Mid;
            else Shade = EnergyShade.High;
        }

        /// <summary>The Draw GUI's percentage for <paramref name="energy"/>.</summary>
        public string Text(float energy) => GmlText.Int((int)Math.Ceiling(Math.Max(0f, energy) - 1e-4f)) + definition.EnergyTextSuffix;

        /// <summary>The colour <see cref="Shade"/> blends the fill with, as #RRGGBB.</summary>
        public string Color
        {
            get
            {
                switch (Shade)
                {
                    case EnergyShade.Low: return definition.EnergyLowColor;
                    case EnergyShade.Mid: return definition.EnergyMidColor;
                    case EnergyShade.High: return definition.EnergyHighColor;
                    default: return definition.EnergyStartColor;
                }
            }
        }
    }

    /// <summary>
    /// obj_speedometer's Step, engine-free: while the ship is not perched, <c>CurrentSpeed = player.speed *
    /// speed_coefficent</c> (divided by 0.62137 in kph when use_metric); perched, it runs down by 0.95 a step until it is
    /// not above 1, then 0. Its Draw GUI draws only while <c>CurrentSpeed &gt; 0</c>, the number as string() prints it.
    /// </summary>
    public sealed class Speedometer
    {
        readonly HudDefinition definition;

        public Speedometer(HudDefinition definition)
        {
            this.definition = definition ?? new HudDefinition();
            Unit = this.definition.ImperialUnit;
        }

        /// <summary>GML CurrentSpeed.</summary>
        public double CurrentSpeed { get; private set; }

        /// <summary>GML UnitString (" mph" from Create).</summary>
        public string Unit { get; private set; }

        public bool Visible => CurrentSpeed > 0;

        public string Text => GmlText.Real(CurrentSpeed);

        /// <summary>One Step. <paramref name="speed"/> is obj_player's speed (px a step, as <see cref="ShipFlight.Speed"/> holds it).</summary>
        public void Step(PlayerFlightState state, float speed, bool useMetric)
        {
            if (state != PlayerFlightState.Idle)
            {
                if (useMetric)
                {
                    CurrentSpeed = speed * (double)definition.SpeedCoefficient / definition.MetricDivisor;
                    Unit = definition.MetricUnit;
                }
                else
                {
                    CurrentSpeed = speed * (double)definition.SpeedCoefficient;
                    Unit = definition.ImperialUnit;
                }
            }
            else if (CurrentSpeed > definition.SpeedFloor) CurrentSpeed *= definition.SpeedDecay;
            else CurrentSpeed = 0;
        }
    }

    /// <summary>Which font a HUD line is drawn in.</summary>
    public enum HudFont
    {
        /// <summary>fnt_score.</summary>
        Score,
        /// <summary>fnt_point.</summary>
        Point,
        /// <summary>fnt_small.</summary>
        Small
    }

    /// <summary>
    /// One draw_sprite_ext of a HUD object's Draw GUI (every HUD sprite's origin is its top left). A NaN
    /// <see cref="Width"/> or <see cref="Height"/> is the sprite's own size.
    /// </summary>
    public readonly struct HudSprite
    {
        public readonly string Key, Sprite;
        public readonly float X, Y, Width, Height, Alpha;
        /// <summary>image_blend as #RRGGBB, white for none.</summary>
        public readonly string Color;

        public HudSprite(string key, string sprite, float x, float y, float width, float height, float alpha = 1f, string color = "#FFFFFF")
        {
            Key = key;
            Sprite = sprite;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            Alpha = alpha;
            Color = color;
        }

        public override string ToString() => Key + ": " + Sprite + " at " + X + "," + Y;
    }

    /// <summary>One draw_text_outline of a HUD object's Draw GUI: its top at <see cref="Y"/>, hung off <see cref="X"/> by <see cref="Align"/>.</summary>
    public readonly struct HudText
    {
        public readonly string Key, Text;
        public readonly float X, Y;
        public readonly ResultsAlign Align;
        public readonly HudFont Font;

        public HudText(string key, string text, float x, float y, ResultsAlign align, HudFont font)
        {
            Key = key;
            Text = text ?? "";
            X = x;
            Y = y;
            Align = align;
            Font = font;
        }

        public override string ToString() => Key + ": " + Text;
    }

    /// <summary>What the HUD reads from the game each frame (GML: player, LIVES, COINS, strTime, use_metric, paused).</summary>
    public struct HudReading
    {
        /// <summary>Whether there is a ship (GML: the player instance exists).</summary>
        public bool HasPlayer;
        public PlayerFlightState State;
        public float Speed;
        public float Energy;
        public float MaxEnergy;
        public int Health;
        public int MaxHealth;
        public int Lives;
        public int Coins;
        /// <summary>obj_healthbar's strTime.</summary>
        public string Clock;
        public bool UseMetric;
        public bool Paused;
        /// <summary>keyboard_check(vk_control).</summary>
        public bool ControlHeld;
        public GroundWeapon? GroundWeapon;
        public AirWeapon AirWeapon;
        public int GroundPrimaryAmmo, GroundPrimaryMax, GroundSecondaryAmmo, GroundSecondaryMax;
        public int AirPrimaryAmmo, AirPrimaryMax, AirSecondaryAmmo, AirSecondaryMax;

        // ---- obj_game's Draw GUI (U.HARE-T59) ----

        /// <summary>current_score.</summary>
        public int Score;
        /// <summary>strObjective.</summary>
        public string Objective;
        /// <summary>debug: whether the F12 overlay is drawn.</summary>
        public bool Debug;
        /// <summary>GML fps: frames drawn in the last second.</summary>
        public int Fps;
        /// <summary>player.x and player.y, room pixels.</summary>
        public float X, Y;
        /// <summary>player.direction and player.image_angle, degrees.</summary>
        public float Direction, ImageAngle;
        /// <summary>player.spin, degrees a step.</summary>
        public float Spin;
        /// <summary>player.current_rock's id, or null for noone.</summary>
        public int? Rock;
        /// <summary>current_rock.x, current_rock.y (room pixels) and current_rock.spin (degrees a step).</summary>
        public float RockX, RockY, RockSpin;
        /// <summary>track_name.</summary>
        public string TrackName;
    }

    /// <summary>The HUD as drawn this frame: every sprite and line, in the original's draw order.</summary>
    public sealed class HudFrame
    {
        public readonly List<HudSprite> Sprites = new List<HudSprite>();
        public readonly List<HudText> Texts = new List<HudText>();

        public HudSprite? Sprite(string key)
        {
            foreach (var s in Sprites) if (s.Key == key) return s;
            return null;
        }

        public HudText? Text(string key)
        {
            foreach (var t in Texts) if (t.Key == key) return t;
            return null;
        }
    }

    /// <summary>
    /// The gameplay HUD (U.HARE-T25), engine-free: obj_healthbar (spr_extralives and "x LIVES", strTime, COINS beside
    /// spr_coin_small, and one pip per point of MAXHP, full or empty), obj_weapon_gui (the perched or flying ship's
    /// primary and secondary weapon icons, spr_weapon_select on the primary, or the secondary while Ctrl is held, and each
    /// pool's "ammo / max"), obj_energybar (back at 0.95, the fill scaled and blended, the cover, and the percentage) and
    /// obj_speedometer. <see cref="Step"/> is their Step and End Step events, run once a room step; <see cref="Draw"/> is
    /// their Draw GUI events, in obj_game's creation order (healthbar, weapon GUI, energy bar, speedometer).
    /// </summary>
    public sealed class GameHud
    {
        /// <summary>A sprite drawn at its own size.</summary>
        const float N = float.NaN;

        readonly HealthTuning health;

        public GameHud(HudDefinition definition, HealthTuning health, int guiWidth, int guiHeight)
        {
            Definition = definition ?? new HudDefinition();
            this.health = health ?? new HealthTuning();
            GuiWidth = guiWidth;
            GuiHeight = guiHeight;
            Energy = new EnergyGauge(Definition);
            Speedometer = new Speedometer(Definition);
        }

        public HudDefinition Definition { get; }
        /// <summary>view_wview and view_hview.</summary>
        public int GuiWidth { get; }
        public int GuiHeight { get; }
        public EnergyGauge Energy { get; }
        public Speedometer Speedometer { get; }

        /// <summary>One room step: obj_speedometer's Step and obj_energybar's End Step (nothing without a ship).</summary>
        public void Step(HudReading reading)
        {
            if (!reading.HasPlayer) return;
            Speedometer.Step(reading.State, reading.Speed, reading.UseMetric);
            Energy.Step(reading.Energy, reading.MaxEnergy, reading.Paused, reading.State == PlayerFlightState.Dead);
        }

        /// <summary>
        /// The Draw GUI events for <paramref name="reading"/>: obj_game's own (it is in the room before its Create makes the
        /// other four, so it draws first), then the four HUD objects'.
        /// </summary>
        public HudFrame Draw(HudReading reading)
        {
            var frame = new HudFrame();
            DrawGame(frame, reading);
            DrawHealthBar(frame, reading);
            DrawWeaponGui(frame, reading);
            DrawEnergyBar(frame, reading);
            DrawSpeedometer(frame);
            return frame;
        }

        /// <summary>
        /// obj_game's Draw GUI (U.HARE-T59): the score (fnt_score) and strObjective (fnt_point), right-aligned 200 px in
        /// from the right edge at the bottom; with debug on, FPS, then the ship's position, direction, image_angle,
        /// get_state_string, spin and current rock (its id, position and spin, or "Null"), then track_name, down the left.
        /// </summary>
        void DrawGame(HudFrame frame, HudReading reading)
        {
            var d = Definition;
            float right = GuiWidth - d.ScoreOffsetX;
            frame.Texts.Add(new HudText("score", GmlText.Int(reading.Score), right, GuiHeight - d.ScoreOffsetY, ResultsAlign.Right, HudFont.Score));
            frame.Texts.Add(new HudText("objective", reading.Objective ?? "", right, GuiHeight - d.ObjectiveOffsetY, ResultsAlign.Right, HudFont.Point));
            if (!reading.Debug) return;

            var labels = d.DebugLabels;
            void Line(string key, int label, string value, float y) =>
                frame.Texts.Add(new HudText(key, labels[label] + value, d.DebugX, y, ResultsAlign.Left, HudFont.Point));

            Line("debug-fps", 0, GmlText.Int(reading.Fps), d.DebugFpsY);
            if (reading.HasPlayer)
            {
                float y = d.DebugPlayerY;
                float step = d.DebugLineHeight;
                int state = (int)reading.State;
                Line("debug-position", 1, GmlText.Real(reading.X) + "," + GmlText.Real(reading.Y), y);
                Line("debug-direction", 2, GmlText.Real(reading.Direction), y + step);
                Line("debug-angle", 3, GmlText.Real(reading.ImageAngle), y + step * 2);
                Line("debug-state", 4, state >= 0 && state < d.DebugStates.Length ? d.DebugStates[state] : "", y + step * 3);
                Line("debug-spin", 5, GmlText.Real(reading.Spin), y + step * 4);
                if (reading.Rock.HasValue)
                {
                    Line("debug-rock", 6, GmlText.Int(reading.Rock.Value), y + step * 5);
                    Line("debug-rock-position", 7, GmlText.Real(reading.RockX) + "," + GmlText.Real(reading.RockY), y + step * 6);
                    Line("debug-rock-spin", 8, GmlText.Real(reading.RockSpin), y + step * 7);
                }
                else
                {
                    Line("debug-rock", 6, d.DebugNone, y + step * 5);
                    Line("debug-rock-position", 7, d.DebugNone, y + step * 6);
                    Line("debug-rock-spin", 8, d.DebugNone, y + step * 7);
                }
            }
            Line("debug-track", 9, reading.TrackName ?? "", d.DebugTrackY);
        }

        void DrawHealthBar(HudFrame frame, HudReading reading)
        {
            var d = Definition;
            int h = GuiHeight;
            frame.Sprites.Add(new HudSprite("extralives", d.ExtraLivesSprite, d.Column0, h - d.ExtraLivesHeight, N, N));
            frame.Texts.Add(new HudText("lives", d.LivesPrefix + GmlText.Int(reading.Lives), d.Column1, h - d.LivesOffsetY, ResultsAlign.Left, HudFont.Score));
            frame.Texts.Add(new HudText("clock", reading.Clock ?? "00:00", d.Column1, h - d.ClockOffsetY, ResultsAlign.Left, HudFont.Point));
            frame.Texts.Add(new HudText("coins", GmlText.Int(reading.Coins), d.Column1 + d.CoinsOffsetX, h - d.CoinsOffsetY, ResultsAlign.Left, HudFont.Point));
            frame.Sprites.Add(new HudSprite("coin", d.CoinSprite, d.Column1, h - d.CoinIconOffsetY, N, N));
            if (!reading.HasPlayer) return;

            float pipY = h - health.LostHealthPipHeight - health.LostHealthBottomMargin;
            for (int i = 0; i < reading.MaxHealth; i++)
            {
                float x = health.LostHealthColumnX + i * health.LostHealthPipWidth;
                if (reading.Health >= i + 1)
                    frame.Sprites.Add(new HudSprite("pip" + i, d.PipSprite, x, pipY, N, N));
                else
                {
                    frame.Sprites.Add(new HudSprite("pip" + i + "-back", d.PipBackSprite, x, pipY, N, N, d.PipBackAlpha));
                    frame.Sprites.Add(new HudSprite("pip" + i, d.PipEmptySprite, x, pipY, N, N));
                }
            }
        }

        void DrawWeaponGui(HudFrame frame, HudReading reading)
        {
            if (!reading.HasPlayer) return;
            bool ground = reading.State == PlayerFlightState.Idle;
            if (!ground && reading.State != PlayerFlightState.Moving) return; // only state_idle and state_moving draw

            var d = Definition;
            float primaryX = GuiWidth - d.WeaponIconWidth * 2 - d.WeaponBuffer * 2;
            float secondaryX = GuiWidth - d.WeaponIconWidth - d.WeaponBuffer;
            float iconY = GuiHeight - d.WeaponIconHeight - d.WeaponBuffer;
            string primary = ground ? d.GroundIcon(reading.GroundWeapon) : d.AirIcon(reading.AirWeapon);
            string secondary = ground ? d.GroundSecondaryIcon : d.AirSecondaryIcon;

            frame.Sprites.Add(new HudSprite("weapon-primary", primary, primaryX, iconY, N, N));
            if (!reading.ControlHeld) frame.Sprites.Add(new HudSprite("weapon-select", d.WeaponSelectSprite, primaryX, iconY, N, N));
            frame.Sprites.Add(new HudSprite("weapon-secondary", secondary, secondaryX, iconY, N, N));
            if (reading.ControlHeld) frame.Sprites.Add(new HudSprite("weapon-select", d.WeaponSelectSprite, secondaryX, iconY, N, N));

            int primaryAmmo = ground ? reading.GroundPrimaryAmmo : reading.AirPrimaryAmmo;
            int primaryMax = ground ? reading.GroundPrimaryMax : reading.AirPrimaryMax;
            int secondaryAmmo = ground ? reading.GroundSecondaryAmmo : reading.AirSecondaryAmmo;
            int secondaryMax = ground ? reading.GroundSecondaryMax : reading.AirSecondaryMax;
            float textY = GuiHeight - d.WeaponBuffer;
            frame.Texts.Add(new HudText("ammo-primary", GmlText.Int(primaryAmmo) + d.AmmoSeparator + GmlText.Int(primaryMax),
                GuiWidth - d.WeaponIconWidth - d.WeaponBuffer * 2, textY, ResultsAlign.Right, HudFont.Small));
            frame.Texts.Add(new HudText("ammo-secondary", GmlText.Int(secondaryAmmo) + d.AmmoSeparator + GmlText.Int(secondaryMax),
                GuiWidth - d.WeaponBuffer, textY, ResultsAlign.Right, HudFont.Small));
        }

        void DrawEnergyBar(HudFrame frame, HudReading reading)
        {
            var d = Definition;
            float y = GuiHeight - d.EnergyHeight - d.EnergyBottomMargin;
            frame.Sprites.Add(new HudSprite("energy-back", d.EnergyBackSprite, d.EnergyX, y, d.EnergyWidth, d.EnergyHeight, d.EnergyBackAlpha));
            frame.Sprites.Add(new HudSprite("energy-fill", d.EnergyFillSprite, d.EnergyX, y, d.EnergyWidth * Energy.Fraction, d.EnergyHeight, 1f, Energy.Color));
            frame.Sprites.Add(new HudSprite("energy-cover", d.EnergyCoverSprite, d.EnergyX, y, d.EnergyWidth, d.EnergyHeight));
            float energy = reading.HasPlayer ? reading.Energy : reading.MaxEnergy;
            frame.Texts.Add(new HudText("energy", Energy.Text(energy), d.EnergyX + d.EnergyWidth / 2f, y + d.EnergyTextOffsetY, ResultsAlign.Center, HudFont.Point));
        }

        void DrawSpeedometer(HudFrame frame)
        {
            if (!Speedometer.Visible) return;
            var d = Definition;
            float x = GuiWidth / 2f;
            frame.Texts.Add(new HudText("speed", Speedometer.Text, x, GuiHeight - d.SpeedOffsetY, ResultsAlign.Right, HudFont.Score));
            frame.Texts.Add(new HudText("speed-unit", Speedometer.Unit, x, GuiHeight - d.SpeedUnitOffsetY, ResultsAlign.Left, HudFont.Point));
        }
    }
}
