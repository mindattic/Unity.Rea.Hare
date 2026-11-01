using System;
using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using Unity.Rea.Hare.Enemies;
using static Unity.Rea.Hare.Tests.Core.EnemyRig;

namespace Unity.Rea.Hare.Tests.Core
{
    /// <summary>The GML helper scripts and the dagger, hydra, sawblade and trilobyte rules, run outside Unity.</summary>
    public class EnemyBasicsTests
    {
        // ---- GML scripts ----

        [Test]
        public void HelpersMatchTheGmlScripts()
        {
            Assert.IsTrue(GmlMath.ANearB(10f, 12f, 2f));
            Assert.IsFalse(GmlMath.ANearB(10f, 12.5f, 2f));
            Assert.IsFalse(GmlMath.ANearB(359f, 0.5f, 2f), "a_near_b does not wrap");
            Assert.IsTrue(GmlMath.CheckRotateClockwise(0f, 90f));
            Assert.IsFalse(GmlMath.CheckRotateClockwise(90f, 0f));
            Assert.IsTrue(GmlMath.CheckRotateClockwise(350f, 10f));
            Assert.IsFalse(GmlMath.CheckRotateClockwise(10f, 350f));
            Assert.AreEqual(90f, GmlMath.PointDirection(Vec2.Zero, new Vec2(0f, 3f)), 1e-3f);
            Assert.AreEqual(0f, GmlMath.PointDirection(new Vec2(1f, 1f), new Vec2(1f, 1f)), "the same point is 0 degrees");
        }

        // ---- Dagger ----

        [Test]
        public void DaggerRotatesHalfADegreePerStepTheShortWayThenLaunches()
        {
            var rig = new EnemyRig(4321);
            var dagger = rig.Spawn<DaggerEnemy>(EnemyKind.Dagger, Vec2.Zero, new EnemyOptions { Direction = 90f, ImageAngle = 0f });
            Assert.AreEqual(DaggerMode.Rotate, dagger.Mode);
            Assert.AreEqual(0f, dagger.SpeedPxPerStep);
            Assert.That(dagger.Variant, Is.InRange(1, 2));

            rig.Run(dagger, 10);
            Assert.AreEqual(5f, dagger.ImageAngle, 1e-3f, "0.5 degrees/step toward 90");
            Assert.AreEqual(Vec2.Zero, dagger.Position, "no motion while rotating");

            rig.Run(dagger, 166);
            Assert.AreEqual(DaggerMode.Rotate, dagger.Mode);
            rig.Run(dagger, 1);
            Assert.AreEqual(DaggerMode.Fly, dagger.Mode);
            Assert.AreEqual(1, rig.Seen<DaggerLaunched>().Count());
            Assert.That(dagger.FlyStepsLeft, Is.InRange(120f, 240f), "alarm[1] = irandom_range(120, 240)");

            var other = rig.Spawn<DaggerEnemy>(EnemyKind.Dagger, new Vec2(5f, 5f), new EnemyOptions { Direction = 350f, ImageAngle = 10f });
            rig.Run(other, 4);
            Assert.AreEqual(8f, other.ImageAngle, 1e-3f, "10 -> 350 turns down through 0");
        }

