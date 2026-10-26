using System;
using System.Collections.Generic;
using Unity.Hare.Content;

namespace Unity.Hare.Rooms
{
    /// <summary>
    /// One background of room_title and room_intro, as their .room.gmx files list it: placed at (0, 0), tiled both ways,
    /// moving <see cref="VSpeed"/> pixels a step.
    /// </summary>
    [Serializable]
    public class DropBackground
    {
        /// <summary>The GML background's name.</summary>
        public string Name;
        /// <summary>GML: background_hspeed[i] as the room starts it, pixels a step.</summary>
        public int HSpeed;
        /// <summary>GML: background_vspeed[i] as the room starts it, pixels a step (negative scrolls up).</summary>
        public int VSpeed;

        public DropBackground() { }

        public DropBackground(string name, int hspeed, int vspeed)
        {
            Name = name;
            HSpeed = hspeed;
            VSpeed = vspeed;
        }

        internal void Validate(List<string> errors, string path) => ValidationHelpers.Required(errors, path + ".Name", Name);
    }

    /// <summary>
    /// The falling rabbit of room_title and room_intro as data: obj_drop_title / obj_drop_intro (spr_drop, placed at
    /// 960,-608 at half scale), obj_drop_emitter and the obj_drop_cloud it makes, and the two rooms' backgrounds
    /// (bg_nightsky scrolling up 5 px a step under a still bg_gradient3). The defaults are the original's, read from
    /// HARE.gmx; the title's drop also has Alarm 1 (<see cref="HangSteps"/>) and sfx_land, the intro's has neither.
    /// </summary>
    [Serializable]
    public class DropSceneTuning
    {
        /// <summary>The drop's sprite: spr_drop, 462x1154, origin 231,0 (the rabbit head down, its boots up).</summary>
        public string DropSprite = "spr_drop";
        /// <summary>Where the room places it (x, y) and at what scale (the room's instance scale 0.5).</summary>
        public int DropX = 960;
        public int DropY = -608;
        public double DropScale = 0.5;
        /// <summary>Create's and the exit's <c>vspeed = 10</c>.</summary>
        public int FallSpeed = 10;
        /// <summary>Step: <c>x = startX + irandom_range(-3,3)</c>, the shake.</summary>
        public int Jitter = 3;
        /// <summary>Phase "start" stops once <c>y &gt; room_height / 4</c>: the fraction of the room's height.</summary>
        public double HangFraction = 0.25;
        /// <summary>
        /// obj_drop_title's <c>alarm[1] = 200</c> when it stops, whose event sets <c>phase = "exit"</c>; 0 for obj_drop_intro,
        /// which has no Alarm 1 and waits for a key.
        /// </summary>
        public int HangSteps = 200;
        /// <summary>
        /// The smoke: every Step <c>effect_create_below(ef_smokeup, x, y+40, 2, c_orange)</c>, and Alarm 0, every
        /// <c>smoke_delay = 20</c> steps from Create, the same in c_black.
        /// </summary>
        public int SmokeOffsetY = 40;
        public int SmokeSize = 2;
        public int SmokeDelaySteps = 20;
        /// <summary>c_orange (GameMaker's 4235519) and c_black, as #RRGGBB.</summary>
        public string TrailColor = "#FFA040";
        public string PuffColor = "#000000";
        /// <summary>obj_drop_title, once below the room: <c>audio_play_sound(sfx_land,1,0)</c>; empty for obj_drop_intro.</summary>
        public string LandSound = "sfx_land";

        /// <summary>obj_drop_emitter: Create <c>alarm[0] = 1</c>; Alarm 0 makes <c>irandom_range(4,8)</c> obj_drop_cloud, re-armed every <c>rate = 20</c>.</summary>
        public int CloudFirstAlarmSteps = 1;
        public int CloudRateSteps = 20;
        public int CloudBatchMin = 4;
        public int CloudBatchMax = 8;
        /// <summary>obj_drop_cloud's sprites, <c>randomasset = irandom_range(1,4)</c>: spr_cloud1..4, each 851x313, origin 0,0.</summary>
        public string[] CloudSprites = { "spr_cloud1", "spr_cloud2", "spr_cloud3", "spr_cloud4" };
        /// <summary>sprite_width and sprite_height as Create reads them (still spr_cloud1's), and every cloud's height.</summary>
        public int CloudWidth = 851;
        public int CloudHeight = 313;
        /// <summary><c>vspeed = -irandom_range(60, 80)</c>: the clouds rush up past the falling rabbit.</summary>
        public int CloudSpeedMin = 60;
        public int CloudSpeedMax = 80;
        /// <summary><c>image_alpha = 0.1 * irandom_range(1, 3)</c>.</summary>
        public double CloudAlphaStep = 0.1;
        public int CloudAlphaMin = 1;
        public int CloudAlphaMax = 3;
        /// <summary><c>y = room_height + sprite_height * irandom_range(1, 2)</c>.</summary>
        public int CloudBelowMin = 1;
        public int CloudBelowMax = 2;

