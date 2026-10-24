using System;
using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using Unity.Hare.Enemies;
using static Unity.Hare.Tests.Core.EnemyRig;

namespace Unity.Hare.Tests.Core
{
    /// <summary>The meatball, phalanx, wraith, mimic and worm rules, run outside Unity.</summary>
    public class EnemySetPieceTests
    {
        // ---- Meatball ----

        [Test]
        public void MeatballSpraysFourToSixEnemyRoundsOnItsFirstStep()
        {
            var rig = new EnemyRig(5050);
            var meatball = rig.Spawn<MeatballEnemy>(EnemyKind.Meatball, Vec2.Zero);
            rig.Run(meatball, 1);
            int sprayed = rig.Seen<MeatballSprayed>().Single().Bullets;
            Assert.That(sprayed, Is.InRange(4, 6));
            Assert.AreEqual(sprayed, rig.Seen<RoundFired>().Count());
            Assert.IsTrue(rig.Seen<RoundFired>().All(r => r.SpeedPxPerStep == 2f && r.Turret == -1));
        }

        [Test]
        public void MeatballChasesAtOnePixelAStepUntilItsEdgeIsWithin100()
        {
            var rig = new EnemyRig(5050);
            rig.Ship(Px(89f + 150f, 34f));
            var meatball = rig.Spawn<MeatballEnemy>(EnemyKind.Meatball, Vec2.Zero);
            rig.Run(meatball, 1);
            Assert.AreEqual(1f, meatball.SpeedPxPerStep);
            var near = rig.Spawn<MeatballEnemy>(EnemyKind.Meatball, Px(60f, 0f));
            rig.Run(near, 1);
            Assert.AreEqual(0f, near.SpeedPxPerStep);
        }

        [Test]
        public void MeatballShipBulletsDarkenAndShoveItThenTheOneAfter128BurstsIt()
        {
            var rig = new EnemyRig(5050);
            var meatball = rig.Spawn<MeatballEnemy>(EnemyKind.Meatball, Vec2.Zero);
            Assert.IsFalse(rig.EnemyBullet(meatball).Accepted, "enemy rounds pass");

            float speed = GameClock.Px(2f) * GameClock.StepsPerSecond;
            rig.ShipBullet(meatball, Vec2.Zero, 0f, speed);
            Assert.AreEqual(250, meatball.Rgb);
            Assert.AreEqual(GameClock.Px(1f), meatball.Position.X, 1e-5f, "x += other.hspeed / 2");

            int hits = 1;
            while (!meatball.IsDead && hits < 100)
            {
                rig.ShipBullet(meatball);
                hits++;
            }
            Assert.AreEqual(27, hits, "255 -> 130 in 25 hits, 125 on the 26th, burst on the 27th");
            var kill = rig.Kill(meatball);
            Assert.AreEqual(50, kill.Points);
            Assert.IsTrue(kill.AwardsScore && kill.DropsPickup);
            Assert.That(rig.Seen<RoundFired>().Count(), Is.InRange(15, 30), "explosive destruction");
            Assert.AreEqual(1, rig.Seen<GrenadeExplosionRaised>().Count());
            Assert.That(rig.Seen<DebrisRaised>().Count(d => d.Spec.Kind == DebrisKind.MeatballGib), Is.InRange(8, 12));
            Assert.AreEqual(2, rig.Seen<DeathBurstRaised>().Single().Size, "the large burst");
        }

        [Test]
        public void MeatballRocketsCostSixteenAndAlwaysExplodeItsExplosionsDoNothing()
        {
            var rig = new EnemyRig(5050);
            var meatball = rig.Spawn<MeatballEnemy>(EnemyKind.Meatball, Vec2.Zero);
            for (int i = 0; i < 3; i++) Assert.IsTrue(rig.Rocket(meatball).Accepted);
            Assert.AreEqual(255 - 3 * 16, meatball.Rgb);
            rig.Do(new StrikeExplosion(meatball.Id));
            Assert.AreEqual(255 - 3 * 16, meatball.Rgb, "no obj_rocket_explosion event");

            int rockets = 3;
            while (!meatball.IsDead && rockets < 50) { rig.Rocket(meatball); rockets++; }
            Assert.AreEqual(EnemyDeathCause.Rocket, rig.Kill(meatball).Cause);
            Assert.IsTrue(rig.Kill(meatball).AwardsScore, "a rocket's burst always scores");
        }

