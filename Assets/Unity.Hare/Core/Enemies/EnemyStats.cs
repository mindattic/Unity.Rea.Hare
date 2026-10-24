using Unity.Hare.Content;

namespace Unity.Hare.Enemies
{
    /// <summary>One enemy type's tuning: the numbers the rules read, kept as data in one table.</summary>
    public readonly struct EnemyStats
    {
        /// <summary>The GML current_score value of a kill that scores.</summary>
        public readonly int Points;
        /// <summary>A worm's segment is worth this instead of <see cref="Points"/>.</summary>
        public readonly int PartPoints;
        /// <summary>Size of the kill branch's ef_smokeup and ef_firework: 0 small, 1 medium, 2 large.</summary>
        public readonly int DeathBurstSize;
        /// <summary>Half the GML bounding box (unrotated), px, for distance_to_object/point.</summary>
        public readonly Vec2 BBoxHalfPx;
        /// <summary>The bounding box centre's offset from the origin, px (y up).</summary>
        public readonly Vec2 BBoxOffsetPx;
        /// <summary>True for enemies with GML's Collision par_enemy push-apart event.</summary>
        public readonly bool PushesAway;
        /// <summary>generate_enemy_list's diff_* points.</summary>
        public readonly int DifficultyPoints;
        /// <summary>setup_level's emitter.scale for the par_enemy_emitter it places.</summary>
        public readonly float EmitterScale;
        /// <summary>create_wormhole's wormhole.scale.</summary>
        public readonly float WormholeScale;

        public EnemyStats(int points, int partPoints, int deathBurstSize, Vec2 bboxHalfPx, Vec2 bboxOffsetPx, bool pushesAway,
            int difficultyPoints, float emitterScale, float wormholeScale)
        {
            Points = points;
            PartPoints = partPoints;
            DeathBurstSize = deathBurstSize;
            BBoxHalfPx = bboxHalfPx;
            BBoxOffsetPx = bboxOffsetPx;
            PushesAway = pushesAway;
            DifficultyPoints = difficultyPoints;
            EmitterScale = emitterScale;
            WormholeScale = wormholeScale;
        }
    }

    /// <summary>
    /// The per-enemy stats of every spawnable kind. The numbers themselves are data now (<see cref="EnemyCatalog"/>,
    /// loaded and validated by <see cref="ContentRegistry"/>, AGENTS.md rule 4); this stays the stable entry point
    /// the enemy rules call so none of their 20-odd call sites had to change.
    /// </summary>
    public static class EnemyTable
    {
        public const int Small = 0, Medium = 1, Large = 2;

        public static EnemyStats Get(EnemyKind kind) => GameContent.Current.Enemies.Get(kind);
    }
}
