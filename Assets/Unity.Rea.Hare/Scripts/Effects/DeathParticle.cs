using System;
using UnityEngine;
using Unity.Hare.Weapons;
using Random = UnityEngine.Random;

namespace Unity.Hare.Effects
{
    /// <summary>Which of obj_death_particle's three sprites a particle shows.</summary>
    public enum DeathParticleSize
    {
        /// <summary>spr_death_particle_large (36 px).</summary>
        Large,
        /// <summary>spr_death_particle_medium (18 px).</summary>
        Medium,
        /// <summary>spr_death_particle_small (7 px).</summary>
        Small
    }

    /// <summary>
    /// Ports obj_death_particle and spawn_death_particles (gmlHARE). A particle starts on a random
    /// whole heading (0-359) at a whole 1-8 px/step, spinning -9..9 degrees a step, at alpha 0.9,
    /// and loses 0.002 alpha a step, so it flies for 450 steps (3.75 s) before it destroys itself.
    /// While the game is paused it stands still and neither spins nor fades. No collision events.
    /// spawn_death_particles(x, y) plays sfx_death and scatters 3-6 large, 12-18 medium and 32-48
    /// small particles, each within 32 px of the point. It is the death of something big: the
    /// Scrap Mental boss (UH-T68) and the rabbit itself (player_hurt, UH-T54) call it.
    /// </summary>
    public class DeathParticle : MonoBehaviour
    {
        public const string Sound = "sfx_death";
        public const float StartAlpha = 0.9f;
        public const float FadePerStep = 0.002f;
        public const int ScatterPx = 32;
        public const int Depth = 0;

        SpriteRenderer spriteRenderer;

        public DeathParticleSize Size { get; private set; }
        public float Direction { get; private set; }
        public float SpeedPxPerStep { get; private set; }
        public float SpinPerStep { get; private set; }
        public float ImageAngle { get; private set; }
        public float Alpha { get; private set; }

        public static event Action<DeathParticle> Spawned;
        /// <summary>One spawn_death_particles call, with the particles it made.</summary>
        public static event Action<Vector2, int> Scattered;

        public static string SpriteFor(DeathParticleSize size) =>
            size == DeathParticleSize.Large ? "spr_death_particle_large"
            : size == DeathParticleSize.Medium ? "spr_death_particle_medium"
            : "spr_death_particle_small";

        /// <summary>GML instance_create(x, y, obj_death_particle) then particle.sprite_index = ...</summary>
        public static DeathParticle Spawn(Vector2 at, DeathParticleSize size)
        {
            var go = new GameObject("DeathParticle");
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var p = go.AddComponent<DeathParticle>();
            p.Size = size;
            p.Direction = Random.Range(0, 360);
            p.SpeedPxPerStep = Random.Range(1, 9);
            p.SpinPerStep = Random.Range(-9, 10);
            p.ImageAngle = 0f;
            p.Alpha = StartAlpha;
            p.spriteRenderer = HareArt.Show(go, SpriteFor(size), Depth, StartAlpha);
            Spawned?.Invoke(p);
            return p;
        }

        /// <summary>GML spawn_death_particles(x, y); returns how many particles it made.</summary>
        public static int SpawnDeathParticles(Vector2 origin)
        {
            HareArt.Play(Sound);
            int total = Scatter(origin, DeathParticleSize.Large, 3, 6)
                + Scatter(origin, DeathParticleSize.Medium, 12, 18)
                + Scatter(origin, DeathParticleSize.Small, 32, 48);
            Scattered?.Invoke(origin, total);
            return total;
        }

        static int Scatter(Vector2 origin, DeathParticleSize size, int min, int max)
        {
            int count = Random.Range(min, max + 1);
            for (int i = 0; i < count; i++)
                Spawn(origin + HareArt.GmlOffset(Random.Range(-ScatterPx, ScatterPx + 1), Random.Range(-ScatterPx, ScatterPx + 1)), size);
            return count;
        }

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>One GML step; false once the particle has faded out and is gone.</summary>
        public bool Tick(float dt)
        {
            if (Gml.Paused) return true;
            float steps = dt * Gml.StepsPerSecond;
            Vector2 next = (Vector2)transform.position + Gml.Dir(Direction) * Gml.Px(SpeedPxPerStep * steps);
            transform.position = new Vector3(next.x, next.y, transform.position.z);
            ImageAngle = Mathf.Repeat(ImageAngle + SpinPerStep * steps, 360f);
            transform.rotation = Quaternion.Euler(0f, 0f, ImageAngle);
            Alpha -= FadePerStep * steps;
            if (spriteRenderer != null) spriteRenderer.color = new Color(1f, 1f, 1f, Mathf.Max(0f, Alpha));
            if (Alpha > 1e-5f) return true;
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            return false;
        }
    }
}
