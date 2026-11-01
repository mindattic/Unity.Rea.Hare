using System;
using System.Collections.Generic;
using Unity.Rea.Hare.Content;

namespace Unity.Rea.Hare.Enemies
{
    /// <summary>
    /// One enemy kind's tuning as a plain, JSON-bindable record (<see cref="EnemyStats"/> is an immutable struct
    /// built from this once the catalog is loaded and validated).
    /// </summary>
    [Serializable]
    public class EnemyEntry
    {
        public int Points;
        public int PartPoints;
        /// <summary>0 small, 1 medium, 2 large (<see cref="EnemyTable.Small"/> etc.).</summary>
        public int DeathBurstSize;
        public float BBoxHalfWidthPx;
        public float BBoxHalfHeightPx;
        public float BBoxOffsetXPx;
        public float BBoxOffsetYPx;
        public bool PushesAway;
        public int DifficultyPoints;
        public float EmitterScale;
        public float WormholeScale;

        public EnemyEntry() { }

        public EnemyEntry(int points, int partPoints, int deathBurstSize, float bboxHalfW, float bboxHalfH,
            float bboxOffsetX, float bboxOffsetY, bool pushesAway, int difficultyPoints, float emitterScale, float wormholeScale)
        {
            Points = points;
            PartPoints = partPoints;
            DeathBurstSize = deathBurstSize;
            BBoxHalfWidthPx = bboxHalfW;
            BBoxHalfHeightPx = bboxHalfH;
            BBoxOffsetXPx = bboxOffsetX;
            BBoxOffsetYPx = bboxOffsetY;
            PushesAway = pushesAway;
            DifficultyPoints = difficultyPoints;
            EmitterScale = emitterScale;
            WormholeScale = wormholeScale;
        }

        internal EnemyStats ToStats() => new EnemyStats(Points, PartPoints, DeathBurstSize,
            new Vec2(BBoxHalfWidthPx, BBoxHalfHeightPx), new Vec2(BBoxOffsetXPx, BBoxOffsetYPx),
            PushesAway, DifficultyPoints, EmitterScale, WormholeScale);

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.NonNegative(errors, path + ".Points", Points);
            ValidationHelpers.NonNegative(errors, path + ".PartPoints", PartPoints);
            if (DeathBurstSize < EnemyTable.Small || DeathBurstSize > EnemyTable.Large)
                errors.Add($"{path}.DeathBurstSize: must be {EnemyTable.Small} (small), {EnemyTable.Medium} (medium) or {EnemyTable.Large} (large) (was {DeathBurstSize}).");
            ValidationHelpers.Positive(errors, path + ".BBoxHalfWidthPx", BBoxHalfWidthPx);
            ValidationHelpers.Positive(errors, path + ".BBoxHalfHeightPx", BBoxHalfHeightPx);
            ValidationHelpers.NonNegative(errors, path + ".DifficultyPoints", DifficultyPoints);
            ValidationHelpers.Positive(errors, path + ".EmitterScale", EmitterScale);
            ValidationHelpers.Positive(errors, path + ".WormholeScale", WormholeScale);
        }
    }

    /// <summary>
    /// The per-enemy stats of every spawnable kind (generate_enemy_list's index_* values), as data. Replaces the
    /// hardcoded switch <see cref="EnemyTable"/> used to carry; <see cref="EnemyTable.Get"/> now reads from here.
    /// </summary>
    [Serializable]
    public class EnemyCatalog
    {
        public EnemyEntry Hydra = new EnemyEntry(10, 0, EnemyTable.Small, 15f, 15f, 0f, 0f, true, 1, 0.25f, 0.25f);
        public EnemyEntry Sawblade = new EnemyEntry(10, 0, EnemyTable.Small, 15f, 15f, 0f, 0f, true, 1, 0.25f, 0.25f);
        public EnemyEntry Dagger = new EnemyEntry(10, 0, EnemyTable.Small, 38f, 21.5f, 8f, 0f, true, 1, 0.25f, 0.35f);
        public EnemyEntry Mimic = new EnemyEntry(10, 0, EnemyTable.Medium, 53f, 53f, 0f, 0f, false, 2, 0.25f, 1.0f);
        public EnemyEntry Wraith = new EnemyEntry(50, 0, EnemyTable.Small, 50f, 56f, 0f, 0f, false, 8, 0.35f, 0.35f);
        public EnemyEntry Meatball = new EnemyEntry(50, 0, EnemyTable.Large, 89f, 83.5f, -0.5f, 34f, true, 10, 0.70f, 1.0f);
        public EnemyEntry Phalanx = new EnemyEntry(20, 0, EnemyTable.Medium, 15f, 15f, 0f, 0f, false, 5, 0.35f, 0.35f);
        public EnemyEntry Worm = new EnemyEntry(100, 10, EnemyTable.Large, 56.5f, 56.5f, 0f, 0f, false, 15, 0.50f, 0.5f);
        public EnemyEntry Trilobyte = new EnemyEntry(10, 0, EnemyTable.Small, 26f, 24.5f, -3.5f, 1f, true, 2, 0.25f, 0.25f);
        /// <summary>obj_scrapmental: 500 for the boss, 20 (PartPoints) for each hull piece shot loose; no emitter or wormhole makes it.</summary>
        public EnemyEntry Scrapmental = new EnemyEntry(500, 20, EnemyTable.Large, 21f, 21f, 0f, -1f, false, 0, 1f, 1f);

        public EnemyStats Get(EnemyKind kind) => EntryFor(kind).ToStats();

        EnemyEntry EntryFor(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Sawblade: return Sawblade;
                case EnemyKind.Dagger: return Dagger;
                case EnemyKind.Mimic: return Mimic;
                case EnemyKind.Wraith: return Wraith;
                case EnemyKind.Meatball: return Meatball;
                case EnemyKind.Phalanx: return Phalanx;
                case EnemyKind.Worm: return Worm;
                case EnemyKind.Trilobyte: return Trilobyte;
                case EnemyKind.Scrapmental: return Scrapmental;
                default: return Hydra;
            }
        }

        internal void Validate(List<string> errors, string path)
        {
            foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
                EntryFor(kind).Validate(errors, $"{path}.{kind}");
        }
    }
}
