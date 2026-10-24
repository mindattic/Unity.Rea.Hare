using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Enemies;
using Unity.Hare.Player;
using Unity.Hare.Weapons;
using static Unity.Hare.Tests.EditMode.EnemyTestKit;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>UH-T14: obj_hydra's spinning turrets, chase and turret-stripping hits.</summary>
    public class HydraTests
    {
        [SetUp] public void SetUp() => EnemyDirector.Reset(1414);
        [TearDown] public void TearDown() => Cleanup();

        static Hydra Make(Vector2 at, PlayerShipController ship, int arms = 2)
        {
            var hydra = Spawn<Hydra>();
            hydra.Init(at, Gml.Room, ship, arms, 1);
            return hydra;
        }

        [Test]
        public void IsAHurtfulParEnemyWithTwoToFiveTurrets()
        {
            var hydra = Spawn<Hydra>();
            hydra.Init(Vector2.zero, Gml.Room);
            Assert.That(hydra.Arms, Is.InRange(Hydra.MinArms, Hydra.MaxArms));
            Assert.That(hydra.RotationDirection, Is.EqualTo(1).Or.EqualTo(-1));
            Assert.IsNotNull(hydra.GetComponent<PlayerDamageSource>());
            Assert.IsTrue(Hitbox(hydra).isTrigger);
            Assert.AreSame(hydra, Rocket.FindNearestEnemy(Vector2.zero));
        }

        [Test]
        public void TurretsTurnTheLongWayAndFireOnceEachOnTarget()
        {
            var ship = Ship(new Vector2(0f, 0.5f));
            var hydra = Make(Vector2.zero, ship);
            float before = hydra.TurretAngle(0);
            int shots = 0;
            hydra.Fired += (_, b) =>
            {
                shots++;
                Assert.IsTrue(b.FromEnemy, "owner = ID");
                Assert.AreEqual(Gml.Px(Hydra.MuzzlePx), ((Vector2)b.transform.position - hydra.Position).magnitude, 1e-4f);
            };

            Steps(hydra, 1); // Alarm 0 picks targets, Step turns 2 degrees
            float target = hydra.TurretTarget(0);
            float moved = Mathf.DeltaAngle(before, hydra.TurretAngle(0));
            Assume.That(Mathf.Abs(Mathf.DeltaAngle(before, target)), Is.GreaterThan(4f));
            Assert.AreEqual(2f, Mathf.Abs(moved), 1e-3f);
            Assert.AreEqual(-Mathf.Sign(Mathf.DeltaAngle(before, target)), Mathf.Sign(moved), "the original negates check_rotate_clockwise");
            Assert.IsTrue(hydra.TurretReady(0));

            // The long way round is at most 180 steps; the next retarget is 200+ steps after step 1.
            Steps(hydra, 189);
            Assert.AreEqual(2, shots);
            Assert.IsFalse(hydra.TurretReady(0));
            Assert.AreEqual(2, EnemyRoundCount());
        }

        [Test]
        public void ClosesAtOnePixelAStepUntilWithin100()
        {
            var ship = Ship(new Vector2(5f, 0f));
            var hydra = Make(Vector2.zero, ship);
            Steps(hydra, 10);
            Assert.AreEqual(Gml.Px(10f), hydra.Position.x, 1e-4f);
            Assert.AreEqual(1f, hydra.SpeedPxPerStep);

            var near = Make(new Vector2(4.9f, 0f), ship);
            Steps(near, 1);
            Assert.AreEqual(0f, near.SpeedPxPerStep, "within 100 px of its bounding box it holds");
        }

        [Test]
        public void EachShipBulletCostsATurretAndTheOneAfterTheLastKills()
        {
            var ship = Ship(new Vector2(20f, 0f));
            var hydra = Make(Vector2.zero, ship, arms: 2);
            EnemyKill? kill = null;
            hydra.Killed += (_, k) => kill = k;

            var stray = EnemyRound(Vector2.zero);
            Assert.IsFalse(hydra.OnProjectileHit(stray), "other.owner == ID passes through");
            Assert.AreEqual(2, hydra.Arms);

            for (int i = 0; i < 2; i++)
            {
                var b = ShipRound(ship, Vector2.zero);
                Assert.IsTrue(b.TryHit(Hitbox(hydra)));
                Assert.IsTrue(b == null, "the bullet is spent");
            }
            Assert.AreEqual(0, hydra.Arms);
            Assert.IsFalse(hydra.IsDead);

            ShipRound(ship, Vector2.zero).TryHit(Hitbox(hydra));
            Assert.IsTrue(hydra == null);
            Assert.AreEqual(EnemyDeathCause.Bullet, kill.Value.Cause);
            Assert.AreEqual(Hydra.Points, kill.Value.Points);
            Assert.IsTrue(kill.Value.AwardsScore && kill.Value.DropsPickup);
        }

        [Test]
        public void RocketsAndTheirExplosionsKillItBeamsGiveNothing()
        {
            var hydra = Make(Vector2.zero, null);
            EnemyKill? kill = null;
            hydra.Killed += (_, k) => kill = k;
            FireRocket(Vector2.zero).Touch(Hitbox(hydra));
            Assert.IsTrue(hydra == null);
            Assert.AreEqual(EnemyDeathCause.Rocket, kill.Value.Cause);
            Assert.AreEqual(1, Count<RocketExplosion>());

            var second = Make(Vector2.zero, null);
            second.Killed += (_, k) => kill = k;
            Object.FindAnyObjectByType<RocketExplosion>().Touch(Hitbox(second));
            Assert.AreEqual(EnemyDeathCause.RocketExplosion, kill.Value.Cause);
            Assert.IsTrue(kill.Value.AwardsScore);

            var third = Make(Vector2.zero, null);
            third.Killed += (_, k) => kill = k;
            third.OnWeaponHit(new WeaponHit(WeaponHitKind.Laser, Vector2.zero, Vector2.right));
            Assert.IsTrue(third == null);
            Assert.IsFalse(kill.Value.AwardsScore || kill.Value.DropsPickup);
        }

        [Test]
        public void SpinsOneDegreeAStepAndPushesAwayFromEnemies()
        {
            var hydra = Make(Vector2.zero, null);
            float start = hydra.ImageAngle;
            Steps(hydra, 5);
            Assert.AreEqual(5f, Mathf.DeltaAngle(start, hydra.ImageAngle), 1e-3f);
            hydra.PushAwayFrom(new Vector2(-1f, 0f));
            Assert.AreEqual(Gml.Px(2f), hydra.Position.x, 1e-5f);
        }
    }
}
