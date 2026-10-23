using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Hare.Weapons;
using Random = UnityEngine.Random;

namespace Unity.Hare.Effects
{
    /// <summary>The GameMaker built-in effects the death effects use (GML ef_* constants).</summary>
    public enum GmlEffectKind
    {
        /// <summary>ef_firework: a burst of flares falling under gravity.</summary>
        Firework = 3,
        /// <summary>ef_smokeup: puffs of smoke rising and shrinking.</summary>
        SmokeUp = 5
    }

    /// <summary>GameMaker's built-in particle shapes (pt_shape_*), by their GML index.</summary>
    public enum GmlParticleShape
    {
        Flare = 8,
        Explosion = 10
    }

    /// <summary>A GameMaker particle type as the built-in effects set it up (all in GML px and steps).</summary>
    public readonly struct GmlParticleType
    {
        public readonly GmlParticleShape Shape;
        public readonly float SizeMin, SizeMax, SizeIncrPerStep;
        public readonly float SpeedMin, SpeedMax;
        public readonly float Direction;
        /// <summary>Random direction spread: direction is in [Direction, Direction + DirectionRange].</summary>
        public readonly float DirectionRange;
        public readonly float AlphaStart, AlphaEnd;
        public readonly float LifeMin, LifeMax;
        public readonly float GravityPerStep, GravityDirection;

        public GmlParticleType(GmlParticleShape shape, float sizeMin, float sizeMax, float sizeIncr, float speedMin, float speedMax,
            float direction, float directionRange, float alphaStart, float alphaEnd, float lifeMin, float lifeMax,
            float gravity, float gravityDirection)
        {
            Shape = shape;
            SizeMin = sizeMin; SizeMax = sizeMax; SizeIncrPerStep = sizeIncr;
            SpeedMin = speedMin; SpeedMax = speedMax;
            Direction = direction; DirectionRange = directionRange;
            AlphaStart = alphaStart; AlphaEnd = alphaEnd;
            LifeMin = lifeMin; LifeMax = lifeMax;
            GravityPerStep = gravity; GravityDirection = gravityDirection;
        }
    }

    /// <summary>One built-in effect: how many particles of which type, scattered over what box.</summary>
    public readonly struct GmlEffectRecipe
    {
        public readonly GmlParticleType Type;
        public readonly int Count;
        /// <summary>Each particle starts at x - Spread + random(2 Spread), the same for y (0: all at the point).</summary>
        public readonly float SpreadPx;

        public GmlEffectRecipe(GmlParticleType type, int count, float spreadPx)
        {
            Type = type;
            Count = count;
            SpreadPx = spreadPx;
        }
    }

    /// <summary>
    /// GML effect_create_above / effect_create_below for the effects the enemies' deaths use. The
    /// parameters are GameMaker's own (the runner's Effects.js, Eff_Effect03 and Eff_Effect05).
    /// The runner scales every speed, size change, gravity and life by Speed_Factor, 30 / room
    /// speed (0.25 in HARE's 120-step rooms), so an effect looks the same in seconds whatever
    /// the room speed: an ef_firework of size 0 is 75 flares at 0.125-0.75 px/step living
    /// 60-100 steps, an ef_smokeup of size 0 six puffs rising 0.75-1 px/step for 100 steps.
    /// Sizes are GML's: 0 small, 2 large, anything else medium.
    /// </summary>
    public static class GmlEffects
    {
        /// <summary>The runner's Speed_Factor() at room_speed 120 (and a game keeping up with it).</summary>
        public const float SpeedFactor = 30f / Gml.StepsPerSecond;

        /// <summary>effect_create_above's particle system depth (-100000: over everything).</summary>
        public const int AboveDepth = -100000;
        /// <summary>effect_create_below's particle system depth (100000: under everything).</summary>
        public const int BelowDepth = 100000;

        /// <summary>Every effect as it is created (tests and the audio-free presentation hang off this).</summary>
        public static event Action<GmlEffectBurst> Created;

