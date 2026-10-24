using System;
using System.Collections.Generic;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// Ports generate_enemy_list (gmlHARE): while the running difficulty is below
    /// argument0, pick irandom_range(1, 9) uniformly, append that enemy and add its diff_* points.
    /// The list therefore overshoots the target by up to the last pick's points, and a difficulty
    /// of 0 (obj_btn_diff0) yields an empty list.
    /// </summary>
    /// <remarks>
    /// The original appends to the globals enemy_list/enemy_count, which nothing ever resets, so a
    /// second generator level would replay the first one's enemies; this port returns a fresh
    /// list per call instead.
    /// </remarks>
    public static class EnemyList
    {
        /// <summary>generate_enemy_list's diff_* points.</summary>
        public static int DifficultyPoints(EnemyKind kind) => EnemyTable.Get(kind).DifficultyPoints;

        /// <summary>
        /// generate_enemy_list(difficulty): the enemies an obj_wormhole_emitter releases, in order.
        /// <paramref name="random"/> deals GML irandom_range(1, 9), inclusive.
        /// </summary>
        public static List<EnemyKind> Generate(int difficulty, IRandomSource random) => Generate(difficulty, random.IRandom);

        /// <summary>generate_enemy_list over a bare irandom_range function.</summary>
        public static List<EnemyKind> Generate(int difficulty, Func<int, int, int> irandom)
        {
            var list = new List<EnemyKind>();
            int current = 0;
            while (current < difficulty)
            {
                var kind = (EnemyKind)irandom(1, 9);
                list.Add(kind);
                current += DifficultyPoints(kind);
            }
            return list;
        }

        /// <summary>The summed diff_* points of <paramref name="list"/>.</summary>
        public static int TotalPoints(IEnumerable<EnemyKind> list)
        {
            int total = 0;
            foreach (var kind in list) total += DifficultyPoints(kind);
            return total;
        }
    }
}