        [Test]
        public void DaggerFliesAcceleratingToEightThenBrakesAndPicksANewHeading()
        {
            var rig = new EnemyRig(4321);
            var dagger = rig.Spawn<DaggerEnemy>(EnemyKind.Dagger, Vec2.Zero, new EnemyOptions { Direction = 0f, ImageAngle = 0f });
            rig.Run(dagger, 1);
            Assert.AreEqual(DaggerMode.Fly, dagger.Mode, "already lined up");
            rig.Run(dagger, 7);
            Assert.AreEqual(1f, dagger.SpeedPxPerStep);
            rig.Run(dagger, 8 * 7);
            Assert.AreEqual(8f, dagger.SpeedPxPerStep);
            rig.Run(dagger, 16);
            Assert.AreEqual(8f, dagger.SpeedPxPerStep, "capped at 8");
            Assert.Greater(dagger.Position.X, 0f, "flies along direction 0");
            Assert.AreEqual(3, rig.Seen<SmokePuffRaised>().Count(), "a smoke puff every 20 steps of the 79 flown since launch");

            int guard = 0;
            while (dagger.Mode == DaggerMode.Fly && guard++ < 300) rig.Run(dagger, 1);
            Assert.AreEqual(DaggerMode.Stop, dagger.Mode);
            rig.Run(dagger, 8);
            Assert.AreEqual(7f, dagger.SpeedPxPerStep);
            guard = 0;
            while (dagger.Mode == DaggerMode.Stop && guard++ < 100) rig.Run(dagger, 1);
            Assert.AreEqual(DaggerMode.Rotate, dagger.Mode);
            Assert.AreEqual(0f, dagger.SpeedPxPerStep);
            int puffs = rig.Seen<SmokePuffRaised>().Count();
            rig.Run(dagger, 20);
            Assert.AreEqual(puffs, rig.Seen<SmokePuffRaised>().Count(), "Stop clears alarm[2]");
        }

        [Test]
        public void DaggerReversesOnceItIsASpriteWidthOutsideTheRoom()
        {
            var rig = new EnemyRig();
            float size = GameClock.Px(DaggerEnemy.SizePx);
            var dagger = rig.Spawn<DaggerEnemy>(EnemyKind.Dagger, new Vec2(rig.Room.XMax + size + 0.01f, 0f), new EnemyOptions { Direction = 30f, ImageAngle = 200f });
            rig.Run(dagger, 1);
            Assert.AreEqual(210f, dagger.Direction, 1e-3f);
            Assert.AreEqual(210f, dagger.ImageAngle, 1e-3f, "image_angle = direction");
            var inside = rig.Spawn<DaggerEnemy>(EnemyKind.Dagger, new Vec2(rig.Room.XMax + size - 0.01f, 0f), new EnemyOptions { Direction = 30f, ImageAngle = 200f });
            rig.Run(inside, 1);
            Assert.AreEqual(30f, inside.Direction, 1e-3f);
        }

        [Test]
        public void DaggerDiesToEverythingAndOnlyTheShipsBulletsScore()
        {
            var rig = new EnemyRig();
            var a = rig.Spawn<DaggerEnemy>(EnemyKind.Dagger, Vec2.Zero);
            Assert.IsTrue(rig.ShipBullet(a).Accepted, "the bullet goes too");
            var kill = rig.Kill(a);
            Assert.AreEqual(EnemyDeathCause.Bullet, kill.Cause);
            Assert.IsTrue(kill.AwardsScore && kill.DropsPickup);
            Assert.AreEqual(10, kill.Points);

            var b = rig.Spawn<DaggerEnemy>(EnemyKind.Dagger, Vec2.Zero);
            Assert.IsFalse(rig.EnemyBullet(b).Accepted, "enemy rounds pass through");
            Assert.IsFalse(b.IsDead);
            rig.Do(new StrikeBullet(b.Id, new BulletShot(false, false, b.Position, 0f, 0f)));
            Assert.IsFalse(rig.Kill(b).AwardsScore, "only other.owner == player.id scores");
            Assert.IsTrue(rig.Kill(b).DropsPickup);

            var c = rig.Spawn<DaggerEnemy>(EnemyKind.Dagger, Vec2.Zero);
            Assert.IsTrue(rig.Rocket(c).Accepted);
            Assert.AreEqual(EnemyDeathCause.Rocket, rig.Kill(c).Cause);
            Assert.IsTrue(rig.Kill(c).AwardsScore);

            var d = rig.Spawn<DaggerEnemy>(EnemyKind.Dagger, Vec2.Zero);
            rig.Do(new StrikeExplosion(d.Id));
            Assert.AreEqual(EnemyDeathCause.RocketExplosion, rig.Kill(d).Cause);

            var e = rig.Spawn<DaggerEnemy>(EnemyKind.Dagger, Vec2.Zero);
            Assert.IsTrue(rig.Do(new StrikeShield(e.Id)).Accepted);
            Assert.AreEqual(EnemyDeathCause.ShieldWall, rig.Kill(e).Cause);
            Assert.IsTrue(rig.Kill(e).AwardsScore && rig.Kill(e).DropsPickup);

            foreach (var beam in new[] { BeamKind.Laser, BeamKind.Railgun, BeamKind.Flame })
            {
                var x = rig.Spawn<DaggerEnemy>(EnemyKind.Dagger, Vec2.Zero);
                Assert.IsTrue(rig.Beam(x, beam).Accepted);
                Assert.IsFalse(rig.Kill(x).AwardsScore || rig.Kill(x).DropsPickup, beam.ToString());
            }
        }

