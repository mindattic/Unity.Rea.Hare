using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Enemies;
using Unity.Hare.Player;
using Unity.Hare.Weapons;
using static Unity.Hare.Tests.EditMode.EnemyTestKit;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>UH-T14: obj_worm, its segment chain, sliver volleys and tail-first damage.</summary>
    public class WormTests
    {
        [SetUp] public void SetUp() => EnemyDirector.Reset(8080);
        [TearDown] public void TearDown() => Cleanup();

        static Worm Make(Vector2 at, PlayerShipController ship, int segmentCount = 5, float direction = 0f)
        {
            var worm = Spawn<Worm>();
            worm.Init(at, Gml.Room, ship, segmentCount, direction);
            return worm;
        }

        [Test]
        public void BuildsAChainOfOnePlusSegmentCountLinks()
        {
            var worm = Make(Vector2.zero, null, 5);
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
            Assert.IsTrue(worm.Segments.All(s => s.GetComponent<PlayerDamageSource>() != null));

            var random = Spawn<Worm>();
            random.Init(Vector2.zero, Gml.Room);
            Assert.That(random.SegmentCount, Is.InRange(Worm.MinSegmentCount, Worm.MaxSegmentCount));
        }

        [Test]
        public void SegmentsTrailTheLinkAheadAtTheHeadsSpeed()
        {
            var worm = Make(Vector2.zero, null);
            var first = worm.Segments[0];
            first.Tick(Step);
            Assert.AreEqual(0f, first.SpeedPxPerStep, "the head's centre is inside its box: no move");

            Place(worm, new Vector2(2f, 0f));
            first.Tick(Step);
            Assert.AreEqual(worm.SpeedPxPerStep, first.SpeedPxPerStep);
            Assert.AreEqual(0f, first.Direction, 1e-3f);
            Assert.AreEqual(Gml.Px(1f), first.Position.x, 1e-5f);
        }

        [Test]
        public void TheJawTurnsADegreeAStepTowardTheHeading()
        {
            var worm = Make(Vector2.zero, null, direction: 0f);
            Vector2 before = worm.Position;
            Steps(worm, 1); // Alarm 0 jolts direction by up to 90
            float dir = worm.Direction;
            Assume.That(Mathf.Abs(Mathf.DeltaAngle(0f, dir)), Is.GreaterThan(5f));
            Assert.AreEqual(Mathf.Sign(Mathf.DeltaAngle(0f, dir)), Mathf.Sign(Mathf.DeltaAngle(0f, worm.HeadingAngle)));
            Assert.AreEqual(1f, Mathf.Abs(Mathf.DeltaAngle(0f, worm.HeadingAngle)), 1e-3f);
            // headX/headY are set in Step, before the built-in motion moves the body.
            Vector2 expected = before + Gml.Dir(worm.HeadingAngle) * Gml.Px(Worm.JawOffsetPx);
            Assert.AreEqual(expected.x, worm.JawPosition.x, 1e-4f);
            Assert.AreEqual(expected.y, worm.JawPosition.y, 1e-4f);
        }

        [Test]
        public void SpitsAVolleyOfSliversWithinSixHundredSteps()
        {
            var worm = Make(Vector2.zero, null);
            int volley = 0;
            worm.Spat += (_, n) => volley = n;
            int guard = 0;
            while (volley == 0 && guard++ < 600) Steps(worm, 1);
            Assert.That(volley, Is.InRange(12, 24));
            Assert.AreEqual(volley, EnemyRoundCount());
        }

        [Test]
        public void OnlyTheTailTakesDamageAndNineBulletsKillIt()
        {
            var ship = Ship(new Vector2(10f, 0f));
            var worm = Make(Vector2.zero, ship, 5);
            var tail = worm.Segments[5];
            var middle = worm.Segments[2];
            float speed = worm.SpeedPxPerStep;

            var shrugged = ShipRound(ship, Vector2.zero);
            Assert.IsTrue(shrugged.TryHit(Hitbox(middle)));
            Assert.IsTrue(shrugged == null, "spent even on a covered link");
            Assert.AreEqual(Worm.FullRgb, middle.Rgb);
            Assert.IsTrue(worm.OnProjectileHit(EnemyRound(Vector2.zero)), "any owner's bullet is spent");
            Assert.AreEqual(Worm.FullRgb, worm.Rgb);

            EnemyKill? kill = null;
            tail.Killed += (_, k) => kill = k;
            int hits = 0;
            while (tail != null && hits < 50) { ShipRound(ship, Vector2.zero).TryHit(Hitbox(tail)); hits++; }
            Assert.AreEqual(9, hits, "-16 from 255 reaches 127 after 8; the 9th kills");
            Assert.AreEqual(WormSegment.Points, kill.Value.Points);
            Assert.IsTrue(kill.Value.AwardsScore && kill.Value.DropsPickup);
            Assert.AreEqual(speed + (10 - 5) * 0.1f, worm.SpeedPxPerStep, 1e-5f, "head.speed += (10 - segment_count) * 0.1");
            Assert.AreEqual(1, Count<GrenadeExplosion>());
            Assert.That(DebrisCount(DebrisKind.WormGib), Is.InRange(6, 8));
            Assert.IsTrue(worm.Segments[4].IsExposed);
        }

        [Test]
        public void RocketsAlwaysExplodeAndTheExposedHeadDiesForAHundred()
        {
            var worm = Make(Vector2.zero, null, 0);
            var only = worm.Segments[0];
            var rocket = FireRocket(Vector2.zero);
            rocket.Touch(Hitbox(worm));
            Assert.IsTrue(rocket == null, "explodes on a covered head too");
            Assert.AreEqual(Worm.FullRgb, worm.Rgb);

            only.OnWeaponHit(new WeaponHit(WeaponHitKind.Railgun, Vector2.zero, Vector2.right));
            Assert.IsTrue(worm.IsExposed);

            EnemyKill? kill = null;
            worm.Killed += (_, k) => kill = k;
            int rockets = 0;
            while (worm != null && rockets < 20) { FireRocket(Vector2.zero).Touch(Hitbox(worm)); rockets++; }
            Assert.AreEqual(5, rockets, "-42: 213, 171, 129, 87, then dead");
            Assert.AreEqual(Worm.Points, kill.Value.Points);
            Assert.AreEqual(1, DebrisCount(DebrisKind.WormHeadGib));
        }

        [Test]
        public void ReversesOnceItsWidthOutsideTheRoom()
        {
            var worm = Make(new Vector2(Gml.Room.xMax + Gml.Px(Worm.SizePx) + 0.01f, 0f), null, 0, 10f);
            float before = worm.Direction;
            Steps(worm, 1);
            // Alarm 0 adds up to +/-90 first, then Step reverses.
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(before + 180f, worm.Direction)), Is.LessThanOrEqualTo(90f));
        }
    }
}