        /// <summary>GML effect_create_above(kind, x, y, size, color).</summary>
        public static GmlEffectBurst CreateAbove(GmlEffectKind kind, Vector2 at, int size, Color color) =>
            Create(kind, at, size, color, true);

        /// <summary>GML effect_create_below(kind, x, y, size, color).</summary>
        public static GmlEffectBurst CreateBelow(GmlEffectKind kind, Vector2 at, int size, Color color) =>
            Create(kind, at, size, color, false);

        static GmlEffectBurst Create(GmlEffectKind kind, Vector2 at, int size, Color color, bool above)
        {
            var go = new GameObject("ef_" + kind);
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var burst = go.AddComponent<GmlEffectBurst>();
            burst.Init(kind, size, Recipe(kind, size), at, color, above);
            Created?.Invoke(burst);
            return burst;
        }

        /// <summary>GML Round(x / f): the runner's lives in steps.</summary>
        static float Life(float life) => Mathf.Round(life / SpeedFactor);

        /// <summary>The runner's recipe for <paramref name="kind"/> at <paramref name="size"/>.</summary>
        public static GmlEffectRecipe Recipe(GmlEffectKind kind, int size)
        {
            const float f = SpeedFactor;
            if (kind == GmlEffectKind.Firework)
            {
                // ParticleType_Size(0.1, 0.2), Direction(0, 360), Alpha2(1, 0.4), Gravity(g, 270).
                if (size == 0) return Firework(3.0f, 15f, 25f, 0.10f, 75);
                if (size == 2) return Firework(8.0f, 30f, 40f, 0.17f, 250);
                return Firework(6.0f, 20f, 30f, 0.15f, 150);

                GmlEffectRecipe Firework(float maxSpeed, float lifeMin, float lifeMax, float gravity, int count) =>
                    new GmlEffectRecipe(new GmlParticleType(GmlParticleShape.Flare, 0.1f, 0.2f, 0f, 0.5f * f, maxSpeed * f,
                        0f, 360f, 1f, 0.4f, Life(lifeMin), Life(lifeMax), gravity * f, 270f), count, 0f);
            }

            // ef_smokeup: Size(min, max, -0.01 f), Alpha2(0.4, 0), Direction(90, 90); i = 0..n, so n + 1 puffs.
            if (size == 0) return SmokeUp(0.2f, 0.4f, 3.0f, 4.0f, 25f, 6, 5f);
            if (size == 2) return SmokeUp(0.4f, 1.0f, 6.0f, 7.0f, 50f, 16, 30f);
            return SmokeUp(0.4f, 0.7f, 5.0f, 6.0f, 30f, 11, 15f);

            GmlEffectRecipe SmokeUp(float sizeMin, float sizeMax, float speedMin, float speedMax, float life, int count, float spread) =>
                new GmlEffectRecipe(new GmlParticleType(GmlParticleShape.Explosion, sizeMin, sizeMax, -0.01f * f, speedMin * f, speedMax * f,
                    90f, 0f, 0.4f, 0f, Life(life), Life(life), 0f, 0f), count, spread);
        }
    }

    /// <summary>
    /// The particles of one built-in effect, run as GameMaker's particle system runs them: each
    /// step a particle ages (and goes once its age reaches its life), takes gravity into its
    /// velocity, moves, changes size (never below 0) and fades linearly from its start to its end
    /// alpha over its life. Its shape is GameMaker's 64 px particle texture scaled by size and
    /// tinted the effect's colour, drawn through a ParticleSystem the burst fills itself.
    /// </summary>
    public class GmlEffectBurst : MonoBehaviour
    {
        /// <summary>GameMaker's particle shape textures are 64x64 px.</summary>
        public const float ShapeTexturePx = 64f;

        public struct Particle
        {
            /// <summary>World position.</summary>
            public Vector2 Position;
            /// <summary>Velocity in px/step (Unity axes).</summary>
            public Vector2 Velocity;
            public float Size;
            public float Alpha;
            public float Age;
            public float Life;
        }

        readonly List<Particle> particles = new List<Particle>();
        ParticleSystem system;
        ParticleSystem.Particle[] drawn;