        [Test]
        public void MeatballHealsOneShadeEvery120Steps()
        {
            var rig = new EnemyRig(5050);
            var meatball = rig.Spawn<MeatballEnemy>(EnemyKind.Meatball, Vec2.Zero);
            rig.Rocket(meatball);
            Assert.AreEqual(239, meatball.Rgb);
            rig.Run(meatball, 120);
            Assert.AreEqual(240, meatball.Rgb);
            rig.Run(meatball, 240);
            Assert.AreEqual(242, meatball.Rgb);
        }

        // ---- Phalanx ----

        [Test]
        public void PhalanxCreatesAShieldThatRidesOnItAndTurnsTowardTheShip()
        {
            var rig = new EnemyRig(2020);
            rig.Ship(new Vec2(0f, 0.6f)); // straight up: 90 degrees
            var phalanx = rig.Spawn<PhalanxEnemy>(EnemyKind.Phalanx, Vec2.Zero);
            var shield = phalanx.Shield;
            Assert.IsNotNull(shield);
            Assert.AreSame(phalanx, shield.Parent);
            Assert.IsTrue(rig.Seen<EnemySpawned>().Any(s => s.Body == shield), "the shield is announced so a view can be made");

            for (int i = 0; i < 10; i++) { rig.Run(phalanx, 1); rig.Run(shield, 1); }
            Assert.AreEqual(2f, shield.ImageAngle, 1e-3f, "0.2 degrees a step toward 90");
            Assert.AreEqual(phalanx.Position, shield.Position);
            Assert.That(shield.ImageAlpha, Is.InRange(0.3f - 1e-5f, 0.6f + 1e-5f));
        }

        [Test]
        public void PhalanxCreepsAtHalfAPixelAStepAndSpinsBothLayers()
        {
            var rig = new EnemyRig(2020);
            rig.Ship(new Vec2(10f, 0f));
            var phalanx = rig.Spawn<PhalanxEnemy>(EnemyKind.Phalanx, Vec2.Zero);
            float back = phalanx.BackAngle;
            rig.Run(phalanx, 10);
            Assert.AreEqual(GameClock.Px(5f), phalanx.Position.X, 1e-4f);
            Assert.AreEqual(20f, phalanx.ImageAngle, 1e-3f);
            Assert.AreEqual(-20f, Maths.DeltaAngle(back, phalanx.BackAngle), 1e-3f);
        }

        [Test]
        public void PhalanxShieldBouncesBulletsAndRocketsOncePerShot()
        {
            var rig = new EnemyRig(2020);
            rig.Ship(new Vec2(10f, 0f));
            var phalanx = rig.Spawn<PhalanxEnemy>(EnemyKind.Phalanx, Vec2.Zero);
            var shield = phalanx.Shield;

            var first = rig.ShipBullet(shield, Px(40f, 0f), 180f, key: 5);
            Assert.IsFalse(first.Accepted, "not spent");
            Assert.AreEqual(0f, first.Deflection.Value, 1e-3f, "turned out along the normal");
            var again = rig.ShipBullet(shield, Px(40f, 0f), 180f, key: 5);
            Assert.IsNull(again.Deflection, "last_impact: once per bullet");

            var enemyRound = rig.EnemyBullet(shield, Px(0f, 40f), 270f, key: 6);
            Assert.AreEqual(90f, enemyRound.Deflection.Value, 1e-3f, "enemy rounds bounce too");

            var rocket = rig.Rocket(shield, Px(40f, 0f));
            Assert.IsFalse(rocket.Accepted, "no explosion");
            Assert.AreEqual(0f, rocket.Deflection.Value, 1e-3f);
            Assert.IsFalse(phalanx.IsDead || shield.IsDead);
        }

