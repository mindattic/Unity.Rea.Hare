using UnityEngine;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// The view on <see cref="ButterflyBody"/> (obj_butterfly, ai_butterfly). The sprite faces its dX (image_xscale = 1
    /// when dX is positive, else -1); spr_butterfly is a 16x16, 6-frame flap played at image_speed 1 while unpaused.
    /// </summary>
    public class Butterfly : Insect
    {
        public const float MaxSpeedPxPerStep = ButterflyBody.MaxSpeedPxPerStep;
        public const float FloorMarginPx = ButterflyBody.FloorMarginPx;
        public const int ThinkMaxStepsValue = ButterflyBody.ThinkMaxStepsValue;

        protected override InsectKind Kind => InsectKind.Butterfly;
        protected override string SpriteName => "spr_butterfly";

        /// <summary>GML image_xscale = 1 (true) or -1.</summary>
        public bool FacingRight => ((ButterflyBody)Core).FacingRight;

        protected override void OnShown()
        {
            Vector3 s = transform.localScale;
            s.x = Mathf.Abs(s.x) * (FacingRight ? 1f : -1f);
            transform.localScale = s;
        }
    }
}
