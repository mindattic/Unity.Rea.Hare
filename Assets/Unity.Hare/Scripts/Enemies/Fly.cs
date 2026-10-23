using UnityEngine;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// Ports obj_fly and ai_fly (gmlHARE). Every irandom_range(5, 30) steps it picks dX in [-4, 4]
    /// and dY in [-2, 2]; in the room's upper half it sinks (dY = 1, not 2) and within 10 px of a
    /// side wall it turns back (dX = +/-4). Like the firefly it has no floor rule. Create's
    /// image_index = choose(spr_fly, spr_fly2) assigns a sprite id to the frame index of the
    /// one-frame spr_fly, so every fly shows spr_fly; spr_fly2 is never seen.
    /// </summary>
    public class Fly : Insect
    {
        public const float MaxSpeedXPxPerStep = 4f;
        public const float MaxSpeedYPxPerStep = 2f;
        public const float SinkSpeedPxPerStep = 1f;
        public const int ThinkMaxStepsValue = 30;

        public override int ThinkMaxSteps => ThinkMaxStepsValue;

        public override void Think()
        {
            DX = Random.Range(-MaxSpeedXPxPerStep, MaxSpeedXPxPerStep);
            DY = Random.Range(-MaxSpeedYPxPerStep, MaxSpeedYPxPerStep);

            if (InUpperHalf) DY = SinkSpeedPxPerStep;

            if (NearLeft) DX = MaxSpeedXPxPerStep;
            else if (NearRight) DX = -MaxSpeedXPxPerStep;
        }
    }
}
