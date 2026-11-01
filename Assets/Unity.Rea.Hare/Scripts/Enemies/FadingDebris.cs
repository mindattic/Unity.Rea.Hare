using System;
using UnityEngine;
using Unity.Rea.Hare.Effects;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Enemies
{
    /// <summary>
    /// Ports the purely visual leftovers of the set-piece enemies, which share one Step: drift along
    /// direction at speed px/step, spin image_angle by spin_speed, and lose fade_speed alpha a step,
    /// destroying themselves the step after alpha reaches 0. obj_meatball_gib, obj_worm_gib and
    /// obj_worm_head_gib start opaque on a random heading (0-359), angle, spin (-5..5), speed (1..3)
    /// and fade (0.004..0.010); obj_wraith_blur starts at alpha 0.3, fades 0.005 and sits still
    /// unless the wraith is stunned. While the game is paused they stand still and neither spin nor
    /// fade. No collision events: nothing can hit them. Gibs draw at depth 0, under the enemies
    /// (-3); the wraith blur at -2.
    /// </summary>
    public class FadingDebris : MonoBehaviour
    {
        public DebrisKind Kind { get; private set; }
        /// <summary>Which sprite of the kind's set (1-based), e.g. spr_worm_gib3.</summary>
        public int Variant { get; private set; }
        public float Direction { get; private set; }
        public float SpeedPxPerStep { get; private set; }
        public float ImageAngle { get; private set; }
        public float SpinPerStep { get; private set; }
        public float FadePerStep { get; private set; }
        public float Alpha { get; private set; }
        /// <summary>GML image_xscale (wraith blurs face their heading).</summary>
        public int Facing { get; private set; } = 1;
        /// <summary>The GML sprite this piece shows (spr_meatball_gib1..6, spr_worm_gib1..8, spr_worm_head).</summary>
        public string SpriteName => Kind == DebrisKind.MeatballGib ? "spr_meatball_gib" + Variant
            : Kind == DebrisKind.WormGib ? "spr_worm_gib" + Variant
            : Kind == DebrisKind.WormHeadGib ? "spr_worm_head" : null;

        SpriteRenderer spriteRenderer;

        /// <summary>Every new piece, for the VFX epic to give it a sprite.</summary>
        public static event Action<FadingDebris> Spawned;

        public static FadingDebris Spawn(DebrisKind kind, Vector2 position, int variant, float direction, float speedPxPerStep,
            float imageAngle, float spinPerStep, float alpha, float fadePerStep, int facing = 1)
        {
            var go = new GameObject(kind.ToString());
            go.transform.position = new Vector3(position.x, position.y, 0f);
            var d = go.AddComponent<FadingDebris>();
            d.Kind = kind;
            d.Variant = variant;
            d.Direction = direction;
            d.SpeedPxPerStep = speedPxPerStep;
            d.ImageAngle = imageAngle;
            d.SpinPerStep = spinPerStep;
            d.Alpha = alpha;
            d.FadePerStep = fadePerStep;
            d.Facing = facing;
            go.transform.rotation = Quaternion.Euler(0f, 0f, imageAngle);
            if (d.SpriteName != null)
            {
                d.spriteRenderer = HareArt.Show(go, d.SpriteName, 0, alpha);
                d.spriteRenderer.flipX = facing < 0;
            }
            Spawned?.Invoke(d);
            return d;
        }

        /// <summary>A piece Core dealt (its Create's random values are Core's, from the world's seed).</summary>
        public static FadingDebris Spawn(DebrisSpec spec) =>
            Spawn(spec.Kind, spec.Position.ToVector2(), spec.Variant, spec.Direction, spec.SpeedPxPerStep, spec.ImageAngle,
                spec.SpinPerStep, spec.Alpha, spec.FadePerStep, spec.Facing);

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>One GML step; false once the piece is gone.</summary>
        public bool Tick(float dt)
        {
            if (Gml.Paused) return true;
            float steps = dt * Gml.StepsPerSecond;
            Vector2 next = (Vector2)transform.position + Gml.Dir(Direction) * Gml.Px(SpeedPxPerStep * steps);
            transform.position = new Vector3(next.x, next.y, transform.position.z);
            ImageAngle = Mathf.Repeat(ImageAngle + SpinPerStep * steps, 360f);
            transform.rotation = Quaternion.Euler(0f, 0f, ImageAngle);

            if (Alpha > 0f)
            {
                Alpha -= FadePerStep * steps;
                if (spriteRenderer != null) spriteRenderer.color = new Color(1f, 1f, 1f, Mathf.Max(0f, Alpha));
                return true;
            }
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            return false;
        }
    }
}
