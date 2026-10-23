using UnityEngine;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// Ports obj_firefly and ai_firefly (gmlHARE). Every irandom_range(5, 30) steps it picks dX in
    /// [-3, 3] and dY in [-1, 1]; in the room's upper half it sinks (dY = 1) and within 10 px of a
    /// side wall it turns back (dX = +/-3). There is no floor rule, so a firefly can drift out of
    /// the bottom of the room, as in the original. Each think has a 1-in-5 chance (irandom(4) == 4)
    /// to blink: toggle between spr_firefly and spr_firefly_dark (both a single pixel).
    /// </summary>
    public class Firefly : Insect
    {
        public const float MaxSpeedXPxPerStep = 3f;
        public const float MaxSpeedYPxPerStep = 1f;
        public const int ThinkMaxStepsValue = 30;

        [SerializeField] Sprite litSprite;
        [SerializeField] Sprite darkSprite;

        public override int ThinkMaxSteps => ThinkMaxStepsValue;

        /// <summary>GML sprite_index == spr_firefly_dark.</summary>
        public bool IsDark { get; private set; }

        protected override void OnInit() => SetDark(false);

        public override void Think()
        {
            DX = Random.Range(-MaxSpeedXPxPerStep, MaxSpeedXPxPerStep);
            DY = Random.Range(-MaxSpeedYPxPerStep, MaxSpeedYPxPerStep);

            if (InUpperHalf) DY = MaxSpeedYPxPerStep;

            if (NearLeft) DX = MaxSpeedXPxPerStep;
            else if (NearRight) DX = -MaxSpeedXPxPerStep;

            if (Random.Range(0, 5) == 4) Blink(); // blink = irandom(4); if (blink == 4)
        }

        /// <summary>Toggles spr_firefly / spr_firefly_dark.</summary>
        public void Blink() => SetDark(!IsDark);

        void SetDark(bool dark)
        {
            IsDark = dark;
            var spriteRenderer = GetComponent<SpriteRenderer>();
            var sprite = dark ? darkSprite : litSprite;
            if (spriteRenderer != null && sprite != null) spriteRenderer.sprite = sprite;
        }
    }
}
