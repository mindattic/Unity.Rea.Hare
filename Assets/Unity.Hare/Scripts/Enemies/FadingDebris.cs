using System;
using UnityEngine;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    public enum DebrisKind
    {
        /// <summary>obj_meatball_gib: spr_meatball_gib1..6.</summary>
        MeatballGib,
        /// <summary>obj_worm_gib: spr_worm_gib1..8.</summary>
        WormGib,
        /// <summary>obj_worm_head_gib: spr_worm_head.</summary>
        WormHeadGib,
        /// <summary>obj_wraith_blur: an afterimage of the wraith's current sprite.</summary>
        WraithBlur
    }

    /// <summary>
    /// Ports the purely visual leftovers of the set-piece enemies, which share one Step: drift along
    /// direction at speed px/step, spin image_angle by spin_speed, and lose fade_speed alpha a step,
    /// destroying themselves the step after alpha reaches 0. obj_meatball_gib, obj_worm_gib and
    /// obj_worm_head_gib start opaque on a random heading (0-359), angle, spin (-5..5), speed (1..3)
    /// and fade (0.004..0.010); obj_wraith_blur starts at alpha 0.3, fades 0.005 and sits still
    /// unless the wraith is stunned. No collision events: nothing can hit them.
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
            Spawned?.Invoke(d);
            return d;
        }

        /// <summary>One gib with obj_*_gib's random Create.</summary>
        public static FadingDebris Gib(DebrisKind kind, Vector2 position)
        {
            int variant = kind == DebrisKind.MeatballGib ? UnityEngine.Random.Range(1, 7)
                : kind == DebrisKind.WormGib ? UnityEngine.Random.Range(1, 9) : 1;
            return Spawn(kind, position, variant,
                UnityEngine.Random.Range(0, 360),
                UnityEngine.Random.Range(1, 4),
                UnityEngine.Random.Range(0, 360),
                UnityEngine.Random.Range(-5, 6),
                1f,
                0.001f * UnityEngine.Random.Range(4, 11));
        }

        /// <summary>GML meatball_gib(x, y): 8-12 obj_meatball_gib within 40 px of (x, y - 34).</summary>
        public static int MeatballGibs(Vector2 at) => Scatter(DebrisKind.MeatballGib, at, 8, 12);

        /// <summary>GML worm_gib(x, y): 6-8 obj_worm_gib within 40 px of (x, y - 34).</summary>
        public static int WormGibs(Vector2 at) => Scatter(DebrisKind.WormGib, at, 6, 8);

        static int Scatter(DebrisKind kind, Vector2 at, int min, int max)
        {
            int count = UnityEngine.Random.Range(min, max + 1);
            for (int i = 0; i < count; i++)
            {
                // GML y grows downward, so Y - 34 is 34 px up.
                var offset = new Vector2(UnityEngine.Random.Range(-40, 41), 34f - UnityEngine.Random.Range(-40, 41));
                Gib(kind, at + new Vector2(Gml.Px(offset.x), Gml.Px(offset.y)));
            }
            return count;
        }

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>One GML step; false once the piece is gone.</summary>
        public bool Tick(float dt)
        {
            float steps = dt * Gml.StepsPerSecond;
            Vector2 next = (Vector2)transform.position + Gml.Dir(Direction) * Gml.Px(SpeedPxPerStep * steps);
            transform.position = new Vector3(next.x, next.y, transform.position.z);
            ImageAngle = Mathf.Repeat(ImageAngle + SpinPerStep * steps, 360f);
            transform.rotation = Quaternion.Euler(0f, 0f, ImageAngle);

            if (Alpha > 0f)
            {
                Alpha -= FadePerStep * steps;
                return true;
            }
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            return false;
        }
    }
}
