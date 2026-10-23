using System;
using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Weapons;

namespace Unity.Hare.Rocks
{
    /// <summary>
    /// Ports obj_flotsum (gmlHARE, depth 0, no collisions): a shard of a broken rock, created only by
    /// rock_destroyed. Create picks spr_flotsum1-5, image_angle and direction 0-359, spin_speed
    /// irandom_range(-5, 5) degrees/step, speed irandom_range(1, 3) px/step and fade_speed
    /// irandom_range(2, 10) * 0.001 alpha/step. Each unpaused step it turns spin_speed and fades,
    /// destroying itself once image_alpha reaches 0; while paused it stops.
    /// GML's Step stored Speed = speed every unpaused step but never restored it, so after any pause
    /// every shard sat frozen until it faded; fixed: it drifts on (speed = Speed), as par_rock does.
    /// </summary>
    public class Flotsum : MonoBehaviour
    {
        public const int Depth = 0;
        public static readonly string[] SpriteNames = { "spr_flotsum1", "spr_flotsum2", "spr_flotsum3", "spr_flotsum4", "spr_flotsum5" };

        SpriteRenderer spriteRenderer;
        float direction;
        float imageAngle;
        bool gone;

        public static event Action<Flotsum> Spawned;

        /// <summary>rnd_asset - 1: index into <see cref="SpriteNames"/>.</summary>
        public int Kind { get; private set; }
        public float Direction => direction;
        public float SpeedPxPerStep { get; private set; }
        public float StoredSpeedPxPerStep { get; private set; }
        public float SpinSpeed { get; private set; }
        public float FadeSpeed { get; private set; }
        public float ImageAlpha { get; private set; } = 1f;
        public float ImageAngle => imageAngle;
        public Vector2 Position => transform.position;

        /// <summary>GML instance_create(x, y, obj_flotsum).</summary>
        public static Flotsum Spawn(Vector2 at)
        {
            var go = new GameObject("Flotsum");
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var flotsum = go.AddComponent<Flotsum>();
            flotsum.Init();
            Spawned?.Invoke(flotsum);
            return flotsum;
        }

        void Init()
        {
            Kind = Rock.IRandom(1, 5) - 1;
            direction = Rock.IRandom(0, 359);
            SetAngle(Rock.IRandom(0, 359));
            SpinSpeed = Rock.IRandom(-5, 5);
            SpeedPxPerStep = Rock.IRandom(1, 3);
            StoredSpeedPxPerStep = SpeedPxPerStep;
            FadeSpeed = 0.001f * Rock.IRandom(2, 10);
            ImageAlpha = 1f;
            spriteRenderer = HareArt.Show(gameObject, SpriteNames[Kind], Depth);
        }

        void SetAngle(float degrees)
        {
            imageAngle = Mathf.Repeat(degrees, 360f);
            transform.rotation = Quaternion.Euler(0f, 0f, imageAngle);
        }

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>One GML step; false once it has faded out and is gone.</summary>
        public bool Tick(float dt)
        {
            if (gone) return false;
            float steps = dt * Gml.StepsPerSecond;
            if (Gml.Paused)
            {
                SpeedPxPerStep = 0f;
                return true;
            }

            SpeedPxPerStep = StoredSpeedPxPerStep;
            SetAngle(imageAngle + SpinSpeed * steps);
            if (ImageAlpha > 0f)
            {
                ImageAlpha -= FadeSpeed * steps;
                if (spriteRenderer != null) spriteRenderer.color = new Color(1f, 1f, 1f, Mathf.Max(0f, ImageAlpha));
            }
            else
            {
                gone = true;
                if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
                return false;
            }

            Vector2 next = Position + Gml.Dir(direction) * Gml.Px(SpeedPxPerStep * steps);
            transform.position = new Vector3(next.x, next.y, transform.position.z);
            return true;
        }
    }
}
