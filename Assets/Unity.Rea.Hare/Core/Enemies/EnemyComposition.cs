using System;
using System.Collections.Generic;
using Unity.Rea.Hare.Content;

namespace Unity.Rea.Hare.Enemies
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
    public sealed class CompositionPlacement
    {
        public readonly List<EmitterBody> Emitters = new List<EmitterBody>();
        /// <summary>Mimics, which setup_level creates directly instead of through an emitter.</summary>
        public readonly List<MimicEnemy> Mimics = new List<MimicEnemy>();
    }

    /// <summary>
    /// The enemy half of a level's composition (hydra_min/max ... trilobyte_min/max, all 0 after setup_level's reset)
    /// and setup_level's "Generate X" blocks that turn it into spawners. For each type, rnd_X =
    /// irandom_range(X_min, X_max) and setup_level then creates that many mimics outright at random points in the
    /// room; for every other type, places that many emitters (instance = the type, instance_min/max = X_min/X_max,
    /// waits 200-500 steps, scale per <see cref="EnemyStats.EmitterScale"/>) at random points; each emitter then
    /// keeps the type's live count topped up to X_max. The level scripts that fill it in are the levels ticket's.
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
        /// setup_level's enemy blocks. <paramref name="counts"/> pins each type's rnd_X (tests); otherwise it is
        /// irandom_range(min, max). Run by <see cref="PlaceComposition"/>.
        /// </summary>
        internal CompositionPlacement Place(EnemyWorld world, RoomBounds room, IDictionary<EnemyKind, int> counts)
        {
            var placed = new CompositionPlacement();
            // Every rnd_X is rolled before any block runs, as in setup_level.
            var rolls = new Dictionary<EnemyKind, int>();
            foreach (var kind in PlacementOrder)
                rolls[kind] = counts != null && counts.TryGetValue(kind, out int n) ? n : world.Random.IRandom(this[kind].Min, this[kind].Max);

            foreach (var kind in PlacementOrder)
            {
                var range = this[kind];
                for (int i = 0; i < rolls[kind]; i++)
                {
                    // rnd_x = irandom_range(0, room_width); rnd_y = irandom_range(0, room_height)
                    Vec2 at = SpawnPoints.RandomPointInside(world.Random, room, 0f);
                    if (kind == EnemyKind.Mimic)
                    {
                        placed.Mimics.Add((MimicEnemy)world.Spawn(kind, at, room, null));
                        continue;
                    }
                    var emitter = world.AddVortex(new EmitterBody());
                    var timing = GameContent.Current.Emitters;
                    emitter.Create(kind, at, range.Min, range.Max, EnemyTable.Get(kind).EmitterScale, room,
                        timing.DefaultWaitMinSteps, timing.DefaultWaitMaxSteps, null);
                    placed.Emitters.Add(emitter);
                }
            }
            return placed;
        }
    }
}
