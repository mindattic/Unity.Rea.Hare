using System.Collections.Generic;
using Unity.Rea.Hare.Content;

namespace Unity.Rea.Hare.Helper
{
    /// <summary>
    /// obj_helper's numbers, as gmlHARE has them (the content file's <c>helper</c> section): its Create (sfx_hawk,
    /// <c>turn_speed = 2</c>, <c>speed = Speed = last_speed = max_speed = 4</c>, <c>smoke_delay = 40</c>,
    /// <c>spark_delay = 5</c>), the arrow keys' one-px speed steps, the solo mouse-follow's 2 degrees a step outside
    /// 4 degrees, the return's 400 px, the effects' sizes and spr_helper's box. Distances are GML px, times steps.
    /// </summary>
    public sealed class HelperTuning
    {
        /// <summary>obj_helper's sprite.</summary>
        public string Sprite = "spr_helper";
        /// <summary>obj_helper's depth.</summary>
        public int Depth = 0;
        /// <summary>Create: <c>audio_play_sound(sfx_hawk, 1, 0)</c>.</summary>
        public string CallSound = "sfx_hawk";

        /// <summary>Create: <c>speed = 4; Speed = 4; last_speed = 4</c>.</summary>
        public float StartSpeedPx = 4f;
        /// <summary>Create: <c>max_speed = 4</c> (Up stops there; the return flies at it).</summary>
        public float MaxSpeedPx = 4f;
        /// <summary>Down's <c>if (speed &gt; 1) speed -= 1</c>: the slowest it goes.</summary>
        public float MinSpeedPx = 1f;
        /// <summary>Up's and Down's <c>speed += 1</c> / <c>speed -= 1</c>.</summary>
        public float SpeedStepPx = 1f;
        /// <summary>Create: <c>turn_speed = 2</c>, degrees a step the left and right arrows turn it (Alarm 2, which would
        /// have made it <c>ceil(speed / 2)</c>, never runs).</summary>
        public float TurnSpeed = 2f;

        /// <summary>Create: <c>smoke_delay = 40</c>, Alarm 0's ef_smokeup trail.</summary>
        public int SmokeEverySteps = 40;
        /// <summary>Create: <c>spark_delay = 5</c>, Alarm 1's ef_spark while turning.</summary>
        public int SparkEverySteps = 5;
        /// <summary>Alarm 1's <c>rnd_x = irandom_range(-2, 2)</c>, likewise y.</summary>
        public int SparkJitterPx = 2;

        /// <summary>The solo Step's <c>direction -= 2</c> / <c>direction += 2</c> toward the mouse.</summary>
        public float SoloTurnDegrees = 2f;
        /// <summary>The solo Step's <c>!a_near_b(direction, mouse_direction, 4)</c>.</summary>
        public float SoloNearDegrees = 4f;
        /// <summary>The return's <c>distance_to_object(player) &gt; 400</c>.</summary>
        public float ReturnDistancePx = 400f;

        /// <summary>The first free step's <c>effect_create_below(ef_ring, x, y, 1, c_white)</c>.</summary>
        public int RingSize = 1;
        /// <summary>Every free step in view (c_gold) and every return step (c_orange): <c>effect_create_below(ef_smoke, x, y, 0, ...)</c>.</summary>
        public int SmokeSize = 0;
        /// <summary>Alarm 0's <c>effect_create_below(ef_smokeup, x - hspeed, y - vspeed, 0, c_white)</c>.</summary>
        public int TrailSize = 0;
        /// <summary>Alarm 1's <c>effect_create_below(ef_spark, ..., 0, spark_color)</c>.</summary>
        public int SparkSize = 0;
        /// <summary>Delete's <c>effect_create_above(ef_smokeup, x, y, 0, c_black)</c> and <c>(ef_firework, x, y, 0, c_orange)</c>.</summary>
        public int ExplosionEffectSize = 0;

        /// <summary>spr_helper's bounding box about its origin (16, 16): left, top, right, bottom, GML px (y down).</summary>
        public float[] Box = { -16f, -16f, 15f, 15f };

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Sprite", Sprite);
            ValidationHelpers.Required(errors, path + ".CallSound", CallSound);
            ValidationHelpers.Positive(errors, path + ".StartSpeedPx", StartSpeedPx);
            ValidationHelpers.Positive(errors, path + ".MaxSpeedPx", MaxSpeedPx);
            ValidationHelpers.NonNegative(errors, path + ".MinSpeedPx", MinSpeedPx);
            if (MinSpeedPx > MaxSpeedPx) errors.Add($"{path}.MinSpeedPx: must not exceed MaxSpeedPx (was {MinSpeedPx}).");
            ValidationHelpers.Positive(errors, path + ".SpeedStepPx", SpeedStepPx);
            ValidationHelpers.NonNegative(errors, path + ".TurnSpeed", TurnSpeed);
            ValidationHelpers.Positive(errors, path + ".SmokeEverySteps", SmokeEverySteps);
            ValidationHelpers.Positive(errors, path + ".SparkEverySteps", SparkEverySteps);
            ValidationHelpers.NonNegative(errors, path + ".SparkJitterPx", SparkJitterPx);
            ValidationHelpers.NonNegative(errors, path + ".SoloTurnDegrees", SoloTurnDegrees);
            ValidationHelpers.NonNegative(errors, path + ".SoloNearDegrees", SoloNearDegrees);
            ValidationHelpers.NonNegative(errors, path + ".ReturnDistancePx", ReturnDistancePx);
            ValidationHelpers.NonNegative(errors, path + ".RingSize", RingSize);
            ValidationHelpers.NonNegative(errors, path + ".SmokeSize", SmokeSize);
            ValidationHelpers.NonNegative(errors, path + ".TrailSize", TrailSize);
            ValidationHelpers.NonNegative(errors, path + ".SparkSize", SparkSize);
            ValidationHelpers.NonNegative(errors, path + ".ExplosionEffectSize", ExplosionEffectSize);
            if (Box == null || Box.Length != 4 || Box[2] < Box[0] || Box[3] < Box[1])
                errors.Add($"{path}.Box: must be [left, top, right, bottom] with right >= left and bottom >= top.");
        }
    }
}
