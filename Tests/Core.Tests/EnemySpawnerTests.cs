using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using Unity.Rea.Hare.Enemies;
using Unity.Rea.Hare.Player;
using static Unity.Rea.Hare.Tests.Core.EnemyRig;

namespace Unity.Rea.Hare.Tests.Core
{
    /// <summary>The insects, emitters, wormholes, composition and the world's reproducibility, run outside Unity.</summary>
    public class EnemySpawnerTests
    {
        static void RunVortex(EnemyRig rig, VortexBody v, int steps)
        {
            for (int i = 0; i < steps && !v.IsGone; i++) rig.Do(new TickVortex(v.Id, Step));
        }

        // ---- Insects ----

        [Test]
        public void InsectCreatePlacesThemInTheLowerHalfMovingOneByOne()
        {
            var rig = new EnemyRig(1234);
            for (int i = 0; i < 50; i++)
            {
                var fly = rig.Do(new SpawnInsect(InsectKind.Fly)).Insect;
                Assert.That(fly.Position.X, Is.InRange(rig.Room.XMin, rig.Room.XMax));
                Assert.That(fly.Position.Y, Is.InRange(rig.Room.YMin, rig.Room.CenterY), "y = random_range(room_height/2, room_height)");
                Assert.AreEqual(1f, fly.DX);
                Assert.AreEqual(1f, fly.DY);
                Assert.That(fly.ThinkCountdownSteps, Is.InRange(5f, 30f), "alarm[0] = irandom_range(5,30)");
            }
        }

        [Test]
        public void InsectStepMovesByDxDyPixelsPerStepWithGmlYDown()
        {
            var rig = new EnemyRig(1234);
            var fly = rig.Do(new SpawnInsect(InsectKind.Fly, null, new Vec2(0f, -3f))).Insect;
            Vec2 start = fly.Position;
            rig.Do(new TickInsect(fly.Id, Step)); // dX = dY = 1 until the first think
            Assert.AreEqual(GameClock.Px(1f), fly.Position.X - start.X, 1e-5f);
            Assert.AreEqual(-GameClock.Px(1f), fly.Position.Y - start.Y, 1e-5f, "GML y += 1 moves down the screen");
        }

        static Vec2 LowerMiddle(EnemyRig rig) => new Vec2(0f, rig.Room.CenterY - 3f);

        [Test]
        public void ButterflyStaysInTheLowerHalfAndFacesItsHeading()
        {
            var rig = new EnemyRig(1234);
            var b = (ButterflyBody)rig.Do(new SpawnInsect(InsectKind.Butterfly, null, LowerMiddle(rig))).Insect;
            for (int i = 0; i < 100; i++)
            {
                rig.Do(new ThinkInsect(b.Id));
                Assert.That(b.DX, Is.InRange(-2f, 2f));
                Assert.That(b.DY, Is.InRange(-2f, 2f));
                Assert.AreEqual(b.DX > 0f, b.FacingRight);
            }
            rig.Do(new PlaceBody(b.Id, new Vec2(0f, rig.Room.CenterY + 3f)));
            rig.Do(new ThinkInsect(b.Id));
            Assert.AreEqual(2f, b.DY, "upper half: dY = 2 (down)");
            rig.Do(new PlaceBody(b.Id, new Vec2(0f, rig.Room.YMin + GameClock.Px(60f))));
            rig.Do(new ThinkInsect(b.Id));
            Assert.AreEqual(-2f, b.DY, "within 120 px of the floor: dY = -2 (up)");
            rig.Do(new PlaceBody(b.Id, new Vec2(rig.Room.XMin + GameClock.Px(5f), rig.Room.CenterY - 3f)));
            rig.Do(new ThinkInsect(b.Id));
            Assert.AreEqual(2f, b.DX);
            Assert.IsTrue(b.FacingRight);
            rig.Do(new PlaceBody(b.Id, new Vec2(rig.Room.XMax - GameClock.Px(5f), rig.Room.CenterY - 3f)));
            rig.Do(new ThinkInsect(b.Id));
            Assert.AreEqual(-2f, b.DX);
            Assert.IsFalse(b.FacingRight);
        }