        [Test]
        public void OverlappingEnemiesPushThemTwoPixelsAStepAway()
        {
            var rig = new EnemyRig();
            var dagger = rig.Spawn<DaggerEnemy>(EnemyKind.Dagger, Vec2.Zero);
            rig.Do(new Separate(dagger.Id, new Vec2(-1f, 0f)));
            Assert.AreEqual(GameClock.Px(2f), dagger.Position.X, 1e-5f);
            rig.Do(new Separate(dagger.Id, dagger.Position + new Vec2(0f, 1f), 3f));
            Assert.AreEqual(-GameClock.Px(6f), dagger.Position.Y, 1e-5f);
        }

        // ---- Hydra ----

        [Test]
        public void HydraTurretsTurnTheLongWayAndFireOnceEachOnTarget()
        {
            var rig = new EnemyRig(1414);
            rig.Ship(new Vec2(0f, 0.5f));
            var hydra = rig.Spawn<HydraEnemy>(EnemyKind.Hydra, Vec2.Zero, new EnemyOptions { Arms = 2, RotationDirection = 1 });
            float before = hydra.TurretAngle(0);
            rig.Run(hydra, 1); // Alarm 0 picks targets, Step turns 2 degrees
            float target = hydra.TurretTarget(0);
            float moved = Maths.DeltaAngle(before, hydra.TurretAngle(0));
            Assert.AreEqual(2f, Math.Abs(moved), 1e-3f);
            Assert.AreEqual(-Math.Sign(Maths.DeltaAngle(before, target)), Math.Sign(moved), "the original negates check_rotate_clockwise");
            Assert.IsTrue(hydra.TurretReady(0));

            rig.Run(hydra, 189);
            var shots = rig.Seen<RoundFired>().ToList();
            Assert.AreEqual(2, shots.Count);
            Assert.IsTrue(shots.All(s => s.Turret >= 0));
            Assert.IsFalse(hydra.TurretReady(0));
        }

        [Test]
        public void HydraClosesAtOnePixelAStepUntilWithin100()
        {
            var rig = new EnemyRig(1414);
            rig.Ship(new Vec2(5f, 0f));
            var hydra = rig.Spawn<HydraEnemy>(EnemyKind.Hydra, Vec2.Zero, new EnemyOptions { Arms = 2, RotationDirection = 1 });
            rig.Run(hydra, 10);
            Assert.AreEqual(GameClock.Px(10f), hydra.Position.X, 1e-4f);
            var near = rig.Spawn<HydraEnemy>(EnemyKind.Hydra, new Vec2(4.9f, 0f), new EnemyOptions { Arms = 2, RotationDirection = 1 });
            rig.Run(near, 1);
            Assert.AreEqual(0f, near.SpeedPxPerStep, "within 100 px of its bounding box it holds");
        }

