using NUnit.Framework;
using UnityEngine;
using Unity.Rea.Hare.Enemies;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Weapons;
using static Unity.Rea.Hare.Tests.EditMode.EnemyTestKit;

namespace Unity.Rea.Hare.Tests.EditMode
{
    /// <summary>UH-T14: obj_phalanx and the obj_phalanx_shield that turns to face the ship.</summary>
    public class PhalanxTests
    {
        [SetUp] public void SetUp() => EnemyDirector.Reset(2020);
        [TearDown] public void TearDown() => Cleanup();

        static Phalanx Make(Vector2 at, PlayerShipController ship)
        {
            var phalanx = Spawn<Phalanx>();
            phalanx.Init(at, Gml.Room, ship);
            return phalanx;
        }

        [Test]
        public void CreatesAShieldThatRidesOnItAndTurnsTowardTheShip()
        {
            var ship = Ship(new Vector2(0f, 0.6f)); // straight up: 90 degrees
            var phalanx = Make(Vector2.zero, ship);
            var shield = phalanx.Shield;
            Assert.IsNotNull(shield);
            Assert.AreSame(phalanx, shield.Parent);
            Assert.IsNotNull(shield.GetComponent<PlayerDamageSource>(), "a par_enemy: touching it hurts");

            for (int i = 0; i < 10; i++) { phalanx.Tick(Step); shield.Tick(Step); }
            Assert.AreEqual(2f, shield.ImageAngle, 1e-3f, "0.2 degrees a step toward 90");
            Assert.AreEqual((Vector2)phalanx.transform.position, shield.Position);
            Assert.That(shield.ImageAlpha, Is.InRange(0.3f - 1e-5f, 0.6f + 1e-5f));
        }

        [Test]
        public void CreepsAtHalfAPixelAStepAndSpinsBothLayers()
        {
            var ship = Ship(new Vector2(10f, 0f));
            var phalanx = Make(Vector2.zero, ship);
            float back = phalanx.BackAngle;
            Steps(phalanx, 10);
            Assert.AreEqual(Gml.Px(5f), phalanx.Position.x, 1e-4f);
            Assert.AreEqual(20f, phalanx.ImageAngle, 1e-3f);
            Assert.AreEqual(-20f, Mathf.DeltaAngle(back, phalanx.BackAngle), 1e-3f);
        }

        [Test]
        public void BulletsAndRocketsBounceOffTheShieldOncePerShot()
        {
            var ship = Ship(new Vector2(10f, 0f));
            var phalanx = Make(Vector2.zero, ship);
            var shield = phalanx.Shield;

            var bullet = ShipRound(ship, new Vector2(Gml.Px(40f), 0f), 180f);
            Assert.IsTrue(bullet.TryHit(Hitbox(shield)));
            Assert.IsTrue(bullet != null, "not spent");
            Assert.AreEqual(0f, bullet.Direction, 1e-3f, "turned out along the normal");
            bullet.SetDirection(180f);
            bullet.TryHit(Hitbox(shield));
            Assert.AreEqual(180f, bullet.Direction, 1e-3f, "last_impact: once per bullet");

            var enemyRound = EnemyRound(new Vector2(0f, Gml.Px(40f)), 270f);
            enemyRound.TryHit(Hitbox(shield));
            Assert.AreEqual(90f, enemyRound.Direction, 1e-3f, "enemy rounds bounce too");

            var rocket = FireRocket(new Vector2(Gml.Px(40f), 0f), 180f);
            rocket.Touch(Hitbox(shield));
            Assert.IsTrue(rocket != null, "no explosion");
            Assert.AreEqual(0, Count<RocketExplosion>());
            Assert.AreEqual(0f, rocket.Direction, 1e-3f);
            Assert.IsFalse(phalanx.IsDead || shield.IsDead);
        }

        [Test]
        public void TheBodyDiesToShipBulletsAndRocketsForTwenty()
        {
            var ship = Ship(new Vector2(10f, 0f));
            var phalanx = Make(Vector2.zero, ship);
            EnemyKill? kill = null;
            phalanx.Killed += (_, k) => kill = k;

            Assert.IsFalse(phalanx.OnProjectileHit(EnemyRound(Vector2.zero)));
            phalanx.OnWeaponHit(new WeaponHit(WeaponHitKind.RocketExplosion, Vector2.zero, Vector2.right));
            Assert.IsFalse(phalanx.IsDead, "its rocket-explosion event is commented out");

            ShipRound(ship, Vector2.zero).TryHit(Hitbox(phalanx));
            Assert.IsTrue(phalanx == null);
            Assert.AreEqual(Phalanx.Points, kill.Value.Points);
            Assert.IsTrue(kill.Value.AwardsScore && kill.Value.DropsPickup);

            var second = Make(Vector2.zero, ship);
            second.Killed += (_, k) => kill = k;
            FireRocket(Vector2.zero).Touch(Hitbox(second));
            Assert.AreEqual(EnemyDeathCause.Rocket, kill.Value.Cause);
            Assert.AreEqual(1, Count<RocketExplosion>());
        }

        [Test]
        public void BeamsStripTheShieldAndTheShieldGoesWithItsPhalanx()
        {
            var phalanx = Make(Vector2.zero, null);
            var shield = phalanx.Shield;
            shield.OnWeaponHit(new WeaponHit(WeaponHitKind.Laser, Vector2.zero, Vector2.right));
            Assert.IsTrue(shield == null);
            Assert.IsFalse(phalanx.IsDead);

            var other = Make(Vector2.one, null);
            var otherShield = other.Shield;
            EnemyKill? kill = null;
            otherShield.Killed += (_, k) => kill = k;
            other.OnWeaponHit(new WeaponHit(WeaponHitKind.Flame, Vector2.zero, Vector2.right));
            otherShield.Tick(Step);
            Assert.IsTrue(otherShield == null);
            Assert.AreEqual(EnemyDeathCause.Orphaned, kill.Value.Cause);
        }
    }
}