        [Test]
        public void FireflyDriftsSlowlyAndBlinksOnAboutOneThinkInFive()
        {
            var rig = new EnemyRig(1234);
            var f = (FireflyBody)rig.Do(new SpawnInsect(InsectKind.Firefly, null, LowerMiddle(rig))).Insect;
            Assert.IsFalse(f.IsDark);
            int toggles = 0;
            bool dark = f.IsDark;
            for (int i = 0; i < 2000; i++)
            {
                rig.Do(new PlaceBody(f.Id, LowerMiddle(rig)));
                rig.Do(new ThinkInsect(f.Id));
                Assert.That(f.DX, Is.InRange(-3f, 3f));
                Assert.That(f.DY, Is.InRange(-1f, 1f));
                if (f.IsDark != dark) { toggles++; dark = f.IsDark; }
            }
            Assert.That(toggles, Is.InRange(300, 500), "irandom(4) == 4");
            rig.Do(new PlaceBody(f.Id, new Vec2(0f, rig.Room.CenterY + 3f)));
            rig.Do(new ThinkInsect(f.Id));
            Assert.AreEqual(1f, f.DY);
        }

        [Test]
        public void FlyDartsFastestAndSinksAtOnePixelInTheUpperHalf()
        {
            var rig = new EnemyRig(1234);
            var fly = rig.Do(new SpawnInsect(InsectKind.Fly, null, LowerMiddle(rig))).Insect;
            for (int i = 0; i < 100; i++)
            {
                rig.Do(new ThinkInsect(fly.Id));
                Assert.That(fly.DX, Is.InRange(-4f, 4f));
                Assert.That(fly.DY, Is.InRange(-2f, 2f));
            }
            rig.Do(new PlaceBody(fly.Id, new Vec2(0f, rig.Room.CenterY + 3f)));
            rig.Do(new ThinkInsect(fly.Id));
            Assert.AreEqual(1f, fly.DY);
            rig.Do(new PlaceBody(fly.Id, new Vec2(rig.Room.XMin + GameClock.Px(5f), rig.Room.CenterY - 3f)));
            rig.Do(new ThinkInsect(fly.Id));
            Assert.AreEqual(4f, fly.DX);
        }

        [Test]
        public void InsectsAreNotEnemiesTheWeaponsCanTarget()
        {
            var rig = new EnemyRig(1234);
            rig.Do(new SpawnInsect(InsectKind.Butterfly, null, LowerMiddle(rig)));
            Assert.IsNull(rig.World.NearestEnemy(LowerMiddle(rig)));
            Assert.AreEqual(0, rig.World.Enemies.Count);
        }

        // ---- par_enemy_emitter ----

        EmitterBody Emitter(EnemyRig rig, EnemyKind kind, int max, int firstAlarm, int wait = 10) =>
            (EmitterBody)rig.Do(new AddEmitter(kind, Vec2.Zero, 0, max, EnemyTable.Get(kind).EmitterScale, null, wait, wait, firstAlarm)).Vortex;

        [Test]
        public void EmitterIdleJumpsInsideTheRoomAndWaitsInstanceWait()
        {
            var rig = new EnemyRig(1515);
            var e = Emitter(rig, EnemyKind.Hydra, 2, 100, 300);
            Assert.AreEqual(VortexMode.Idle, e.Mode);
            Assert.AreEqual(0f, e.ImageScale);
            RunVortex(rig, e, 1);
            Assert.AreEqual(VortexMode.Idle, e.Mode, "Create's alarm is overwritten by idle's first pass");
            Assert.AreEqual(300f, e.AlarmSteps, 1e-3f);
            float buffer = GameClock.Px(e.BufferPx);
            Assert.That(e.Position.X, Is.InRange(rig.Room.XMin + buffer - 1e-3f, rig.Room.XMax - buffer + 1e-3f));
            Assert.That(e.Position.Y, Is.InRange(rig.Room.YMin + buffer - 1e-3f, rig.Room.YMax - buffer + 1e-3f));
        }

