using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Enemies;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>UH-T13: obj_dagger's rotate / fly / stop flight and its collisions.</summary>
    public class DaggerTests
    {
        const float Step = 1f / 120f;
        static readonly Rect Room = Gml.Room;

        [SetUp]
        public void SetUp() => Random.InitState(4321);

        [TearDown]
        public void TearDown()
        {
            DestroyAll<Dagger>();
            DestroyAll<Bullet>();
            DestroyAll<Rocket>();
            DestroyAll<RocketExplosion>();
            DestroyAll<PlayerShipController>();
        }

        static void DestroyAll<T>() where T : Component
        {
            foreach (var c in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
                if (c != null) Object.DestroyImmediate(c.gameObject);
        }

        static Dagger Spawn(Vector2 position, float direction, float imageAngle)
        {
            var dagger = new GameObject("Dagger").AddComponent<Dagger>();
            dagger.Init(position, Room, direction, imageAngle);
            return dagger;
        }

        static void Steps(Dagger dagger, int steps)
        {
            for (int i = 0; i < steps && !dagger.IsDead; i++) dagger.Tick(Step);
        }

        [Test]
        public void SpawnsAtRestRotatingWithAHurtfulHitbox()
        {
            var dagger = Spawn(Vector2.zero, 90f, 0f);
            Assert.AreEqual(DaggerMode.Rotate, dagger.Mode);
            Assert.AreEqual(0f, dagger.SpeedPxPerStep);
            Assert.That(dagger.Variant, Is.InRange(1, 2));
            Assert.IsNotNull(dagger.GetComponent<PlayerDamageSource>(), "obj_player's par_enemy collision");
            Assert.IsTrue(dagger.GetComponent<BoxCollider2D>().isTrigger);
            Assert.AreSame(dagger, Rocket.FindNearestEnemy(Vector2.zero), "a par_enemy rockets and mines home on");
        }

        [Test]
        public void RotatesHalfADegreePerStepTheShortWayThenLaunches()
        {
            var dagger = Spawn(Vector2.zero, 90f, 0f);
            int launches = 0;
            dagger.Launched += _ => launches++;

            Steps(dagger, 10);
            Assert.AreEqual(5f, dagger.ImageAngle, 1e-3f, "0.5 degrees/step toward 90");
            Assert.AreEqual(Vector3.zero, dagger.transform.position, "no motion while rotating");

            // 88 is within a_near_b's 2 degrees: 176 steps of turning, then the launch step.
            Steps(dagger, 166);
            Assert.AreEqual(DaggerMode.Rotate, dagger.Mode);
            Steps(dagger, 1);
            Assert.AreEqual(DaggerMode.Fly, dagger.Mode);
            Assert.AreEqual(1, launches);
            Assert.That(dagger.FlyStepsLeft, Is.InRange(120f, 240f), "alarm[1] = irandom_range(120, 240)");

            var other = Spawn(Vector2.one * 5f, 350f, 10f);
            Steps(other, 4);
            Assert.AreEqual(8f, other.ImageAngle, 1e-3f, "10 -> 350 turns down through 0");
            var wrap = Spawn(Vector2.one * -5f, 10f, 359f);
            Steps(wrap, 4);
            Assert.AreEqual(1f, wrap.ImageAngle, 1e-3f, "359 -> 10 turns up through 360");
        }

        [Test]
        public void FliesAcceleratingToEightThenBrakesAndPicksANewHeading()
        {
            var dagger = Spawn(Vector2.zero, 0f, 0f);
            var puffs = new List<Vector2>();
            dagger.SmokePuff += (_, p) => puffs.Add(p);

            Steps(dagger, 1);
            Assert.AreEqual(DaggerMode.Fly, dagger.Mode, "already lined up");

            // Alarm 0 started at Create (8 steps), so it gains 1 px/step every 8 steps from then.
            Steps(dagger, 7);
            Assert.AreEqual(1f, dagger.SpeedPxPerStep);
            Steps(dagger, 8 * 7);
            Assert.AreEqual(8f, dagger.SpeedPxPerStep);
            Steps(dagger, 16);
            Assert.AreEqual(8f, dagger.SpeedPxPerStep, "capped at 8");
            Assert.Greater(dagger.transform.position.x, 0f, "flies along direction 0");
            Assert.AreEqual(0f, dagger.transform.position.y, 1e-4f);
            Assert.AreEqual(3, puffs.Count, "a smoke puff every 20 steps of the 79 flown since launch");

            // Alarm 1 ends the flight within 240 steps; it then sheds 1 px/step every 8 steps.
            int guard = 0;
            while (dagger.Mode == DaggerMode.Fly && guard++ < 300) Steps(dagger, 1);
            Assert.AreEqual(DaggerMode.Stop, dagger.Mode);
            Steps(dagger, 8);
            Assert.AreEqual(7f, dagger.SpeedPxPerStep);

            guard = 0;
            while (dagger.Mode == DaggerMode.Stop && guard++ < 100) Steps(dagger, 1);
            Assert.AreEqual(DaggerMode.Rotate, dagger.Mode);
            Assert.AreEqual(0f, dagger.SpeedPxPerStep);
            int puffsWhenStopped = puffs.Count;
            // Even if the new heading lines up at once, a fresh alarm[2] needs 20 steps.
            Steps(dagger, 20);
            Assert.AreEqual(puffsWhenStopped, puffs.Count, "Stop clears alarm[2]");
        }

        [Test]
        public void ReversesOnceItIsASpriteWidthOutsideTheRoom()
        {
            float size = Gml.Px(Dagger.SizePx);
            var dagger = Spawn(new Vector2(Room.xMax + size + 0.01f, 0f), 30f, 200f);
            Steps(dagger, 1);
            Assert.AreEqual(210f, dagger.Direction, 1e-3f);
            Assert.AreEqual(210f, dagger.ImageAngle, 1e-3f, "image_angle = direction");

            var inside = Spawn(new Vector2(Room.xMax + size - 0.01f, 0f), 30f, 200f);
            Steps(inside, 1);
            Assert.AreEqual(30f, inside.Direction, 1e-3f);
        }

        [Test]
        public void HelpersMatchTheGmlScripts()
        {
            Assert.IsTrue(Dagger.ANearB(10f, 12f, 2f));
            Assert.IsFalse(Dagger.ANearB(10f, 12.5f, 2f));
            Assert.IsFalse(Dagger.ANearB(359f, 0.5f, 2f), "a_near_b does not wrap");
            Assert.IsTrue(Dagger.CheckRotateClockwise(0f, 90f));
            Assert.IsFalse(Dagger.CheckRotateClockwise(90f, 0f));
            Assert.IsTrue(Dagger.CheckRotateClockwise(350f, 10f));
            Assert.IsFalse(Dagger.CheckRotateClockwise(10f, 350f));
        }

        [Test]
        public void BulletsKillItAndArePointsOnlyFromTheShip()
        {
            var ship = new GameObject("Player").AddComponent<PlayerShipController>();
            var dagger = Spawn(Vector2.zero, 0f, 0f);
            DaggerKill? kill = null;
            dagger.Killed += (_, k) => kill = k;

            var bullet = new GameObject("Bullet").AddComponent<Bullet>();
            bullet.Init(ship.gameObject, 0f, 1f, 1, 1f, 0.05f);
            Assert.IsTrue(bullet.TryHit(dagger.GetComponent<Collider2D>()));
            Assert.IsTrue(bullet == null, "the bullet goes too");
            Assert.IsTrue(dagger == null);
            Assert.AreEqual(DaggerDeathCause.Bullet, kill.Value.Cause);
            Assert.IsTrue(kill.Value.AwardsScore);
            Assert.IsTrue(kill.Value.DropsPickup);

            var second = Spawn(Vector2.zero, 0f, 0f);
            DaggerKill? stray = null;
            second.Killed += (_, k) => stray = k;
            Assert.IsTrue(second.OnProjectileHit(null));
            Assert.IsFalse(stray.Value.AwardsScore, "only other.owner == player.id scores");
            Assert.IsTrue(stray.Value.DropsPickup);
        }

        [Test]
        public void RocketsExplodeOnItAndBeamsOrFlameKillItWithoutReward()
        {
            var dagger = Spawn(Vector2.zero, 0f, 0f);
            DaggerKill? kill = null;
            dagger.Killed += (_, k) => kill = k;
            var rocket = new GameObject("Rocket").AddComponent<Rocket>();
            rocket.Init(Vector2.zero, 0f);
            rocket.Touch(dagger.GetComponent<Collider2D>());
            Assert.IsTrue(dagger == null);
            Assert.AreEqual(DaggerDeathCause.Rocket, kill.Value.Cause);
            Assert.IsTrue(kill.Value.AwardsScore);
            Assert.AreEqual(1, Object.FindObjectsByType<RocketExplosion>(FindObjectsSortMode.None).Length);

            var blasted = Spawn(Vector2.zero, 0f, 0f);
            DaggerKill? blast = null;
            blasted.Killed += (_, k) => blast = k;
            var explosion = Object.FindObjectsByType<RocketExplosion>(FindObjectsSortMode.None)[0];
            explosion.Touch(blasted.GetComponent<Collider2D>());
            Assert.AreEqual(DaggerDeathCause.RocketExplosion, blast.Value.Cause);
            Assert.IsTrue(blast.Value.AwardsScore && blast.Value.DropsPickup);

            foreach (var kind in new[] { WeaponHitKind.Laser, WeaponHitKind.Railgun, WeaponHitKind.Flame })
            {
                var d = Spawn(Vector2.zero, 0f, 0f);
                DaggerKill? k = null;
                d.Killed += (_, x) => k = x;
                d.OnWeaponHit(new WeaponHit(kind, Vector2.zero, Vector2.right));
                Assert.IsTrue(d == null, kind.ToString());
                Assert.IsFalse(k.Value.AwardsScore, kind.ToString());
                Assert.IsFalse(k.Value.DropsPickup, kind.ToString());
            }
        }

        [Test]
        public void ShieldWallKillsItForPoints()
        {
            var dagger = Spawn(Vector2.zero, 0f, 0f);
            DaggerKill? kill = null;
            dagger.Killed += (_, k) => kill = k;
            dagger.OnShieldHit(null, Vector2.right);
            Assert.IsTrue(dagger == null);
            Assert.AreEqual(DaggerDeathCause.ShieldWall, kill.Value.Cause);
            Assert.IsTrue(kill.Value.AwardsScore && kill.Value.DropsPickup);
        }

        [Test]
        public void OverlappingEnemiesPushItTwoPixelsAStepAway()
        {
            var dagger = Spawn(Vector2.zero, 0f, 0f);
            dagger.PushAwayFrom(new Vector2(-1f, 0f));
            Assert.AreEqual(Gml.Px(2f), dagger.transform.position.x, 1e-5f);
            dagger.PushAwayFrom(dagger.transform.position + Vector3.up, 3f);
            Assert.AreEqual(-Gml.Px(6f), dagger.transform.position.y, 1e-5f);
        }
    }
}
