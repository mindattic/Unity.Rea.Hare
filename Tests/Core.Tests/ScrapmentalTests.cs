using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using Unity.Hare.Enemies;
using static Unity.Hare.Tests.Core.EnemyRig;

namespace Unity.Hare.Tests.Core
{
    /// <summary>obj_scrapmental and its obj_shrapnel_piece and obj_shrapnel_harpoon parts (U.HARE-T68), run outside Unity.</summary>
    public class ScrapmentalTests
    {
        /// <summary>One GML step of every body in the world, in creation order.</summary>
        static void StepAll(EnemyRig rig, int steps)
        {
            for (int i = 0; i < steps; i++)
                foreach (var body in rig.World.Enemies.ToList())
                    if (!body.IsDead) rig.Do(new TickEnemy(body.Id, Step));
        }

        static ScrapmentalEnemy Boss(EnemyRig rig, Vec2? at = null) =>
            rig.Spawn<ScrapmentalEnemy>(EnemyKind.Scrapmental, at ?? Vec2.Zero, new EnemyOptions { Spin = 2f });

        [Test]
        public void CreateBuilds36HullPiecesCyclingTheEightHullSpritesAnd16HarpoonsTwentyDegreesApart()
        {
            var rig = new EnemyRig(6801);
            var boss = Boss(rig);
            Assert.IsTrue(boss.Alive);
            Assert.AreEqual(255, boss.Rgb);
            Assert.AreEqual(ScrapmentalEnemy.PieceCount, boss.Pieces.Count);
            for (int i = 0; i < boss.Pieces.Count; i++)
            {
                Assert.AreEqual(i % 8 + 1, boss.Pieces[i].Asset);
                Assert.AreEqual(ShrapnelPieceMode.Attached, boss.Pieces[i].Mode);
                Assert.AreSame(boss, boss.Pieces[i].Parent);
            }
            Assert.AreEqual(ScrapmentalEnemy.HarpoonCount, boss.Harpoons.Count);
            for (int i = 0; i < boss.Harpoons.Count; i++)
            {
                Assert.AreEqual(i * 20f, boss.Harpoons[i].ImageAngle, 1e-4f);
                Assert.AreEqual(ShrapnelHarpoonMode.Rotate, boss.Harpoons[i].Mode);
                Assert.That(boss.Harpoons[i].FireStepsLeft, Is.InRange(120f, 480f), "alarm[0] = irandom_range(room_speed, room_speed * 4)");
            }
            Assert.AreEqual(1 + 36 + 16, rig.World.Enemies.Count);
        }

        [Test]
        public void ItCreepsAtOnePixelAStepWhileFartherThan100()
        {
            var rig = new EnemyRig(6802);
            rig.Ship(Px(400f, 0f));
            var boss = Boss(rig);
            rig.Run(boss, 1);
            Assert.AreEqual(ScrapmentalEnemy.ChaseSpeedPxPerStep, boss.SpeedPxPerStep);

            var near = new EnemyRig(6802);
            near.Ship(Px(60f, 0f));
            var close = Boss(near);
            near.Run(close, 1);
            Assert.AreEqual(0f, close.SpeedPxPerStep);
        }

        [Test]
        public void ItUndulatesFourPixelsEveryEightSteps()
        {
            var rig = new EnemyRig(6803);
            var boss = Boss(rig);
            rig.Run(boss, 8);
            Assert.AreEqual(4f, boss.UndulateY, 1e-4f);
            rig.Run(boss, 8 * 39);
            Assert.AreEqual(160f, boss.UndulateY, 1e-3f, "up to undulateAmount 160");
        }

        [Test]
        public void ShipBulletsDarkenItByOneAndItsOwnSliversPass()
        {
            var rig = new EnemyRig(6804);
            var boss = Boss(rig);
            Assert.IsFalse(rig.EnemyBullet(boss).Accepted, "other.owner != ID");
            Assert.IsTrue(rig.ShipBullet(boss).Accepted);
            Assert.AreEqual(254, boss.Rgb);
            Assert.IsTrue(rig.Rocket(boss).Accepted);
            Assert.AreEqual(248, boss.Rgb, "a rocket takes 6");
        }