        [Test]
        public void EmitterCycleOpensSpawnsClosesAndSpinsTheVortex()
        {
            var rig = new EnemyRig(1515);
            var e = Emitter(rig, EnemyKind.Sawblade, 1, 100);
            RunVortex(rig, e, 1);
            float angle = e.ImageAngle, back = e.BackAngle;
            RunVortex(rig, e, 10);
            Assert.AreEqual(VortexMode.Open, e.Mode);
            Assert.AreEqual(-50f, Maths.DeltaAngle(angle, e.ImageAngle), 1e-3f, "rot_speed -5");
            Assert.AreEqual(-60f, Maths.DeltaAngle(back, e.BackAngle), 1e-3f, "rot_speed - 1");

            RunVortex(rig, e, 119);
            Assert.AreEqual(0.25f, e.ImageScale, 1e-4f, "grows 0.01 a step to scale");
            Assert.AreEqual(0, rig.World.Count(EnemyKind.Sawblade));
            RunVortex(rig, e, 1);
            Assert.AreEqual(VortexMode.Spawn, e.Mode);
            Assert.AreEqual(1, rig.World.Count(EnemyKind.Sawblade));
            Assert.IsInstanceOf<SawbladeEnemy>(e.LastSpawned);
            Assert.AreEqual(e.Position, e.LastSpawned.Position);
            Assert.AreEqual(1, rig.Seen<VortexReleased>().Count());

            RunVortex(rig, e, 120);
            Assert.AreEqual(VortexMode.Close, e.Mode);
            RunVortex(rig, e, 120);
            Assert.AreEqual(VortexMode.Idle, e.Mode);
            Assert.AreEqual(0f, e.ImageScale, "closed");
        }

        [Test]
        public void EmitterInstanceMaxCapsTheLiveCountRoomWide()
        {
            var rig = new EnemyRig(1515);
            var e = Emitter(rig, EnemyKind.Hydra, 1, 100);
            RunVortex(rig, e, 1 + 10 + 120);
            Assert.AreEqual(1, rig.World.Count(EnemyKind.Hydra));
            RunVortex(rig, e, 120 + 120 + 1 + 10 + 120);
            Assert.AreEqual(VortexMode.Spawn, e.Mode);
            Assert.AreEqual(0f, e.ImageScale, "no growth at the cap");
            Assert.IsNull(e.LastSpawned);
            Assert.AreEqual(1, rig.World.Count(EnemyKind.Hydra));

            rig.Do(new RemoveBody(rig.World.Enemies.Single(b => b.Kind == EnemyKind.Hydra).Id));
            RunVortex(rig, e, 120 + 120 + 1 + 10 + 120);
            Assert.AreEqual(1, rig.World.Count(EnemyKind.Hydra), "tops the population back up");
        }

        [Test]
        public void EmitterPausedNeitherGrowsNorSpawns()
        {
            var rig = new EnemyRig(1515);
            var e = Emitter(rig, EnemyKind.Dagger, 3, 100);
            RunVortex(rig, e, 1);
            float angle = e.ImageAngle;
            rig.Do(new SetPaused(true));
            RunVortex(rig, e, 10 + 120);
            Assert.AreEqual(VortexMode.Spawn, e.Mode, "alarms still run");
            Assert.AreEqual(angle, e.ImageAngle);
            Assert.AreEqual(0f, e.ImageScale);
            Assert.AreEqual(0, rig.World.Count(EnemyKind.Dagger));
        }

        // ---- obj_wormhole ----

        [Test]
        public void WormholeReleasesItsEnemyThenClosesAndGoes()
        {
            var rig = new EnemyRig(1515);
            var w = (WormholeBody)rig.Do(new AddWormhole(EnemyKind.Meatball, null, Vec2.Zero)).Vortex;
            Assert.AreEqual(1.0f, w.Scale, "create_wormhole: meatball 1.0");
            RunVortex(rig, w, 240);
            Assert.AreEqual(VortexMode.Open, w.Mode);
            RunVortex(rig, w, 239);
            Assert.AreEqual(1.0f, w.ImageScale, 1e-3f);
            Assert.IsNull(w.Spawned);
            RunVortex(rig, w, 1);
            Assert.IsInstanceOf<MeatballEnemy>(w.Spawned);
            RunVortex(rig, w, 240);
            Assert.AreEqual(VortexMode.Close, w.Mode);
            RunVortex(rig, w, 98);
            Assert.IsFalse(w.IsGone);
            RunVortex(rig, w, 2);
            Assert.IsTrue(w.IsGone, "removed once shrunk to nothing");
            Assert.AreEqual(1, rig.Seen<VortexClosed>().Count());
            Assert.IsEmpty(rig.World.Vortices);
        }