        [Test]
        public void PhalanxBodyDiesToShipBulletsAndRocketsForTwenty()
        {
            var rig = new EnemyRig(2020);
            rig.Ship(new Vec2(10f, 0f));
            var phalanx = rig.Spawn<PhalanxEnemy>(EnemyKind.Phalanx, Vec2.Zero);
            Assert.IsFalse(rig.EnemyBullet(phalanx).Accepted);
            rig.Do(new StrikeExplosion(phalanx.Id));
            Assert.IsFalse(phalanx.IsDead, "its rocket-explosion event is commented out");
            Assert.IsFalse(rig.Do(new StrikeShield(phalanx.Id)).Accepted, "its shield-wall event is empty");

            rig.ShipBullet(phalanx);
            Assert.AreEqual(20, rig.Kill(phalanx).Points);
            Assert.IsTrue(rig.Kill(phalanx).AwardsScore && rig.Kill(phalanx).DropsPickup);
            Assert.IsFalse(phalanx.PushesAway, "its push-apart is commented out");

            var second = rig.Spawn<PhalanxEnemy>(EnemyKind.Phalanx, Vec2.Zero);
            rig.Rocket(second);
            Assert.AreEqual(EnemyDeathCause.Rocket, rig.Kill(second).Cause);
            Assert.AreEqual(1, rig.Seen<DeathBurstRaised>().Count(b => b.Size == 1 && b.SourceId == second.Id), "the medium burst");
        }

        [Test]
        public void PhalanxBeamsStripTheShieldAndTheShieldGoesWithItsPhalanx()
        {
            var rig = new EnemyRig(2020);
            var phalanx = rig.Spawn<PhalanxEnemy>(EnemyKind.Phalanx, Vec2.Zero);
            rig.Beam(phalanx.Shield, BeamKind.Laser);
            Assert.IsTrue(phalanx.Shield.IsDead);
            Assert.IsFalse(phalanx.IsDead);

            var other = rig.Spawn<PhalanxEnemy>(EnemyKind.Phalanx, new Vec2(1f, 1f));
            var otherShield = other.Shield;
            rig.Beam(other, BeamKind.Flame);
            rig.Run(otherShield, 1);
            Assert.IsTrue(otherShield.IsDead);
            Assert.AreEqual(EnemyDeathCause.Orphaned, rig.Kill(otherShield).Cause);
        }

        // ---- Wraith ----

        [Test]
        public void WraithFloatsTowardTheShipThenChargesPastItAndReappears()
        {
            var rig = new EnemyRig(6060);
            rig.Ship(Vec2.Zero);
            var wraith = rig.Spawn<WraithEnemy>(EnemyKind.Wraith, new Vec2(-6f, 0f)); // 600 px away
            rig.Run(wraith, 10);
            Assert.AreEqual(WraithState.Float, wraith.State);
            Assert.AreEqual(-6f + GameClock.Px(20f), wraith.Position.X, 1e-4f, "2 px/step");
            Assert.AreEqual(1, wraith.Facing);

            int guard = 0;
            while (wraith.State == WraithState.Float && guard++ < 300) rig.Run(wraith, 1);
            Assert.AreEqual(WraithState.Charge, wraith.State);
            Assert.That(wraith.DistanceToPlayerPx(), Is.LessThanOrEqualTo(WraithEnemy.ChargeRangePx));

            rig.Run(wraith, 1); // first pass: aim 600 px beyond the ship
            Assert.AreEqual(GameClock.Px(WraithEnemy.ChargeOvershootPx), wraith.ChargeTarget.X, 1e-3f);
            Assert.AreEqual(0f, wraith.ChargeTarget.Y, 1e-3f);
            Assert.AreEqual(2f + WraithEnemy.ChargeAccelerationPxPerStep, wraith.SpeedPxPerStep, 1e-4f);

            guard = 0;
            while (wraith.State == WraithState.Charge && guard++ < 1000) rig.Run(wraith, 1);
            Assert.AreEqual(1, rig.Seen<WraithReappeared>().Count());
            Assert.AreEqual(WraithState.Float, wraith.State);
            Assert.That(wraith.Position.Magnitude, Is.InRange(GameClock.Px(350f) - 1e-4f, GameClock.Px(400f) + 1e-4f));
        }

