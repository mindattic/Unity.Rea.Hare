using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using Unity.Hare.Enemies;

namespace Unity.Hare.Tests.Core
{
    /// <summary>generate_enemy_list: the difficulty table and the list generator, run outside Unity.</summary>
    public class EnemyListTests
    {
        static Func<int, int, int> Rng(int seed)
        {
            var random = new Random(seed);
            return (a, b) => a <= b ? random.Next(a, b + 1) : random.Next(b, a + 1);
        }

        [Test]
        public void DifficultyPointsMatchTheDiffTable()
        {
            var expected = new Dictionary<EnemyKind, int>
            {
                { EnemyKind.Hydra, 1 }, { EnemyKind.Sawblade, 1 }, { EnemyKind.Dagger, 1 }, { EnemyKind.Mimic, 2 },
                { EnemyKind.Wraith, 8 }, { EnemyKind.Meatball, 10 }, { EnemyKind.Phalanx, 5 }, { EnemyKind.Worm, 15 },
                { EnemyKind.Trilobyte, 2 }
            };
            foreach (var pair in expected) Assert.AreEqual(pair.Value, EnemyList.DifficultyPoints(pair.Key), pair.Key.ToString());
        }

        [Test]
        public void ZeroDifficultyGivesAnEmptyList()
        {
            Assert.IsEmpty(EnemyList.Generate(0, Rng(1)));
        }

        [Test]
        public void ListStopsOnTheFirstPickThatReachesTheDifficulty()
        {
            var rng = Rng(1515);
            for (int run = 0; run < 50; run++)
            {
                var list = EnemyList.Generate(100, rng);
                int total = EnemyList.TotalPoints(list);
                Assert.GreaterOrEqual(total, 100);
                Assert.Less(total - EnemyList.DifficultyPoints(list[list.Count - 1]), 100, "no pick after the target is met");
            }
        }

        [Test]
        public void PicksAreUniformOverTheNineKinds()
        {
            var rng = Rng(1515);
            var counts = new Dictionary<EnemyKind, int>();
            for (int run = 0; run < 200; run++)
                foreach (var kind in EnemyList.Generate(100, rng))
                    counts[kind] = counts.TryGetValue(kind, out int n) ? n + 1 : 1;
            Assert.AreEqual(9, counts.Count, "irandom_range(1, 9) reaches every kind");
            int all = counts.Values.Sum();
            foreach (var pair in counts) Assert.That(pair.Value / (float)all, Is.InRange(0.08f, 0.145f), pair.Key.ToString());
        }
    }
}
