using UnityEngine;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// Ports obj_butterfly and ai_butterfly (gmlHARE). Every irandom_range(5, 60) steps it picks
    /// dX, dY in [-2, 2]; in the room's upper half it is pushed down (dY = 2), within 120 px of the
    /// floor up (dY = -2), and within 10 px of a side wall back in (dX = +/-2), so butterflies stay
    /// in the lower half. The sprite faces its dX (image_xscale = 1 when dX &gt; 0, else -1).
    /// spr_butterfly is a 16x16, 6-frame flap played at image_speed 1 while unpaused.
    /// </summary>
    public class Butterfly : Insect
    {
        public const float MaxSpeedPxPerStep = 2f;
        public const float FloorMarginPx = 120f;
        public const int ThinkMaxStepsValue = 60;

        public override int ThinkMaxSteps => ThinkMaxStepsValue;

        /// <summary>GML image_xscale = 1 (true) or -1.</summary>
        public bool FacingRight { get; private set; } = true;

        public override void Think()
        {
            DX = Random.Range(-MaxSpeedPxPerStep, MaxSpeedPxPerStep);
            DY = Random.Range(-MaxSpeedPxPerStep, MaxSpeedPxPerStep);

            if (InUpperHalf) DY = MaxSpeedPxPerStep;
            else if (NearBottom(FloorMarginPx)) DY = -MaxSpeedPxPerStep;

            if (NearLeft) DX = MaxSpeedPxPerStep;
            else if (NearRight) DX = -MaxSpeedPxPerStep;

            FacingRight = DX > 0f;
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x) * (FacingRight ? 1f : -1f);
            transform.localScale = s;
        }
    }
}