        [Test]
        public void TheHitAfterRgb128DefeatsItFor500And24PickupsThenItGoesTenSecondsLater()
        {
            var rig = new EnemyRig(6805);
            var boss = Boss(rig);
            int hits = 0;
            while (boss.Alive && hits < 500) { rig.ShipBullet(boss); hits++; }
            Assert.AreEqual(128, hits, "255 -> 128 in 127 hits, the defeat on the 128th");
            Assert.IsFalse(boss.Alive);
            Assert.AreEqual(500, rig.Seen<ScoreAwarded>().Single().Points);
            Assert.AreEqual(24, rig.Seen<EnemyPickupDropped>().Count());
            Assert.AreEqual(1, rig.Seen<ScrapmentalDefeated>().Count());
            Assert.AreEqual(1, rig.Seen<GrenadeExplosionRaised>().Count());
            Assert.AreEqual(1, rig.Seen<DeathParticlesRaised>().Count());
            Assert.IsTrue(boss.Pieces.All(p => p.Mode == ShrapnelPieceMode.Float), "with(obj_shrapnel_piece) mode = \"float\"");
            Assert.IsTrue(boss.Pieces.All(p => p.SpeedPxPerStep >= 8f && p.SpeedPxPerStep <= 12f), "flying off along their angles (BIBLE D28)");

            Assert.IsFalse(boss.IsParEnemy, "inert once defeated (BIBLE D30)");
            Assert.IsFalse(rig.Rocket(boss).Accepted, "no second defeat by a rocket");
            Assert.AreEqual(1, rig.Seen<ScoreAwarded>().Count());

            rig.Run(boss, (int)ScrapmentalEnemy.DeathDelaySteps - 1);
            Assert.IsFalse(boss.IsDead);
            rig.Run(boss, 2);
            Assert.IsTrue(boss.IsDead, "alarm[4]: instance_destroy, which completes o_defeat");
        }

        [Test]
        public void AnEnemyDefeatScoresNothing()
        {
            var rig = new EnemyRig(6806);
            var boss = Boss(rig);
            for (int i = 0; i < 22; i++) rig.Rocket(boss); // 255 - 22 * 6 = 123
            Assert.IsTrue(boss.Alive);
            rig.Do(new StrikeBullet(boss.Id, new BulletShot(false, false, boss.Position, 0f, 1f, 99)));
            Assert.IsFalse(boss.Alive);
            Assert.AreEqual(0, rig.Seen<ScoreAwarded>().Count(), "only the player's shot scores the 500");
        }

        [Test]
        public void AHullPieceDarkensBy16ThenComesLooseFor20PointsOnceAndFliesOff()
        {
            var rig = new EnemyRig(6807);
            var boss = Boss(rig);
            var piece = boss.Pieces[0];
            Assert.IsTrue(rig.ShipBullet(piece).Accepted);
            Assert.AreEqual(239, piece.Rgb);
            for (int i = 0; i < 7; i++) rig.ShipBullet(piece);
            Assert.AreEqual(127, piece.Rgb);
            Assert.That(piece.LooseStepsLeft, Is.LessThan(0f));
            rig.ShipBullet(piece);
            Assert.That(piece.LooseStepsLeft, Is.InRange(0f, 360f), "alarm[1] = death_delay (0-3 s)");

            StepAll(rig, 362);
            Assert.AreEqual(ShrapnelPieceMode.Float, piece.Mode);
            Assert.That(piece.SpeedPxPerStep, Is.InRange(8f, 12f));
            Assert.AreEqual(20, rig.Seen<ScoreAwarded>().Single().Points);
        }

        [Test]
        public void RocketsExplodeOnAPieceAndTakeFortyEight()
        {
            var rig = new EnemyRig(6808);
            var boss = Boss(rig);
            var piece = boss.Pieces[3];
            Assert.IsTrue(rig.Rocket(piece).Accepted);
            Assert.AreEqual(255 - 48, piece.Rgb);
        }

        [Test]
        public void AHarpoonFiresAlongItsAngleAtEightPixelsAStepAndARockReelsItIn()
        {
            var rig = new EnemyRig(6809);
            var boss = Boss(rig);
            var harpoon = boss.Harpoons[0];
            Assert.IsFalse(rig.Do(new StrikeRock(harpoon.Id)).Accepted, "only a firing harpoon reels in on a rock");
            StepAll(rig, 481);
            Assert.AreEqual(ShrapnelHarpoonMode.Fire, harpoon.Mode);
            Assert.AreEqual(ShrapnelHarpoonBody.FireSpeedPxPerStep, harpoon.SpeedPxPerStep);
            Assert.That(harpoon.LengthPx, Is.GreaterThan(0f));
            Assert.IsFalse(rig.ShipBullet(harpoon).Accepted, "bullets pass through a harpoon");

            Assert.IsTrue(rig.Do(new StrikeRock(harpoon.Id)).Accepted);
            Assert.AreEqual(ShrapnelHarpoonMode.Reel, harpoon.Mode);
        }

        [Test]
        public void HarpoonsBurstOnceTheBossIsDefeated()
        {
            var rig = new EnemyRig(6810);
            var boss = Boss(rig);
            while (boss.Alive) rig.ShipBullet(boss);
            StepAll(rig, 1);
            Assert.IsTrue(boss.Harpoons.All(h => h.IsDead));
        }
    }
}
