using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>What <see cref="EnemyCompositionViews.Place"/> created, as views.</summary>
    public sealed class EnemyPlacement
    {
        public readonly List<EnemyEmitter> Emitters = new List<EnemyEmitter>();
        /// <summary>Mimics, which setup_level creates directly instead of through an emitter.</summary>
        public readonly List<Mimic> Mimics = new List<Mimic>();
    }

    /// <summary>setup_level's enemy blocks, run in the enemy world and shown as views.</summary>
    public static class EnemyCompositionViews
    {
        /// <summary>
        /// Places the composition's emitters and mimics. <paramref name="counts"/> pins each type's rnd_X (tests);
        /// otherwise it is irandom_range(min, max) from the world's random source.
        /// </summary>
        public static EnemyPlacement Place(this EnemyComposition composition, Rect? room = null, PlayerShipController player = null,
            IDictionary<EnemyKind, int> counts = null)
        {
            var result = EnemyDirector.Execute(new PlaceComposition(composition, room?.ToBounds(), counts));
            var placed = new EnemyPlacement();
            foreach (var emitter in result.Placement.Emitters)
            {
                var view = EnemyDirector.ViewOf<EnemyEmitter>(emitter.Id);
                if (player != null) view.Player = player;
                placed.Emitters.Add(view);
            }
            foreach (var mimic in result.Placement.Mimics)
            {
                var view = EnemyDirector.ViewOf<Mimic>(mimic.Id);
                if (player != null) view.Player = player;
                placed.Mimics.Add(view);
            }
            return placed;
        }
    }

    /// <summary>
    /// GML instance_create(x, y, instance) and instance_number(instance) for an <see cref="EnemyKind"/>, plus the
    /// per-kind vortex scales the spawners use (kept as data in Core's <see cref="EnemyTable"/>).
    /// </summary>
    public static class EnemySpawner
    {
        /// <summary>GML instance_create(x, y, instance); new_instance.ID = new_instance is implicit.</summary>
        public static Component Spawn(EnemyKind kind, Vector2 position, Rect? room = null, PlayerShipController player = null)
        {
            var result = EnemyDirector.Execute(new SpawnEnemy(kind, position.ToVec2(), room?.ToBounds()));
            var view = EnemyDirector.ViewOf<EnemyBodyView>(result.Body.Id);
            if (player != null) view.Player = player;
            return view;
        }

        /// <summary>GML instance_number(instance): live instances of that object (a worm counts its head only).</summary>
        public static int Count(EnemyKind kind) => EnemyDirector.Count(kind);

        /// <summary>setup_level's emitter.scale for each par_enemy_emitter it places.</summary>
        public static float EmitterScale(EnemyKind kind) => EnemyTable.Get(kind).EmitterScale;

        /// <summary>create_wormhole's wormhole.scale for each kind.</summary>
        public static float WormholeScale(EnemyKind kind) => EnemyTable.Get(kind).WormholeScale;

        /// <summary>
        /// A whole-pixel point at least <paramref name="bufferPx"/> inside <paramref name="room"/>:
        /// GML x = irandom_range(buffer, room_width - buffer), y likewise (y grows downward).
        /// </summary>
        public static Vector2 RandomPointInside(Rect room, float bufferPx) =>
            SpawnPoints.RandomPointInside(EnemyDirector.World.Random, room.ToBounds(), bufferPx).ToVector2();

        /// <summary>GML irandom_range(a, b), inclusive; tolerates a &gt; b like GML.</summary>
        public static int IRandom(int a, int b) => EnemyDirector.World.Random.IRandom(a, b);
    }
}