        [Test]
        public void WormholeScalesAndPlacementFollowCreateWormhole()
        {
            Assert.AreEqual(0.25f, EnemyTable.Get(EnemyKind.Hydra).WormholeScale);
            Assert.AreEqual(0.35f, EnemyTable.Get(EnemyKind.Dagger).WormholeScale);
            Assert.AreEqual(0.5f, EnemyTable.Get(EnemyKind.Worm).WormholeScale);
            Assert.AreEqual(1.0f, EnemyTable.Get(EnemyKind.Mimic).WormholeScale);
            var rig = new EnemyRig(1515);
            for (int i = 0; i < 20; i++)
            {
                var w = rig.Do(new AddWormhole(EnemyKind.Mimic)).Vortex;
                float size = GameClock.Px(VortexBody.SpriteWidthPx);
                Assert.That(w.Position.X, Is.InRange(rig.Room.XMin + size - 1e-3f, rig.Room.XMax - size + 1e-3f));
                Assert.That(w.Position.Y, Is.InRange(rig.Room.YMin + size - 1e-3f, rig.Room.YMax - size + 1e-3f));
            }
        }

        // ---- obj_wormhole_emitter ----

        [Test]
        public void WormholeEmitterOpensTenPercentAtOnceThenPacesTheRest()
        {
            var rig = new EnemyRig(1515);
            var emitter = rig.Do(new AddWormholeEmitter(Enumerable.Repeat(EnemyKind.Hydra, 25), null, 10)).Emitter;
            Assert.AreEqual(2, emitter.CurrentEnemy, "repeat(25 * 0.1)");
            Assert.AreEqual(2, rig.World.Vortices.Count());
            for (int i = 0; i < 9; i++) rig.Do(new TickWormholeEmitter(emitter.Id, Step));
            Assert.AreEqual(2, emitter.CurrentEnemy);
            rig.Do(new TickWormholeEmitter(emitter.Id, Step));
            Assert.AreEqual(3, emitter.CurrentEnemy);
            CollectionAssert.Contains(new[] { 1f, 121f, 241f, 361f, 481f, 601f, 721f, 841f, 961f }, emitter.AlarmSteps, "1 + room_speed * irandom_range(0, 8)");
        }

        [Test]
        public void WormholeEmitterSpentListCompletesTheLevel()
        {
            var rig = new EnemyRig(1515);
            var emitter = rig.Do(new AddWormholeEmitter(new[] { EnemyKind.Dagger, EnemyKind.Worm, EnemyKind.Sawblade }, null, 1)).Emitter;
            Assert.AreEqual(0, emitter.CurrentEnemy, "3 * 0.1 opens none");
            for (int i = 0; i < 4000 && !emitter.Finished; i++) rig.Do(new TickWormholeEmitter(emitter.Id, Step));
            Assert.AreEqual(1, rig.Seen<LevelCompleted>().Count());
            Assert.IsTrue(emitter.Finished);
            Assert.IsTrue(rig.World.Paused, "set_level_complete pauses");
            var kinds = emitter.Wormholes.Select(w => w.Instance).OrderBy(k => k);
            CollectionAssert.AreEqual(new[] { EnemyKind.Sawblade, EnemyKind.Dagger, EnemyKind.Worm }, kinds);
            rig.Do(new TickWormholeEmitter(emitter.Id, Step));
            Assert.AreEqual(1, rig.Seen<LevelCompleted>().Count(), "gone: it never completes twice");
        }