        [Test]
        public void HydraShipBulletsCostATurretEachAndTheOneAfterTheLastKills()
        {
            var rig = new EnemyRig(1414);
            rig.Ship(new Vec2(20f, 0f));
            var hydra = rig.Spawn<HydraEnemy>(EnemyKind.Hydra, Vec2.Zero, new EnemyOptions { Arms = 2, RotationDirection = 1 });
            Assert.IsFalse(rig.EnemyBullet(hydra).Accepted);
            Assert.AreEqual(2, hydra.Arms);
            for (int i = 0; i < 2; i++) Assert.IsTrue(rig.ShipBullet(hydra).Accepted);
            Assert.AreEqual(0, hydra.Arms);
            Assert.IsFalse(hydra.IsDead);
            rig.ShipBullet(hydra);
            Assert.IsTrue(hydra.IsDead);
            var kill = rig.Kill(hydra);
            Assert.AreEqual(EnemyDeathCause.Bullet, kill.Cause);
            Assert.AreEqual(10, kill.Points);
            Assert.IsTrue(kill.AwardsScore && kill.DropsPickup);
        }

        [Test]
        public void HydraSpinsOneDegreeAStep()
        {
            var rig = new EnemyRig(1414);
            var hydra = rig.Spawn<HydraEnemy>(EnemyKind.Hydra, Vec2.Zero, new EnemyOptions { Arms = 2, RotationDirection = 1 });
            float start = hydra.ImageAngle;
            rig.Run(hydra, 5);
            Assert.AreEqual(5f, Maths.DeltaAngle(start, hydra.ImageAngle), 1e-3f);
        }

        // ---- Sawblade ----

        [Test]
        public void SawbladeDriftsInSlowlyFromAfarSpinningThree()
        {
            var rig = new EnemyRig(7070);
            rig.Ship(new Vec2(10f, 0f));
            var saw = rig.Spawn<SawbladeEnemy>(EnemyKind.Sawblade, Vec2.Zero);
            float start = saw.ImageAngle;
            rig.Run(saw, 10);
            Assert.AreEqual(GameClock.Px(5f), saw.Position.X, 1e-4f, "0.5 px/step");
            Assert.AreEqual(SawbladeEnemy.DriftSpin, saw.Spin);
            Assert.AreEqual(30f, Maths.DeltaAngle(start, saw.ImageAngle), 1e-3f);
        }

        [Test]
        public void SawbladeWithinFourHundredSwarmsAtTwoTowardALeadingJitteredPoint()
        {
            var rig = new EnemyRig(7070);
            rig.Ship(new Vec2(2f, 0f), new Vec2(1f, 0f));
            var saw = rig.Spawn<SawbladeEnemy>(EnemyKind.Sawblade, Vec2.Zero);
            rig.Run(saw, 1);
            Assert.AreEqual(SawbladeEnemy.SwarmSpin, saw.Spin);
            Assert.AreEqual(SawbladeEnemy.SwarmSpeedPxPerStep, saw.SpeedPxPerStep);
            Assert.AreEqual(new Vec2(rig.Room.XMin, rig.Room.YMax), saw.SwarmPoint, "GML (0, 0) until Alarm 0");

            rig.Run(saw, 120);
            Vec2 lead = (saw.SwarmPoint - new Vec2(2f, 0f)) * GameClock.PixelsPerUnit;
            Assert.That(lead.X, Is.InRange(2f - 48f - 1e-3f, 2f + 48f + 1e-3f), "x + hspeed * 2 + jitter");
            Assert.That(lead.Y, Is.InRange(-48f - 1e-3f, 48f + 1e-3f));
        }

