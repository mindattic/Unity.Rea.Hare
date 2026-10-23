using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>One enemy type's GML *_min/*_max pair from a level script.</summary>
    [Serializable]
    public struct EnemyRange
    {
        public int Min;
        public int Max;

        public EnemyRange(int min, int max)
        {
            Min = min;
            Max = max;
        }
    }

    /// <summary>What <see cref="EnemyComposition.Place"/> created.</summary>
    public sealed class EnemyPlacement
    {
        public readonly List<EnemyEmitter> Emitters = new List<EnemyEmitter>();
        /// <summary>Mimics, which setup_level creates directly instead of through an emitter.</summary>
        public readonly List<Mimic> Mimics = new List<Mimic>();
    }

    /// <summary>
    /// The enemy half of a level's composition (hydra_min/max ... trilobyte_min/max, all 0 after
    /// setup_level's reset) and setup_level's "Generate X" blocks that turn it into spawners. For
    /// each type, rnd_X = irandom_range(X_min, X_max) and setup_level then
    /// <list type="bullet">
    /// <item>creates that many mimics outright at random points in the room;</item>
    /// <item>for every other type, places that many <see cref="EnemyEmitter"/>s (instance = the
    /// type, instance_min/max = X_min/X_max, waits 200-500 steps, scale per
    /// <see cref="EnemySpawner.EmitterScale"/>) at random points; each emitter then keeps the
    /// type's live count topped up to X_max.</item>
    /// </list>
    /// The level scripts that fill it in (level_01..level_10, compositions 00/01) are UH-T20's.
    /// </summary>
    [Serializable]
    public class EnemyComposition
    {
        /// <summary>setup_level's Generate order.</summary>
        public static readonly EnemyKind[] PlacementOrder =
        {
            EnemyKind.Hydra, EnemyKind.Sawblade, EnemyKind.Meatball, EnemyKind.Wraith, EnemyKind.Phalanx,
            EnemyKind.Mimic, EnemyKind.Worm, EnemyKind.Dagger, EnemyKind.Trilobyte
        };

        readonly Dictionary<EnemyKind, EnemyRange> ranges = new Dictionary<EnemyKind, EnemyRange>();

        public EnemyRange this[EnemyKind kind]
        {
            get => ranges.TryGetValue(kind, out var r) ? r : default;
            set => ranges[kind] = value;
        }

        /// <summary>Fluent setter: <c>new EnemyComposition().With(EnemyKind.Hydra, 1, 3)</c>.</summary>
        public EnemyComposition With(EnemyKind kind, int min, int max)
        {
            this[kind] = new EnemyRange(min, max);
            return this;
        }

        /// <summary>
        /// setup_level's enemy blocks. <paramref name="counts"/> pins each type's rnd_X (tests);
        /// otherwise it is irandom_range(min, max).
        /// </summary>
        public EnemyPlacement Place(Rect? room = null, PlayerShipController player = null, IDictionary<EnemyKind, int> counts = null)
        {
            Rect r = room ?? Gml.Room;
            var placed = new EnemyPlacement();
            // Every rnd_X is rolled before any block runs, as in setup_level.
            var rolls = new Dictionary<EnemyKind, int>();
            foreach (var kind in PlacementOrder)
                rolls[kind] = counts != null && counts.TryGetValue(kind, out int n) ? n : EnemySpawner.IRandom(this[kind].Min, this[kind].Max);

            foreach (var kind in PlacementOrder)
            {
                var range = this[kind];
                for (int i = 0; i < rolls[kind]; i++)
                {
                    // rnd_x = irandom_range(0, room_width); rnd_y = irandom_range(0, room_height)
                    Vector2 at = EnemySpawner.RandomPointInside(r, 0f);
                    if (kind == EnemyKind.Mimic)
                    {
                        placed.Mimics.Add((Mimic)EnemySpawner.Spawn(kind, at, r, player));
                        continue;
                    }
                    var emitter = new GameObject(kind + "Emitter").AddComponent<EnemyEmitter>();
                    emitter.Init(kind, at, range.Min, range.Max, EnemySpawner.EmitterScale(kind), r, player);
                    placed.Emitters.Add(emitter);
                }
            }
            return placed;
        }
    }
}
