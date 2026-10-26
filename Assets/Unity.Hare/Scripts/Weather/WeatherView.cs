using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Hare.Content;
using Unity.Hare.Effects;
using Unity.Hare.Weapons;

namespace Unity.Hare.Weather
{
    /// <summary>
    /// A play room's weather, played (U.HARE-T18): the room's view of Core's <see cref="WeatherField"/>. The arena
    /// (<see cref="Arena.ArenaRoom"/>) starts the level's emitters (<see cref="Arena.LevelSetting.Weather"/>: the cloud
    /// emitter every play room places) after setup_level's Clear Objects, steps it once a room step and has it drawn each
    /// frame; <see cref="Arena.LevelReset.ClearObjects"/> takes every emitter and particle away. It draws each particle
    /// (obj_cloud at depth 0; obj_raindrop, obj_snow, obj_sand and obj_fog at -1) with a pooled sprite renderer at its
    /// sprite's GML origin, its image_alpha and image_angle, and decides nothing itself. The particles are drawn, not
    /// made as objects: nothing collides with weather.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WeatherView : MonoBehaviour
    {
        readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
        Func<float, float, Vector2> toWorld;

        /// <summary>The weather's rules and state.</summary>
        public WeatherField Field { get; private set; }

        /// <summary>The renderers drawing a particle this frame.</summary>
        public int Drawn { get; private set; }

        /// <summary>Every renderer, drawing or idle (tests).</summary>
        public IReadOnlyList<SpriteRenderer> Renderers => pool;

        /// <summary>
        /// A weather view for a <paramref name="roomWidth"/> x <paramref name="roomHeight"/> room whose pixels
        /// <paramref name="toWorld"/> puts in the world.
        /// </summary>
        public static WeatherView Create(int roomWidth, int roomHeight, Func<float, float, Vector2> toWorld, IRandomSource rng = null, WeatherTuning tuning = null)
        {
            var view = new GameObject("Weather").AddComponent<WeatherView>();
            view.Field = new WeatherField(rng ?? new SeededRandom(Environment.TickCount), roomWidth, roomHeight, tuning ?? GameContent.Current.Weather);
            view.toWorld = toWorld ?? ((x, y) => new Vector2(Gml.Px(x), -Gml.Px(y)));
            return view;
        }

        /// <summary>The level's emitters, in order (each emitter's Create runs now: the cloud emitter's 12, the fog's 32).</summary>
        public void Begin(IReadOnlyList<WeatherKind> kinds)
        {
            var list = new WeatherKind[kinds?.Count ?? 0];
            for (int i = 0; i < list.Length; i++) list[i] = kinds[i];
            Field.Execute(new StartWeather(list));
            Present();
        }

        /// <summary>Every emitter and particle goes (setup_level's Clear Objects).</summary>
        public void Clear()
        {
            if (Field == null) return;
            Field.Execute(new ClearWeather());
            Present();
        }

        /// <summary>One room step, with GML's paused.</summary>
        public void Step()
        {
            Field.Execute(new SetWeatherPaused(Gml.Paused));
            Field.Execute(new StepWeather());
        }

        void LateUpdate() => Present();

        /// <summary>Draws every particle where the field has it; idle renderers are switched off.</summary>
        public void Present()
        {
            if (Field == null) return;
            var particles = Field.Particles;
            while (pool.Count < particles.Count)
            {
                var go = new GameObject("particle");
                go.transform.SetParent(transform, false);
                var renderer = go.AddComponent<SpriteRenderer>();
                pool.Add(renderer);
            }
            for (int i = 0; i < particles.Count; i++)
            {
                var p = particles[i];
                var renderer = pool[i];
                if (!renderer.enabled) renderer.enabled = true;
                renderer.sprite = HareArt.Frame(p.Sprite);
                renderer.sortingOrder = HareArt.SortingOrderForDepth(p.Depth);
                renderer.color = new Color(1f, 1f, 1f, p.Alpha);
                var at = toWorld(p.X, p.Y);
                renderer.transform.SetPositionAndRotation(new Vector3(at.x, at.y, 0f), Quaternion.Euler(0f, 0f, p.Angle));
            }
            for (int i = particles.Count; i < pool.Count; i++)
                if (pool[i].enabled) pool[i].enabled = false;
            Drawn = particles.Count;
        }
    }
}
