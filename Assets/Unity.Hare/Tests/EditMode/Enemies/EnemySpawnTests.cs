using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Enemies;
using Unity.Hare.Weapons;
using static Unity.Hare.Tests.EditMode.EnemyTestKit;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>UH-T15: generate_enemy_list, par_enemy_emitter, obj_wormhole(_emitter) and setup_level's placement.</summary>
    public class EnemySpawnTests
    {
        [SetUp]
        public void SetUp()
        {
            Random.InitState(1515);
            Gml.Paused = false;
        }

        [TearDown]
        public void TearDown()
        {
            Gml.Paused = false;
            DestroyAll<Vortex>();
            DestroyAll<WormholeEmitter>();
            DestroyAll<Dagger>();
            Cleanup();
        }

        static void Run(Vortex v, int steps)
        {
            for (int i = 0; i < steps && v != null; i++) v.Tick(Step);
        }

        // ---- par_enemy_emitter ----

        static EnemyEmitter Emitter(EnemyKind kind, int max, int firstAlarm, int wait = 10)
        {
            var e = new GameObject("Emitter").AddComponent<EnemyEmitter>();
            e.Init(kind, Vector2.zero, 0, max, EnemySpawner.EmitterScale(kind), Gml.Room, null, wait, wait, firstAlarm);
            return e;
        }

        [Test]
        public void IdleJumpsInsideTheRoomAndWaitsInstanceWait()
        {
            var e = Emitter(EnemyKind.Hydra, 2, 100, 300);
            Assert.AreEqual(VortexMode.Idle, e.Mode);
            Assert.AreEqual(0f, e.ImageScale);
            Run(e, 1);
            Assert.AreEqual(VortexMode.Idle, e.Mode, "Create's alarm is overwritten by idle's first pass");
            Assert.AreEqual(300f, e.AlarmSteps, 1e-3f);
            float buffer = Gml.Px(e.BufferPx);
            Vector2 p = e.transform.position;
            Assert.That(p.x, Is.InRange(Gml.Room.xMin + buffer - 1e-3f, Gml.Room.xMax - buffer + 1e-3f));
            Assert.That(p.y, Is.InRange(Gml.Room.yMin + buffer - 1e-3f, Gml.Room.yMax - buffer + 1e-3f));
        }

        [Test]
        public void CycleOpensSpawnsClosesAndSpinsTheVortex()
        {
            var e = Emitter(EnemyKind.Sawblade, 1, 100);
            Run(e, 1);
            float angle = e.ImageAngle, back = e.BackAngle;
            Run(e, 10); // wait
            Assert.AreEqual(VortexMode.Open, e.Mode);
            Assert.AreEqual(-50f, Mathf.DeltaAngle(angle, e.ImageAngle), 1e-3f, "rot_speed -5");
            Assert.AreEqual(-60f, Mathf.DeltaAngle(back, e.BackAngle), 1e-3f, "rot_speed - 1");

            Run(e, 119);
            Assert.AreEqual(0.25f, e.ImageScale, 1e-4f, "grows 0.01 a step to scale");
            Assert.AreEqual(0, EnemySpawner.Count(EnemyKind.Sawblade));
            Run(e, 1);
            Assert.AreEqual(VortexMode.Spawn, e.Mode);
            Assert.AreEqual(1, EnemySpawner.Count(EnemyKind.Sawblade));
            Assert.IsInstanceOf<Sawblade>(e.LastSpawned);
            Assert.AreEqual((Vector2)e.transform.position, (Vector2)e.LastSpawned.transform.position);

            Run(e, 120);
            Assert.AreEqual(VortexMode.Close, e.Mode);
            Run(e, 120);
            Assert.AreEqual(VortexMode.Idle, e.Mode);
            Assert.AreEqual(0f, e.ImageScale, "closed");
        }

        [Test]
        public void InstanceMaxCapsTheLiveCountRoomWide()
        {
            var e = Emitter(EnemyKind.Hydra, 1, 100);
            Run(e, 1 + 10 + 120);
            Assert.AreEqual(1, EnemySpawner.Count(EnemyKind.Hydra));
            Run(e, 120 + 120 + 1 + 10 + 120); // close, idle, open again
            Assert.AreEqual(VortexMode.Spawn, e.Mode);
            Assert.AreEqual(0f, e.ImageScale, "no growth at the cap");
            Assert.IsNull(e.LastSpawned);
            Assert.AreEqual(1, EnemySpawner.Count(EnemyKind.Hydra));

            Object.DestroyImmediate(Object.FindAnyObjectByType<Hydra>().gameObject);
            Run(e, 120 + 120 + 1 + 10 + 120);
            Assert.AreEqual(1, EnemySpawner.Count(EnemyKind.Hydra), "tops the population back up");
        }

        [Test]
        public void PausedEmittersNeitherGrowNorSpawn()
        {
            var e = Emitter(EnemyKind.Dagger, 3, 100);
            Run(e, 1);
            float angle = e.ImageAngle;
            Gml.Paused = true;
            Run(e, 10 + 120);
            Assert.AreEqual(VortexMode.Spawn, e.Mode, "alarms still run");
            Assert.AreEqual(angle, e.ImageAngle);
            Assert.AreEqual(0f, e.ImageScale);
            Assert.AreEqual(0, EnemySpawner.Count(EnemyKind.Dagger));
        }

        // ---- obj_wormhole ----

        [Test]
        public void WormholeReleasesItsEnemyThenClosesAndGoes()
        {
            var w = Wormhole.Create(EnemyKind.Meatball, Gml.Room, null, Vector2.zero);
            Assert.AreEqual(1.0f, w.Scale, "create_wormhole: meatball 1.0");
            Run(w, 240);
            Assert.AreEqual(VortexMode.Open, w.Mode);
            Run(w, 239);
            Assert.AreEqual(1.0f, w.ImageScale, 1e-3f);
            Assert.IsNull(w.Spawned);
            Run(w, 1);
            Assert.IsInstanceOf<Meatball>(w.Spawned);
            Run(w, 240);
            Assert.AreEqual(VortexMode.Close, w.Mode);
            Run(w, 98); // the closing step itself shrank it once
            Assert.IsTrue(w != null);
            Run(w, 2);
            Assert.IsTrue(w == null, "destroyed once shrunk to nothing");
        }

        [Test]
        public void WormholeScalesAndPlacementFollowCreateWormhole()
        {
            Assert.AreEqual(0.25f, EnemySpawner.WormholeScale(EnemyKind.Hydra));
            Assert.AreEqual(0.35f, EnemySpawner.WormholeScale(EnemyKind.Dagger));
            Assert.AreEqual(0.5f, EnemySpawner.WormholeScale(EnemyKind.Worm));
            Assert.AreEqual(1.0f, EnemySpawner.WormholeScale(EnemyKind.Mimic));
            for (int i = 0; i < 20; i++)
            {
                var w = Wormhole.Create(EnemyKind.Mimic);
                float size = Gml.Px(Vortex.SpriteWidthPx);
                Vector2 p = w.transform.position;
                Assert.That(p.x, Is.InRange(Gml.Room.xMin + size - 1e-3f, Gml.Room.xMax - size + 1e-3f));
                Assert.That(p.y, Is.InRange(Gml.Room.yMin + size - 1e-3f, Gml.Room.yMax - size + 1e-3f));
            }
        }

        // ---- obj_wormhole_emitter ----

        [Test]
        public void OpensTenPercentAtOnceThenPacesTheRest()
        {
            var emitter = new GameObject("WE").AddComponent<WormholeEmitter>();
            emitter.Init(Enumerable.Repeat(EnemyKind.Hydra, 25), Gml.Room, null, 10);
            Assert.AreEqual(2, emitter.CurrentEnemy, "repeat(25 * 0.1)");
            Assert.AreEqual(2, Object.FindObjectsByType<Wormhole>(FindObjectsSortMode.None).Length);

            for (int i = 0; i < 9; i++) emitter.Tick(Step);
            Assert.AreEqual(2, emitter.CurrentEnemy);
            emitter.Tick(Step);
            Assert.AreEqual(3, emitter.CurrentEnemy);
            float rate = emitter.AlarmSteps;
            CollectionAssert.Contains(new[] { 1f, 121f, 241f, 361f, 481f, 601f, 721f, 841f, 961f }, rate, "1 + room_speed * irandom_range(0, 8)");
        }

        [Test]
        public void SpentListCompletesTheLevel()
        {
            var emitter = new GameObject("WE").AddComponent<WormholeEmitter>();
            int completed = 0;
            emitter.LevelCompleted += _ => completed++;
            emitter.Init(new[] { EnemyKind.Dagger, EnemyKind.Worm, EnemyKind.Sawblade }, Gml.Room, null, 1);
            Assert.AreEqual(0, emitter.CurrentEnemy, "3 * 0.1 opens none");

            for (int i = 0; i < 4000 && emitter != null; i++) emitter.Tick(Step);
            Assert.AreEqual(1, completed);
            Assert.IsTrue(emitter == null, "instance_destroy");
            Assert.IsTrue(Gml.Paused, "set_level_complete pauses");
            var kinds = Object.FindObjectsByType<Wormhole>(FindObjectsSortMode.None).Select(w => w.Instance).OrderBy(k => k);
            CollectionAssert.AreEqual(new[] { EnemyKind.Sawblade, EnemyKind.Dagger, EnemyKind.Worm }, kinds);
        }

        [Test]
        public void CreateGeneratesTheListFromDifficulty()
        {
            var emitter = WormholeEmitter.Create(40);
            Assert.GreaterOrEqual(EnemyList.TotalPoints(emitter.List), 40);
            Assert.AreEqual(Mathf.FloorToInt(emitter.List.Count * 0.1f), emitter.CurrentEnemy);
        }

        // ---- setup_level placement ----

        [Test]
        public void CompositionPlacesEmittersPerTypeAndMimicsDirectly()
        {
            var comp = new EnemyComposition()
                .With(EnemyKind.Hydra, 1, 3)
                .With(EnemyKind.Meatball, 1, 2)
                .With(EnemyKind.Mimic, 2, 2);
            var counts = new Dictionary<EnemyKind, int> { { EnemyKind.Hydra, 3 }, { EnemyKind.Meatball, 1 }, { EnemyKind.Mimic, 2 } };
            var placed = comp.Place(Gml.Room, null, counts);

            Assert.AreEqual(4, placed.Emitters.Count);
            Assert.AreEqual(2, placed.Mimics.Count);
            Assert.AreEqual(2, EnemySpawner.Count(EnemyKind.Mimic));
            var hydra = placed.Emitters.Where(e => e.Instance == EnemyKind.Hydra).ToList();
            Assert.AreEqual(3, hydra.Count);
            Assert.IsTrue(hydra.All(e => e.InstanceMax == 3 && e.InstanceMin == 1 && Mathf.Approximately(e.Scale, 0.25f)));
            Assert.IsTrue(hydra.All(e => e.WaitMinSteps == 200 && e.WaitMaxSteps == 500));
            var meatball = placed.Emitters.Single(e => e.Instance == EnemyKind.Meatball);
            Assert.AreEqual(0.70f, meatball.Scale, 1e-4f);
            Assert.That(meatball.AlarmSteps, Is.InRange(1f, 240f), "Create: irandom_range(1, room_speed * 2)");
        }

        [Test]
        public void EmptyCompositionPlacesNothing()
        {
            var placed = new EnemyComposition().Place();
            Assert.IsEmpty(placed.Emitters);
            Assert.IsEmpty(placed.Mimics);
        }

        [Test]
        public void EverySpawnableKindCanBeCreatedAndCounted()
        {
            foreach (EnemyKind kind in System.Enum.GetValues(typeof(EnemyKind)))
            {
                var enemy = EnemySpawner.Spawn(kind, Vector2.zero);
                Assert.IsNotNull(enemy, kind.ToString());
                Assert.AreEqual(1, EnemySpawner.Count(kind), kind.ToString());
            }
        }
    }
}
