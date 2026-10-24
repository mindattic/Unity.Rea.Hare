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

    /// <summary>The per-enemy stats of every spawnable kind.</summary>
    public static class EnemyTable
    {
        public const int Small = 0, Medium = 1, Large = 2;

        static readonly EnemyStats Hydra = new EnemyStats(10, 0, Small, new Vec2(15f, 15f), Vec2.Zero, true, 1, 0.25f, 0.25f);
        static readonly EnemyStats Sawblade = new EnemyStats(10, 0, Small, new Vec2(15f, 15f), Vec2.Zero, true, 1, 0.25f, 0.25f);
        static readonly EnemyStats Dagger = new EnemyStats(10, 0, Small, new Vec2(38f, 21.5f), new Vec2(8f, 0f), true, 1, 0.25f, 0.35f);
        static readonly EnemyStats Mimic = new EnemyStats(10, 0, Medium, new Vec2(53f, 53f), Vec2.Zero, false, 2, 0.25f, 1.0f);
        static readonly EnemyStats Wraith = new EnemyStats(50, 0, Small, new Vec2(50f, 56f), Vec2.Zero, false, 8, 0.35f, 0.35f);
        static readonly EnemyStats Meatball = new EnemyStats(50, 0, Large, new Vec2(89f, 83.5f), new Vec2(-0.5f, 34f), true, 10, 0.70f, 1.0f);
        static readonly EnemyStats Phalanx = new EnemyStats(20, 0, Medium, new Vec2(15f, 15f), Vec2.Zero, false, 5, 0.35f, 0.35f);
        static readonly EnemyStats Worm = new EnemyStats(100, 10, Large, new Vec2(56.5f, 56.5f), Vec2.Zero, false, 15, 0.50f, 0.5f);
        static readonly EnemyStats Trilobyte = new EnemyStats(10, 0, Small, new Vec2(26f, 24.5f), new Vec2(-3.5f, 1f), true, 2, 0.25f, 0.25f);

        public static EnemyStats Get(EnemyKind kind)
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
                default: return Hydra;
            }
        }
    }
}
