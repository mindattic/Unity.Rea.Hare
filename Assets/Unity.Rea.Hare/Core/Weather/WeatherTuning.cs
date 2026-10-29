using System.Collections.Generic;
using Unity.Hare.Content;

namespace Unity.Hare.Weather
{
    /// <summary>
    /// The five weather emitters of gmlHARE (the content file's <c>weather</c> section): obj_cloud_emitter's
    /// obj_cloud, obj_rain_emitter's obj_raindrop, obj_snow_emitter's obj_snow, obj_sand_emitter's obj_sand and
    /// obj_fog_emitter's obj_fog. Distances are GML px (y down), speeds px a step, times steps.
    /// </summary>
    public enum WeatherKind
    {
        /// <summary>obj_cloud_emitter / obj_cloud: placed in every play room (and room_boss1) by the room itself.</summary>
        Cloud,
        /// <summary>obj_rain_emitter / obj_raindrop.</summary>
        Rain,
        /// <summary>obj_snow_emitter / obj_snow.</summary>
        Snow,
        /// <summary>obj_sand_emitter / obj_sand.</summary>
        Sand,
        /// <summary>obj_fog_emitter / obj_fog.</summary>
        Fog,
    }

    /// <summary>The weather's numbers, the original's by default.</summary>
    public sealed class WeatherTuning
    {
        // ---- obj_cloud_emitter / obj_cloud (depth 0) ----

        /// <summary>Create: <c>repeat(12)</c> clouds anywhere in the room.</summary>
        public int CloudInitialCount = 12;
        /// <summary>Create: <c>alarm[0] = 1</c>.</summary>
        public int CloudFirstAlarmSteps = 1;
        /// <summary>Alarm 0: <c>alarm[0] = rate</c>, <c>rate = 200</c>.</summary>
        public int CloudRateSteps = 200;
        /// <summary>Alarm 0: <c>repeat(irandom_range(1, 4))</c>.</summary>
        public int CloudBatchMin = 1;
        public int CloudBatchMax = 4;
        /// <summary>obj_cloud Create: <c>hspeed = -irandom_range(1, 2)</c>.</summary>
        public int CloudSpeedMin = 1;
        public int CloudSpeedMax = 2;
        /// <summary>obj_cloud Create: <c>image_alpha = 0.1 * irandom_range(1, 3)</c>.</summary>
        public float CloudAlphaStep = 0.1f;
        public int CloudAlphaMin = 1;
        public int CloudAlphaMax = 3;
        /// <summary><c>randomasset = irandom_range(1, 4)</c>: spr_cloud1..4 (each 851x313, origin 0,0).</summary>
        public string[] CloudSprites = { "spr_cloud1", "spr_cloud2", "spr_cloud3", "spr_cloud4" };
        public int CloudWidthPx = 851;
        public int CloudHeightPx = 313;

        // ---- obj_rain_emitter / obj_raindrop (depth -1) ----

        /// <summary>Step: one raindrop a step.</summary>
        public int RainPerStep = 1;
        public string RainSprite = "spr_rain";
        /// <summary>spr_rain's sprite_height (1x4).</summary>
        public int RainHeightPx = 4;
        /// <summary><c>dY = irandom_range(15, 20)</c>.</summary>
        public int RainFallMin = 15;
        public int RainFallMax = 20;
        /// <summary><c>dX = irandom_range(-1, -2)</c>.</summary>
        public int RainDriftMin = -2;
        public int RainDriftMax = -1;
        public float RainAlpha = 1f;

        // ---- obj_snow_emitter / obj_snow (depth -1) ----

        public int SnowPerStep = 1;
        /// <summary><c>choose(spr_snow1, spr_snow2)</c> (9x9, origin 4,4).</summary>
        public string[] SnowSprites = { "spr_snow1", "spr_snow2" };
        /// <summary>spr_snow1's sprite_height, read in Create before the sprite is chosen.</summary>
        public int SnowHeightPx = 9;
        /// <summary><c>dY = irandom_range(0.5, 2)</c>: GameMaker truncates the bounds, so 0, 1 or 2 ...</summary>
        public int SnowFallMin = 0;
        public int SnowFallMax = 2;
        /// <summary>... and a 0 falls at 0.5, the least the script asks for (BIBLE D16).</summary>
        public float SnowZeroFall = 0.5f;
        /// <summary><c>dX = irandom_range(-2, 2)</c>.</summary>
        public int SnowDriftMax = 2;
        public float SnowAlpha = 0.8f;
        /// <summary>Step: <c>image_angle -= 1</c> (snow and sand).</summary>
        public float SpinPerStep = -1f;