        [Test]
        public void SawbladeDiesToEverythingForTenAndAPickup()
        {
            var rig = new EnemyRig(7070);
            rig.Ship(new Vec2(10f, 0f));
            var a = rig.Spawn<SawbladeEnemy>(EnemyKind.Sawblade, Vec2.Zero);
            Assert.IsFalse(rig.EnemyBullet(a).Accepted);
            rig.ShipBullet(a);
            Assert.AreEqual(EnemyDeathCause.Bullet, rig.Kill(a).Cause);
            Assert.IsTrue(rig.Kill(a).AwardsScore);

            var b = rig.Spawn<SawbladeEnemy>(EnemyKind.Sawblade, Vec2.Zero);
            rig.Do(new StrikeShield(b.Id));
            Assert.AreEqual(EnemyDeathCause.ShieldWall, rig.Kill(b).Cause);
            Assert.AreEqual(10, rig.Kill(b).Points);

            var c = rig.Spawn<SawbladeEnemy>(EnemyKind.Sawblade, Vec2.Zero);
            rig.Do(new StrikeExplosion(c.Id));
            Assert.AreEqual(EnemyDeathCause.RocketExplosion, rig.Kill(c).Cause);
            Assert.IsTrue(rig.Kill(c).AwardsScore && rig.Kill(c).DropsPickup);

            var d = rig.Spawn<SawbladeEnemy>(EnemyKind.Sawblade, Vec2.Zero);
            Assert.IsTrue(rig.Rocket(d).Accepted);
            Assert.AreEqual(EnemyDeathCause.Rocket, rig.Kill(d).Cause);
        }

        // ---- Trilobyte ----

        [Test]
        public void TrilobyteCrawlsAtTwoPixelsAStepAndTurnsItsBodyToTheHeading()
        {
            var rig = new EnemyRig(9090);
            var t = rig.Spawn<TrilobyteEnemy>(EnemyKind.Trilobyte, Vec2.Zero, new EnemyOptions { Direction = 0f });
            Assert.AreEqual(TrilobyteEnemy.SpeedPx, t.SpeedPxPerStep);
            Assert.AreEqual(t.Direction, t.ImageAngle);
            Vec2 start = t.Position;
            rig.Run(t, 1); // Alarm 0 nudges the heading up to 20 degrees
            Assert.AreEqual(GameClock.Px(2f), (t.Position - start).Magnitude, 1e-5f);
            Assert.Less(Math.Abs(Maths.DeltaAngle(t.ImageAngle, t.Direction)), 20f);
        }

        [Test]
        public void TrilobyteLaysAMineAlongItsHeadingWithin620StepsSmokingEveryTen()
        {
            var rig = new EnemyRig(9090);
            var t = rig.Spawn<TrilobyteEnemy>(EnemyKind.Trilobyte, Vec2.Zero, new EnemyOptions { Direction = 0f });
            int guard = 0;
            while (!rig.Seen<EnemySpawned>().Any(s => s.Body is EnemyMineBody) && guard++ < 620) rig.Run(t, 1);
            var mine = (EnemyMineBody)rig.Seen<EnemySpawned>().Single(s => s.Body is EnemyMineBody).Body;
            Assert.AreEqual(t.Direction, mine.ImageAngle, 1e-3f);
            Assert.AreSame(t, mine.Owner);
            Assert.AreEqual(BodyPart.Mine, mine.Part);
            Assert.AreEqual(guard / 10, rig.Seen<TrilobyteSmoked>().Count(), "red smoke every 10 steps");
        }

        [Test]
        public void TrilobyteDiesToShipBulletsAndRocketsOnly()
        {
            var rig = new EnemyRig(9090);
            var t = rig.Spawn<TrilobyteEnemy>(EnemyKind.Trilobyte, Vec2.Zero, new EnemyOptions { Direction = 0f });
            Assert.IsFalse(rig.EnemyBullet(t).Accepted);
            rig.Do(new StrikeExplosion(t.Id));
            Assert.IsFalse(rig.Do(new StrikeShield(t.Id)).Accepted, "no shield wall event");
            Assert.IsFalse(t.IsDead);
            rig.ShipBullet(t);
            Assert.AreEqual(10, rig.Kill(t).Points);
            Assert.IsTrue(rig.Kill(t).AwardsScore);

            var r = rig.Spawn<TrilobyteEnemy>(EnemyKind.Trilobyte, Vec2.Zero, new EnemyOptions { Direction = 0f });
            rig.Rocket(r);
            Assert.AreEqual(EnemyDeathCause.Rocket, rig.Kill(r).Cause);
        }