        [Test]
        public void WormholeEmitterGeneratesTheListFromDifficultyOnTheWorldsSeed()
        {
            var rig = new EnemyRig(1515);
            var emitter = rig.Do(new AddWormholeEmitter(40)).Emitter;
            Assert.GreaterOrEqual(EnemyList.TotalPoints(emitter.List), 40);
            Assert.AreEqual((int)Math.Floor(emitter.List.Count * 0.1f), emitter.CurrentEnemy);
        }

        // ---- setup_level placement ----

        [Test]
        public void CompositionPlacesEmittersPerTypeAndMimicsDirectly()
        {
            var rig = new EnemyRig(1515);
            var comp = new EnemyComposition()
                .With(EnemyKind.Hydra, 1, 3)
                .With(EnemyKind.Meatball, 1, 2)
                .With(EnemyKind.Mimic, 2, 2);
            var counts = new Dictionary<EnemyKind, int> { { EnemyKind.Hydra, 3 }, { EnemyKind.Meatball, 1 }, { EnemyKind.Mimic, 2 } };
            var placed = rig.Do(new PlaceComposition(comp, null, counts)).Placement;

            Assert.AreEqual(4, placed.Emitters.Count);
            Assert.AreEqual(2, placed.Mimics.Count);
            Assert.AreEqual(2, rig.World.Count(EnemyKind.Mimic));
            var hydra = placed.Emitters.Where(e => e.Instance == EnemyKind.Hydra).ToList();
            Assert.AreEqual(3, hydra.Count);
            Assert.IsTrue(hydra.All(e => e.InstanceMax == 3 && e.InstanceMin == 1 && Math.Abs(e.Scale - 0.25f) < 1e-6f));
            Assert.IsTrue(hydra.All(e => e.WaitMinSteps == 200 && e.WaitMaxSteps == 500));
            var meatball = placed.Emitters.Single(e => e.Instance == EnemyKind.Meatball);
            Assert.AreEqual(0.70f, meatball.Scale, 1e-4f);
            Assert.That(meatball.AlarmSteps, Is.InRange(1f, 240f), "Create: irandom_range(1, room_speed * 2)");
        }

        [Test]
        public void EmptyCompositionPlacesNothing()
        {
            var rig = new EnemyRig(1515);
            var placed = rig.Do(new PlaceComposition(new EnemyComposition())).Placement;
            Assert.IsEmpty(placed.Emitters);
            Assert.IsEmpty(placed.Mimics);
        }

        [Test]
        public void EverySpawnableKindCanBeCreatedAndCounted()
        {
            var rig = new EnemyRig(1515);
            foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
            {
                rig.Do(new SpawnEnemy(kind, Vec2.Zero));
                Assert.AreEqual(1, rig.World.Count(kind), kind.ToString());
            }
        }

        // ---- A seed reproduces a run ----

        /// <summary>A whole busy room for a while, reduced to one fingerprint of where everything ended up and what happened.</summary>
        static string Fingerprint(int seed)
        {
            var rig = new EnemyRig(seed);
            rig.Ship(new Vec2(1f, 1f), new Vec2(1f, 0f));
            var comp = new EnemyComposition()
                .With(EnemyKind.Hydra, 1, 2).With(EnemyKind.Sawblade, 1, 2).With(EnemyKind.Meatball, 1, 1)
                .With(EnemyKind.Wraith, 1, 1).With(EnemyKind.Dagger, 1, 2).With(EnemyKind.Trilobyte, 1, 1)
                .With(EnemyKind.Worm, 1, 1).With(EnemyKind.Mimic, 1, 1).With(EnemyKind.Phalanx, 1, 1);
            rig.Do(new PlaceComposition(comp));
            rig.Do(new AddWormholeEmitter(30));
            for (int i = 0; i < 6; i++) rig.Do(new SpawnInsect((InsectKind)(i % 3)));
            for (int i = 0; i < 3000; i++) rig.Do(new TickWorld(Step));
            var sb = new System.Text.StringBuilder();
            foreach (var b in rig.World.Enemies) sb.Append(b.Kind).Append(b.Part).Append(b.Position).Append(';');
            foreach (var b in rig.World.Insects) sb.Append(b.Position).Append(';');
            foreach (var v in rig.World.Vortices) sb.Append(v.Mode).Append(v.Position).Append(';');
            sb.Append(rig.Events.Count).Append('/').Append(rig.Events.Select(e => e.GetType().Name).GroupBy(n => n).Count());
            return sb.ToString();
        }