        // ---- obj_sand_emitter / obj_sand (depth -1) ----

        /// <summary>Step: two grains a step.</summary>
        public int SandPerStep = 2;
        /// <summary><c>choose(spr_sand1, spr_sand2)</c> (4x4, origin 2,2).</summary>
        public string[] SandSprites = { "spr_sand1", "spr_sand2" };
        public int SandWidthPx = 4;
        /// <summary><c>dY = irandom_range(-1, 6)</c>, and <c>if (dY == 0) dY = 2</c>.</summary>
        public int SandFallMin = -1;
        public int SandFallMax = 6;
        public int SandZeroFall = 2;
        /// <summary><c>dX = irandom_range(3, 6)</c>.</summary>
        public int SandDriftMin = 3;
        public int SandDriftMax = 6;
        public float SandAlpha = 0.8f;

        // ---- obj_fog_emitter / obj_fog (depth -1) ----

        /// <summary>Create: <c>repeat(32)</c> banks of fog.</summary>
        public int FogCount = 32;
        /// <summary>obj_fog uses the cloud sprites, <c>randomasset = irandom_range(1, 4)</c>.</summary>
        public float FogAlpha = 0.3f;
        /// <summary><c>dX = irandom_range(1, 2)</c> (negated for a bank drifting left).</summary>
        public int FogSpeedMin = 1;
        public int FogSpeedMax = 2;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.NonNegative(errors, path + ".CloudInitialCount", CloudInitialCount);
            ValidationHelpers.Positive(errors, path + ".CloudFirstAlarmSteps", CloudFirstAlarmSteps);
            ValidationHelpers.Positive(errors, path + ".CloudRateSteps", CloudRateSteps);
            Range(errors, path + ".CloudBatch", CloudBatchMin, CloudBatchMax);
            Range(errors, path + ".CloudSpeed", CloudSpeedMin, CloudSpeedMax);
            Range(errors, path + ".CloudAlpha", CloudAlphaMin, CloudAlphaMax);
            Sprites(errors, path + ".CloudSprites", CloudSprites);
            ValidationHelpers.Positive(errors, path + ".CloudWidthPx", CloudWidthPx);
            ValidationHelpers.Positive(errors, path + ".CloudHeightPx", CloudHeightPx);
            ValidationHelpers.NonNegative(errors, path + ".RainPerStep", RainPerStep);
            ValidationHelpers.Required(errors, path + ".RainSprite", RainSprite);
            Range(errors, path + ".RainFall", RainFallMin, RainFallMax);
            Range(errors, path + ".RainDrift", RainDriftMin, RainDriftMax);
            ValidationHelpers.NonNegative(errors, path + ".SnowPerStep", SnowPerStep);
            Sprites(errors, path + ".SnowSprites", SnowSprites);
            Range(errors, path + ".SnowFall", SnowFallMin, SnowFallMax);
            ValidationHelpers.Positive(errors, path + ".SnowZeroFall", SnowZeroFall);
            ValidationHelpers.NonNegative(errors, path + ".SnowDriftMax", SnowDriftMax);
            ValidationHelpers.NonNegative(errors, path + ".SandPerStep", SandPerStep);
            Sprites(errors, path + ".SandSprites", SandSprites);
            Range(errors, path + ".SandFall", SandFallMin, SandFallMax);
            Range(errors, path + ".SandDrift", SandDriftMin, SandDriftMax);
            ValidationHelpers.NonNegative(errors, path + ".FogCount", FogCount);
            Range(errors, path + ".FogSpeed", FogSpeedMin, FogSpeedMax);
            Alpha(errors, path + ".CloudAlphaStep", CloudAlphaStep * CloudAlphaMax);
            Alpha(errors, path + ".RainAlpha", RainAlpha);
            Alpha(errors, path + ".SnowAlpha", SnowAlpha);
            Alpha(errors, path + ".SandAlpha", SandAlpha);
            Alpha(errors, path + ".FogAlpha", FogAlpha);
        }

        static void Range(List<string> errors, string path, int min, int max)
        {
            if (max < min) errors.Add($"{path}Max: must be at least {path}Min ({min}) (was {max}).");
        }

        static void Sprites(List<string> errors, string path, string[] sprites)
        {
            if (sprites == null || sprites.Length == 0) { errors.Add(path + ": needs at least one sprite."); return; }
            for (int i = 0; i < sprites.Length; i++) ValidationHelpers.Required(errors, $"{path}[{i}]", sprites[i]);
        }

        static void Alpha(List<string> errors, string path, float alpha)
        {
            if (alpha < 0f || alpha > 1f) errors.Add($"{path}: must be between 0 and 1 (was {alpha}).");
        }
    }
}