        public GmlEffectKind Kind { get; private set; }
        /// <summary>The GML size argument (0 small, 2 large, else medium).</summary>
        public int Size { get; private set; }
        /// <summary>The x, y the effect was created at.</summary>
        public Vector2 Origin { get; private set; }
        public GmlEffectRecipe Recipe { get; private set; }
        public Color Color { get; private set; }
        public bool Above { get; private set; }
        public IReadOnlyList<Particle> Particles => particles;
        public int Count => particles.Count;

        public void Init(GmlEffectKind kind, int size, GmlEffectRecipe recipe, Vector2 at, Color color, bool above)
        {
            Kind = kind;
            Size = size;
            Origin = at;
            Recipe = recipe;
            Color = color;
            Above = above;
            var t = recipe.Type;
            for (int i = 0; i < recipe.Count; i++)
            {
                // GML: x - s + YYRandom(2 s), y likewise; the box is symmetric so GML's downward y needs no flip.
                Vector2 offset = recipe.SpreadPx > 0f
                    ? new Vector2(Random.Range(-recipe.SpreadPx, recipe.SpreadPx), Random.Range(-recipe.SpreadPx, recipe.SpreadPx))
                    : Vector2.zero;
                float speed = Random.Range(t.SpeedMin, t.SpeedMax);
                float dir = t.Direction + Random.Range(0f, t.DirectionRange);
                particles.Add(new Particle
                {
                    Position = at + new Vector2(Gml.Px(offset.x), Gml.Px(offset.y)),
                    Velocity = Gml.Dir(dir) * speed,
                    Size = Random.Range(t.SizeMin, t.SizeMax),
                    Alpha = t.AlphaStart,
                    Age = 0f,
                    Life = Random.Range(t.LifeMin, t.LifeMax)
                });
            }
            if (Application.isPlaying) SetUpRenderer();
        }

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>Runs the particles for <paramref name="dt"/> seconds; false once the last one is gone.</summary>
        public bool Tick(float dt)
        {
            float steps = dt * Gml.StepsPerSecond;
            var t = Recipe.Type;
            Vector2 gravity = t.GravityPerStep != 0f ? Gml.Dir(t.GravityDirection) * t.GravityPerStep : Vector2.zero;
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.Age += steps;
                if (p.Age >= p.Life - 1e-4f)
                {
                    particles.RemoveAt(i);
                    continue;
                }
                p.Velocity += gravity * steps;
                p.Position += p.Velocity * Gml.Px(steps);
                p.Size = Mathf.Max(0f, p.Size + t.SizeIncrPerStep * steps);
                p.Alpha = Mathf.Lerp(t.AlphaStart, t.AlphaEnd, p.Age / p.Life);
                particles[i] = p;
            }
            if (particles.Count > 0) return true;
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            return false;
        }

        void SetUpRenderer()
        {
            var texture = HareArt.ParticleShape(Recipe.Type.Shape == GmlParticleShape.Flare ? "pt_shape_flare" : "pt_shape_explosion");
            if (texture == null) return;
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) return;

            system = gameObject.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = Mathf.Max(1, Recipe.Count);
            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var renderer = GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            // The particle systems sit at depth -100000 / 100000: over or under every sprite.
            renderer.sortingOrder = Above ? short.MaxValue : short.MinValue;
            var material = new Material(shader) { mainTexture = texture };
            renderer.sharedMaterial = material;
            drawn = new ParticleSystem.Particle[Recipe.Count];
        }

        void LateUpdate()
        {
            if (system == null) return;
            int n = particles.Count;
            Color32 tint = Color;
            for (int i = 0; i < n; i++)
            {
                var p = particles[i];
                tint.a = (byte)Mathf.RoundToInt(Mathf.Clamp01(p.Alpha) * 255f);
                drawn[i].position = p.Position;
                drawn[i].startSize = Gml.Px(ShapeTexturePx * p.Size);
                drawn[i].startColor = tint;
                drawn[i].startLifetime = drawn[i].remainingLifetime = 1000f;
                drawn[i].velocity = Vector3.zero;
            }
            system.SetParticles(drawn, n);
        }
    }
}