        [Test]
        public void TheSameSeedPlaysTheSameRunAndAnotherSeedDoesNot()
        {
            string first = Fingerprint(2024);
            Assert.AreEqual(first, Fingerprint(2024));
            Assert.AreNotEqual(first, Fingerprint(2025));
        }

        [Test]
        public void ASeededSourceDealsTheSameNumbersAndHonoursGmlRanges()
        {
            var a = new SeededRandom(5);
            var b = new SeededRandom(5);
            for (int i = 0; i < 100; i++)
            {
                int n = a.IRandom(3, 7);
                Assert.AreEqual(n, b.IRandom(3, 7));
                Assert.That(n, Is.InRange(3, 7), "irandom_range is inclusive");
                float r = a.Range(-2f, 2f);
                Assert.AreEqual(r, b.Range(-2f, 2f));
                Assert.That(r, Is.InRange(-2f, 2f));
            }
            Assert.That(new SeededRandom(1).IRandom(9, 4), Is.InRange(4, 9), "GML tolerates a > b");
        }

        // ---- Where the player's and the enemies' hit rules meet ----

        [Test]
        public void ContactAndRoundsHurtTheShipThroughItsOwnRules()
        {
            var rig = new EnemyRig(77);
            var players = new PlayerWorld((min, max) => 0f);
            var id = new PlayerId(1);
            players.AddPlayer(id);
            players.Execute(new ResetHealth(id));
            var hydra = rig.Spawn<HydraEnemy>(EnemyKind.Hydra, Vec2.Zero);

            // A fresh ship is spawn-protected, as in the original.
            Assert.IsFalse(rig.Do(new HurtPlayer(players, id, hydra.Id)).Accepted);
            players.Execute(new TickHealth(id, 10f));
            var hit = rig.Do(new HurtPlayer(players, id, hydra.Id));
            Assert.IsTrue(hit.Accepted);
            Assert.IsTrue(hit.PlayerResult.Value.Has<PlayerDamaged>());
            Assert.AreEqual(hydra, hit.Body);
            Assert.IsFalse(rig.Do(new HurtPlayer(players, id, hydra.Id)).Accepted, "invulnerable after a hit");

            players.Execute(new TickHealth(id, 10f));
            var mine = (EnemyMineBody)rig.Do(new LayMine(null, Vec2.Zero)).Body;
            Assert.IsFalse(rig.Do(new HurtPlayer(players, id, mine.Id)).Accepted, "the ship touches a mine harmlessly");

            Assert.IsTrue(rig.Do(new HurtPlayer(players, id)).Accepted, "an enemy round needs no enemy");
            Assert.AreEqual(players.Get(id).Health.MaxHealth - 2, players.Get(id).Health.Health);

            rig.Beam(hydra, BeamKind.Laser);
            players.Execute(new TickHealth(id, 10f));
            Assert.IsFalse(rig.Do(new HurtPlayer(players, id, hydra.Id)).Accepted, "a dead enemy hurts nobody");
        }

        [Test]
        public void EnemiesChaseTheNearestOfSeveralShips()
        {
            var rig = new EnemyRig(77);
            rig.Ship(new Vec2(50f, 0f), seat: 1);
            rig.Ship(new Vec2(-3f, 0f), new Vec2(0f, 0f), seat: 2);
            var hydra = rig.Spawn<HydraEnemy>(EnemyKind.Hydra, Vec2.Zero);
            rig.Run(hydra, 5);
            Assert.Less(hydra.Position.X, 0f, "toward the nearer ship");
            rig.Do(new ClearTarget(new PlayerId(2)));
            rig.Run(hydra, 5);
            Assert.That(hydra.Direction, Is.InRange(0f, 1f).Or.InRange(359f, 360f), "now toward the far one");
        }
    }
}