        [Test]
        public void WraithShipBulletsStunItAndOnlyAStunnedWraithDiesToRockets()
        {
            var rig = new EnemyRig(6060);
            rig.Ship(new Vec2(10f, 0f));
            var wraith = rig.Spawn<WraithEnemy>(EnemyKind.Wraith, Vec2.Zero);
            Assert.IsFalse(rig.Rocket(wraith).Accepted, "rockets fly through it unless it is stunned");
            Assert.IsFalse(rig.EnemyBullet(wraith).Accepted, "only other.owner == player.id");

            Assert.IsTrue(rig.ShipBullet(wraith).Accepted);
            Assert.AreEqual(WraithState.Stunned, wraith.State);
            Assert.AreEqual(1f, wraith.ImageAlpha);
            Assert.That(wraith.StunStepsLeft, Is.InRange(WraithEnemy.MinStunSteps, WraithEnemy.MaxStunSteps));
            rig.Run(wraith, 1);
            Assert.AreEqual(0f, wraith.SpeedPxPerStep, "frozen");
            Assert.AreEqual(1f, wraith.ImageAlpha, "no breathing while stunned");

            Assert.IsTrue(rig.Rocket(wraith).Accepted);
            var kill = rig.Kill(wraith);
            Assert.AreEqual(EnemyDeathCause.Rocket, kill.Cause);
            Assert.AreEqual(50, kill.Points);
            Assert.IsTrue(kill.DropsPickup);
        }

        [Test]
        public void WraithStunWearsOffAndRocketExplosionsDoNothing()
        {
            var rig = new EnemyRig(6060);
            rig.Ship(new Vec2(10f, 0f));
            var wraith = rig.Spawn<WraithEnemy>(EnemyKind.Wraith, Vec2.Zero);
            rig.ShipBullet(wraith);
            rig.Do(new StrikeExplosion(wraith.Id));
            Assert.IsFalse(wraith.IsDead);
            rig.Run(wraith, (int)Math.Ceiling(wraith.StunStepsLeft));
            Assert.AreEqual(WraithState.Float, wraith.State);
            Assert.That(wraith.ImageAlpha, Is.InRange(0.3f - 0.003f, 0.6f + 0.003f));
        }

        [Test]
        public void WraithLeavesAnAfterimageEveryTenStepsAndBreathesBetweenPointThreeAndPointSix()
        {
            var rig = new EnemyRig(6060);
            var wraith = rig.Spawn<WraithEnemy>(EnemyKind.Wraith, Vec2.Zero);
            rig.Run(wraith, 30);
            var blurs = rig.Seen<DebrisRaised>().Where(d => d.Spec.Kind == DebrisKind.WraithBlur).ToList();
            Assert.AreEqual(3, blurs.Count);
            Assert.AreEqual(WraithEnemy.BlurFadePerStep, blurs[0].Spec.FadePerStep);
            Assert.AreEqual(WraithEnemy.BlurAlpha, blurs[0].Spec.Alpha);

            float min = 1f, max = 0f;
            for (int i = 0; i < 400; i++)
            {
                rig.Run(wraith, 1);
                min = Math.Min(min, wraith.ImageAlpha);
                max = Math.Max(max, wraith.ImageAlpha);
            }
            Assert.AreEqual(0.3f, min, 0.003f);
            Assert.AreEqual(0.6f, max, 0.003f);
        }

        // ---- Mimic ----

        EnemyOptions Still => new EnemyOptions { SpeedPxPerStep = 0f, Spin = 0f };

        [Test]
        public void MimicStartsAsARockAndCreatesItsIllusion()
        {
            var rig = new EnemyRig(1010);
            var mimic = rig.Spawn<MimicEnemy>(EnemyKind.Mimic, Vec2.Zero, Still);
            Assert.AreEqual(0f, mimic.ImageAlpha);
            Assert.AreEqual(1f, mimic.IllusionAlpha);
            Assert.IsTrue(mimic.IsDisguised);
            Assert.That(mimic.IllusionAsset, Is.InRange(1, MimicEnemy.IllusionAssets));
            Assert.AreSame(mimic, mimic.Illusion.Parent);
            Assert.IsTrue(mimic.Illusion.IsParEnemy, "the fake rock is a par_enemy");

            var stopped = rig.Spawn<MimicEnemy>(EnemyKind.Mimic, Vec2.Zero, new EnemyOptions { SpeedPxPerStep = 0.02f, Spin = 0.02f });
            Assert.AreEqual(0f, stopped.SpeedPxPerStep, "within 0.03 of 0 rounds to 0");
            Assert.AreEqual(0f, stopped.Spin);

            var random = rig.Spawn<MimicEnemy>(EnemyKind.Mimic, Vec2.Zero);
            Assert.That(random.SpeedPxPerStep, Is.InRange(-1.5f, 1.5f));
            Assert.AreEqual(0f, random.Direction, "direction is never set: GML's default 0");
        }