        [Test]
        public void TrilobyteReversesOnceItsWidthOutsideTheRoom()
        {
            var rig = new EnemyRig(9090);
            var t = rig.Spawn<TrilobyteEnemy>(EnemyKind.Trilobyte, new Vec2(rig.Room.XMax + GameClock.Px(TrilobyteEnemy.SizePx) + 0.05f, 0f), new EnemyOptions { Direction = 0f });
            rig.Run(t, 1);
            Assert.That(Math.Abs(Maths.DeltaAngle(180f, t.Direction)), Is.LessThanOrEqualTo(20f));
        }

        // ---- Enemy mine ----

        EnemyMineBody Mine(EnemyRig rig, Vec2 at, int fuseSeconds = 30)
        {
            var result = rig.Do(new LayMine(null, at, 0f, fuseSeconds));
            return (EnemyMineBody)result.Body;
        }

        [Test]
        public void MineArmsAfterThreeSecondsThenHomesAndBlowsUpNearTheShip()
        {
            var rig = new EnemyRig(9090);
            rig.Ship(new Vec2(GameClock.Px(100f), 0f));
            var mine = Mine(rig, Vec2.Zero);
            rig.Run(mine, 359);
            Assert.IsFalse(mine.IsActive);
            Assert.AreEqual(Vec2.Zero, mine.Position, "still until armed");
            rig.Run(mine, 1);
            Assert.IsTrue(mine.IsActive);
            Assert.AreEqual(1, rig.Seen<MineArmed>().Count());

            rig.Run(mine, 10);
            Assert.AreEqual(GameClock.Px(11f), mine.Position.X, 1e-4f, "1 px/step toward the ship");
            Assert.That(Math.Abs(Maths.DeltaAngle(0f, mine.ImageAngle)), Is.LessThanOrEqualTo(3f + 1e-3f));

            rig.Run(mine, 200);
            Assert.IsTrue(mine.IsDead);
            Assert.AreEqual(EnemyDeathCause.Proximity, rig.Kill(mine).Cause);
            Assert.AreEqual(1, rig.Seen<RocketExplosionRaised>().Count());
        }

        [Test]
        public void MineIgnoresAFarShipAndItsFuseSetsItOff()
        {
            var rig = new EnemyRig(9090);
            rig.Ship(new Vec2(10f, 0f));
            var mine = Mine(rig, Vec2.Zero, 20);
            rig.Run(mine, 2399);
            Assert.AreEqual(Vec2.Zero, mine.Position);
            Assert.IsFalse(mine.IsDead);
            rig.Run(mine, 1);
            Assert.AreEqual(EnemyDeathCause.Fuse, rig.Kill(mine).Cause);
            Assert.AreEqual(1, rig.Seen<RocketExplosionRaised>().Count());
        }

        [Test]
        public void MineBlowsOnShotsButBeamsAndExplosionsDoNotTouchIt()
        {
            var rig = new EnemyRig(9090);
            var mine = Mine(rig, Vec2.Zero);
            Assert.IsFalse(mine.IsParEnemy, "a par_projectile, not a par_enemy");
            Assert.IsFalse(rig.Beam(mine, BeamKind.Laser).Accepted);
            Assert.IsFalse(mine.IsDead);
            Assert.IsNull(rig.World.NearestEnemy(Vec2.Zero), "rockets and mines do not seek it");

            rig.ShipBullet(mine, new Vec2(0.05f, 0f));
            Assert.IsTrue(mine.IsDead);
            var kill = rig.Kill(mine);
            Assert.IsTrue(kill.AwardsScore);
            Assert.AreEqual(1, kill.Points);
            Assert.AreEqual(new Vec2(0.05f, 0f), rig.Seen<RocketExplosionRaised>().Single().At);

            var second = Mine(rig, Vec2.Zero);
            Assert.IsTrue(rig.Rocket(second).Accepted);
            Assert.IsTrue(second.IsDead);
        }
    }
}