        /// <summary>The room's backgrounds, back to front: bg_nightsky (vspeed -5), then bg_gradient3 over it.</summary>
        public DropBackground[] Backgrounds =
        {
            new DropBackground("bg_nightsky", 0, -5),
            new DropBackground("bg_gradient3", 0, 0),
        };

        /// <summary>room_title's: obj_drop_title with its Alarm 1 and sfx_land.</summary>
        public static DropSceneTuning Title() => new DropSceneTuning();

        /// <summary>room_intro's: obj_drop_intro, which hangs until a key and plays nothing as it leaves.</summary>
        public static DropSceneTuning Intro() => new DropSceneTuning { HangSteps = 0, LandSound = "" };

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".DropSprite", DropSprite);
            if (!(DropScale > 0d)) errors.Add($"{path}.DropScale: must be positive (was {DropScale}).");
            ValidationHelpers.Positive(errors, path + ".FallSpeed", FallSpeed);
            ValidationHelpers.NonNegative(errors, path + ".Jitter", Jitter);
            if (!(HangFraction > 0d && HangFraction < 1d)) errors.Add($"{path}.HangFraction: must be within 0..1 (was {HangFraction}).");
            ValidationHelpers.NonNegative(errors, path + ".HangSteps", HangSteps);
            ValidationHelpers.NonNegative(errors, path + ".SmokeSize", SmokeSize);
            ValidationHelpers.Positive(errors, path + ".SmokeDelaySteps", SmokeDelaySteps);
            if (!StartupTuning.IsHexColor(TrailColor)) errors.Add($"{path}.TrailColor: must be #RRGGBB (was {TrailColor ?? "null"}).");
            if (!StartupTuning.IsHexColor(PuffColor)) errors.Add($"{path}.PuffColor: must be #RRGGBB (was {PuffColor ?? "null"}).");
            if (LandSound == null) errors.Add(path + ".LandSound: must not be null (empty plays nothing).");
            ValidationHelpers.Positive(errors, path + ".CloudFirstAlarmSteps", CloudFirstAlarmSteps);
            ValidationHelpers.Positive(errors, path + ".CloudRateSteps", CloudRateSteps);
            ValidationHelpers.NonNegative(errors, path + ".CloudBatchMin", CloudBatchMin);
            if (CloudBatchMax < CloudBatchMin) errors.Add($"{path}.CloudBatchMax: must not be below CloudBatchMin (was {CloudBatchMax}).");
            if (CloudSprites == null || CloudSprites.Length == 0) errors.Add(path + ".CloudSprites: must list at least one sprite.");
            else for (int i = 0; i < CloudSprites.Length; i++) ValidationHelpers.Required(errors, $"{path}.CloudSprites[{i}]", CloudSprites[i]);
            ValidationHelpers.Positive(errors, path + ".CloudWidth", CloudWidth);
            ValidationHelpers.Positive(errors, path + ".CloudHeight", CloudHeight);
            if (CloudSpeedMax < CloudSpeedMin) errors.Add($"{path}.CloudSpeedMax: must not be below CloudSpeedMin (was {CloudSpeedMax}).");
            if (!(CloudAlphaStep > 0d)) errors.Add($"{path}.CloudAlphaStep: must be positive (was {CloudAlphaStep}).");
            if (CloudAlphaMax < CloudAlphaMin) errors.Add($"{path}.CloudAlphaMax: must not be below CloudAlphaMin (was {CloudAlphaMax}).");
            if (CloudBelowMax < CloudBelowMin) errors.Add($"{path}.CloudBelowMax: must not be below CloudBelowMin (was {CloudBelowMax}).");
            if (Backgrounds == null) errors.Add(path + ".Backgrounds: must not be null.");
            else for (int i = 0; i < Backgrounds.Length; i++)
            {
                if (Backgrounds[i] == null) errors.Add($"{path}.Backgrounds[{i}]: must not be null.");
                else Backgrounds[i].Validate(errors, $"{path}.Backgrounds[{i}]");
            }
        }
    }
}