        [Test]
        public void MimicFadesTheDisguiseWithin275PxAndBackOutside()
        {
            var rig = new EnemyRig(1010);
            rig.Ship(Px(53f + 200f, 0f));
            var mimic = rig.Spawn<MimicEnemy>(EnemyKind.Mimic, Vec2.Zero, Still);
            rig.Run(mimic, 10);
            Assert.AreEqual(0.8f, mimic.IllusionAlpha, 1e-4f);
            Assert.AreEqual(0.2f, mimic.ImageAlpha, 1e-4f);
            int guard = 0;
            while (mimic.IllusionAlpha > 0f && guard++ < 100) rig.Run(mimic, 1);
            rig.Run(mimic, 1);
            Assert.AreEqual(0f, mimic.IllusionAlpha);
            Assert.AreEqual(1f, mimic.ImageAlpha);

            rig.Ship(new Vec2(10f, 0f));
            guard = 0;
            while (!mimic.IsDisguised && guard++ < 100) rig.Run(mimic, 1);
            Assert.IsTrue(mimic.IsDisguised);
            Assert.AreEqual(0f, mimic.ImageAlpha, 1e-4f);
        }

        [Test]
        public void MimicWhileDisguisedTheIllusionSoaksUpShotsAndTheMimicIsUntouchable()
        {
            var rig = new EnemyRig(1010);
            rig.Ship(new Vec2(10f, 0f));
            var mimic = rig.Spawn<MimicEnemy>(EnemyKind.Mimic, Vec2.Zero, Still);
            var illusion = mimic.Illusion;

            Assert.IsFalse(rig.ShipBullet(mimic).Accepted);
            Assert.IsTrue(rig.ShipBullet(illusion).Accepted, "the fake rock stops it");
            Assert.IsTrue(rig.EnemyBullet(illusion).Accepted, "any owner's");
            Assert.AreEqual(2, rig.Seen<TinyExplosionRaised>().Count());

            Assert.IsFalse(rig.Rocket(mimic).Accepted, "the mimic lets it through");
            Assert.IsTrue(rig.Rocket(illusion).Accepted);
            Assert.IsFalse(mimic.IsDead || illusion.IsDead);
        }

        [Test]
        public void MimicRevealedDiesToShotsAndABulletKillAlsoBlowsUp()
        {
            var rig = new EnemyRig(1010);
            rig.Ship(Px(150f, 0f));
            var mimic = rig.Spawn<MimicEnemy>(EnemyKind.Mimic, Vec2.Zero, Still);
            var illusion = mimic.Illusion;
            rig.Run(mimic, 1);
            Assert.Less(mimic.IllusionAlpha, 1f);

            Assert.IsFalse(rig.ShipBullet(illusion).Accepted, "the illusion only blocks at full alpha");
            Assert.IsFalse(rig.EnemyBullet(mimic).Accepted, "enemy rounds pass");

            rig.ShipBullet(mimic, new Vec2(0.1f, 0f));
            Assert.IsTrue(mimic.IsDead);
            var kill = rig.Kill(mimic);
            Assert.AreEqual(10, kill.Points);
            Assert.IsTrue(kill.AwardsScore && kill.DropsPickup);
            Assert.AreEqual(new Vec2(0.1f, 0f), rig.Seen<RocketExplosionRaised>().Single().At);

            rig.Run(illusion, 1);
            Assert.IsTrue(illusion.IsDead, "goes with its mimic");

            var second = rig.Spawn<MimicEnemy>(EnemyKind.Mimic, Vec2.Zero, Still);
            rig.Run(second, 1);
            rig.Rocket(second);
            Assert.AreEqual(EnemyDeathCause.Rocket, rig.Kill(second).Cause);
        }

