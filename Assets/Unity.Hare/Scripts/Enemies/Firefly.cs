using UnityEngine;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// The view on <see cref="FireflyBody"/> (obj_firefly, ai_firefly). Each think has a 1-in-5 chance to blink:
    /// toggle between spr_firefly and spr_firefly_dark (both a single pixel).
    /// </summary>
    public class Firefly : Insect
    {
        public const float MaxSpeedXPxPerStep = FireflyBody.MaxSpeedXPxPerStep;
        public const float MaxSpeedYPxPerStep = FireflyBody.MaxSpeedYPxPerStep;
        public const int ThinkMaxStepsValue = FireflyBody.ThinkMaxStepsValue;

        [SerializeField] Sprite litSprite;
        [SerializeField] Sprite darkSprite;

        protected override InsectKind Kind => InsectKind.Firefly;

        /// <summary>GML sprite_index == spr_firefly_dark.</summary>
        public bool IsDark => ((FireflyBody)Core).IsDark;

        protected override void OnShown()
        {
            var spriteRenderer = GetComponent<SpriteRenderer>();
            var sprite = IsDark ? darkSprite : litSprite;
            if (spriteRenderer != null && sprite != null) spriteRenderer.sprite = sprite;
        }
    }
}