        [Test]
        public void MimicDriftsAlongDirectionZeroAndReversesTwoHundredPixelsOutside()
        {
            var rig = new EnemyRig(1010);
            var mimic = rig.Spawn<MimicEnemy>(EnemyKind.Mimic, Vec2.Zero, new EnemyOptions { SpeedPxPerStep = 1f, Spin = 0f });
            rig.Run(mimic, 10);
            Assert.AreEqual(GameClock.Px(10f), mimic.Position.X, 1e-4f);
            var backwards = rig.Spawn<MimicEnemy>(EnemyKind.Mimic, Vec2.Zero, new EnemyOptions { SpeedPxPerStep = -1f, Spin = 0f });
            rig.Run(backwards, 10);
            Assert.AreEqual(-GameClock.Px(10f), backwards.Position.X, 1e-4f, "negative speed runs backwards");
            var outside = rig.Spawn<MimicEnemy>(EnemyKind.Mimic, new Vec2(rig.Room.XMax + GameClock.Px(MimicEnemy.SizePx) + 0.01f, 0f), new EnemyOptions { SpeedPxPerStep = 1f, Spin = 0f });
            rig.Run(outside, 1);
            Assert.AreEqual(180f, outside.Direction, 1e-3f);
        }

        // ---- Worm ----

        [Test]
        public void WormBuildsAChainOfOnePlusSegmentCountLinks()
        {
            var rig = new EnemyRig(8080);
            var worm = rig.Spawn<WormEnemy>(EnemyKind.Worm, Vec2.Zero, new EnemyOptions { SegmentCount = 5, Direction = 0f });
            Assert.AreEqual(6, worm.Segments.Count);
            Assert.AreSame(worm.Segments[0], worm.Child);
            Assert.AreSame(worm, worm.Segments[0].Parent);
            for (int i = 1; i < 6; i++)
            {
                Assert.AreSame(worm.Segments[i - 1], worm.Segments[i].Parent);
                Assert.AreSame(worm.Segments[i], worm.Segments[i - 1].Child);
                Assert.AreSame(worm, worm.Segments[i].Head);
            }
            Assert.IsNull(worm.Segments[5].Child);
            Assert.IsTrue(worm.Segments[5].IsExposed);
            Assert.IsFalse(worm.IsExposed);
            Assert.AreEqual(1, rig.World.Count(EnemyKind.Worm), "a worm counts its head only");
            Assert.AreEqual(7, rig.World.Enemies.Count, "the head and six links live in the world");

            var random = rig.Spawn<WormEnemy>(EnemyKind.Worm, Vec2.Zero);
            Assert.That(random.SegmentCount, Is.InRange(WormEnemy.MinSegmentCount, WormEnemy.MaxSegmentCount));
        }

        [Test]
        public void WormSegmentsTrailTheLinkAheadAtTheHeadsSpeed()
        {
            var rig = new EnemyRig(8080);
            var worm = rig.Spawn<WormEnemy>(EnemyKind.Worm, Vec2.Zero, new EnemyOptions { SegmentCount = 5, Direction = 0f });
            var first = worm.Segments[0];
            rig.Run(first, 1);
            Assert.AreEqual(0f, first.SpeedPxPerStep, "the head's centre is inside its box: no move");
            rig.Do(new PlaceBody(worm.Id, new Vec2(2f, 0f)));
            rig.Run(first, 1);
            Assert.AreEqual(worm.SpeedPxPerStep, first.SpeedPxPerStep);
            Assert.AreEqual(0f, first.Direction, 1e-3f);
            Assert.AreEqual(GameClock.Px(1f), first.Position.X, 1e-5f);
        }

        [Test]
        public void WormJawTurnsADegreeAStepTowardTheHeading()
        {
            var rig = new EnemyRig(8080);
            var worm = rig.Spawn<WormEnemy>(EnemyKind.Worm, Vec2.Zero, new EnemyOptions { SegmentCount = 5, Direction = 0f });
            Vec2 before = worm.Position;
            rig.Run(worm, 1); // Alarm 0 jolts direction by up to 90
            float dir = worm.Direction;
            Assume.That(Math.Abs(Maths.DeltaAngle(0f, dir)), Is.GreaterThan(5f));
            Assert.AreEqual(Math.Sign(Maths.DeltaAngle(0f, dir)), Math.Sign(Maths.DeltaAngle(0f, worm.HeadingAngle)));
            Assert.AreEqual(1f, Math.Abs(Maths.DeltaAngle(0f, worm.HeadingAngle)), 1e-3f);
            Vec2 expected = before + GmlMath.Dir(worm.HeadingAngle) * GameClock.Px(WormEnemy.JawOffsetPx);
            Assert.AreEqual(expected.X, worm.JawPosition.X, 1e-4f);
            Assert.AreEqual(expected.Y, worm.JawPosition.Y, 1e-4f);
        }

        [Test]
        public void WormSpitsAVolleyOfSliversWithinSixHundredSteps()
        {
            var rig = new EnemyRig(8080);
            var worm = rig.Spawn<WormEnemy>(EnemyKind.Worm, Vec2.Zero, new EnemyOptions { SegmentCount = 5, Direction = 0f });
            int guard = 0;
            while (!rig.Seen<WormSpat>().Any() && guard++ < 600) rig.Run(worm, 1);
            int volley = rig.Seen<WormSpat>().Single().Slivers;
            Assert.That(volley, Is.InRange(12, 24));
            Assert.AreEqual(volley, rig.Seen<RoundFired>().Count());
        }

        [Test]
        public void WormOnlyTheTailTakesDamageAndNineBulletsKillIt()
        {
            var rig = new EnemyRig(8080);
            rig.Ship(new Vec2(10f, 0f));
            var worm = rig.Spawn<WormEnemy>(EnemyKind.Worm, Vec2.Zero, new EnemyOptions { SegmentCount = 5, Direction = 0f });
            var tail = worm.Segments[5];
            var middle = worm.Segments[2];
            float speed = worm.SpeedPxPerStep;

            Assert.IsTrue(rig.ShipBullet(middle).Accepted, "spent even on a covered link");
            Assert.AreEqual(WormEnemy.FullRgb, middle.Rgb);
            Assert.IsTrue(rig.EnemyBullet(worm).Accepted, "any owner's bullet is spent");
            Assert.AreEqual(WormEnemy.FullRgb, worm.Rgb);

            int hits = 0;
            while (!tail.IsDead && hits < 50) { rig.ShipBullet(tail); hits++; }
            Assert.AreEqual(9, hits, "-16 from 255 reaches 127 after 8; the 9th kills");
            var kill = rig.Kill(tail);
            Assert.AreEqual(10, kill.Points);
            Assert.IsTrue(kill.AwardsScore && kill.DropsPickup);
            Assert.AreEqual(speed + (10 - 5) * 0.1f, worm.SpeedPxPerStep, 1e-5f, "head.speed += (10 - segment_count) * 0.1");
            Assert.AreEqual(1, rig.Seen<GrenadeExplosionRaised>().Count());
            Assert.That(rig.Seen<DebrisRaised>().Count(d => d.Spec.Kind == DebrisKind.WormGib), Is.InRange(6, 8));
            Assert.IsTrue(worm.Segments[4].IsExposed);
        }

        [Test]
        public void WormRocketsAlwaysExplodeAndTheExposedHeadDiesForAHundred()
        {
            var rig = new EnemyRig(8080);
            var worm = rig.Spawn<WormEnemy>(EnemyKind.Worm, Vec2.Zero, new EnemyOptions { SegmentCount = 0, Direction = 0f });
            var only = worm.Segments[0];
            Assert.IsTrue(rig.Rocket(worm).Accepted, "explodes on a covered head too");
            Assert.AreEqual(WormEnemy.FullRgb, worm.Rgb);

            rig.Beam(only, BeamKind.Railgun);
            Assert.IsTrue(worm.IsExposed);

            int rockets = 0;
            while (!worm.IsDead && rockets < 20) { rig.Rocket(worm); rockets++; }
            Assert.AreEqual(5, rockets, "-42: 213, 171, 129, 87, then dead");
            Assert.AreEqual(100, rig.Kill(worm).Points);
            Assert.AreEqual(1, rig.Seen<DebrisRaised>().Count(d => d.Spec.Kind == DebrisKind.WormHeadGib));
        }

        [Test]
        public void WormReversesOnceItsWidthOutsideTheRoom()
        {
            var rig = new EnemyRig(8080);
            var worm = rig.Spawn<WormEnemy>(EnemyKind.Worm, new Vec2(rig.Room.XMax + GameClock.Px(WormEnemy.SizePx) + 0.01f, 0f), new EnemyOptions { SegmentCount = 0, Direction = 10f });
            float before = worm.Direction;
            rig.Run(worm, 1);
            Assert.That(Math.Abs(Maths.DeltaAngle(before + 180f, worm.Direction)), Is.LessThanOrEqualTo(90f));
        }
    }
}
