using NUnit.Framework;
using UnityEngine;
using Unity.Rea.Hare.Enemies;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Weapons;
using static Unity.Rea.Hare.Tests.EditMode.EnemyTestKit;

namespace Unity.Rea.Hare.Tests.EditMode
{
    /// <summary>UH-T14: obj_sawblade's drift, swarm point and fragility.</summary>
    public class SawbladeTests
    {
        [SetUp] public void SetUp() => EnemyDirector.Reset(7070);
        [TearDown] public void TearDown() => Cleanup();

        static Sawblade Make(Vector2 at, PlayerShipController ship)
        {
            var saw = Spawn<Sawblade>();
            saw.Init(at, Gml.Room, ship);
            return saw;
        }

        [Test]
        public void DriftsInSlowlyFromAfarSpinningThree()
        {
            var ship = Ship(new Vector2(10f, 0f));
            var saw = Make(Vector2.zero, ship);
            float start = saw.ImageAngle;
            Steps(saw, 10);
            Assert.AreEqual(Gml.Px(5f), saw.Position.x, 1e-4f, "0.5 px/step");
            Assert.AreEqual(SawbladeEnemy.DriftSpin, saw.Spin);
            Assert.AreEqual(30f, Mathf.DeltaAngle(start, saw.ImageAngle), 1e-3f);
        }

        [Test]
        public void WithinFourHundredItSwarmsAtTwoTowardALeadingJitteredPoint()
        {
            var ship = Ship(new Vector2(2f, 0f));
            ship.GetComponent<Rigidbody2D>().linearVelocity = new Vector2(1.2f, 0f); // 1 px/step
            var saw = Make(Vector2.zero, ship);
            Steps(saw, 1);
            Assert.AreEqual(SawbladeEnemy.SwarmSpin, saw.Spin);
            Assert.AreEqual(SawbladeEnemy.SwarmSpeedPxPerStep, saw.SpeedPxPerStep);
            Assert.AreEqual(new Vector2(Gml.Room.xMin, Gml.Room.yMax), saw.SwarmPoint, "GML (0, 0) until Alarm 0");

            Steps(saw, 120); // Alarm 0 first fires within 30-120 steps
            Vector2 lead = (saw.SwarmPoint - (Vector2)ship.transform.position) * Gml.PixelsPerUnit;
            Assert.That(lead.x, Is.InRange(2f - 48f - 1e-3f, 2f + 48f + 1e-3f), "x + hspeed * 2 + jitter");
            Assert.That(lead.y, Is.InRange(-48f - 1e-3f, 48f + 1e-3f));
        }

        [Test]
        public void DiesToEverythingForTenAndAPickup()
        {
            var ship = Ship(new Vector2(10f, 0f));
            EnemyKill? kill = null;

            var a = Make(Vector2.zero, ship);
            a.Killed += (_, k) => kill = k;
            Assert.IsFalse(a.OnProjectileHit(EnemyRound(Vector2.zero)));
            ShipRound(ship, Vector2.zero).TryHit(Hitbox(a));
            Assert.AreEqual(EnemyDeathCause.Bullet, kill.Value.Cause);
            Assert.IsTrue(kill.Value.AwardsScore);

            var b = Make(Vector2.zero, ship);
            b.Killed += (_, k) => kill = k;
            b.OnShieldHit(null, Vector2.right);
            Assert.AreEqual(EnemyDeathCause.ShieldWall, kill.Value.Cause);
            Assert.AreEqual(Sawblade.Points, kill.Value.Points);

            var c = Make(Vector2.zero, ship);
            c.Killed += (_, k) => kill = k;
            c.OnWeaponHit(new WeaponHit(WeaponHitKind.RocketExplosion, Vector2.zero, Vector2.right));
            Assert.AreEqual(EnemyDeathCause.RocketExplosion, kill.Value.Cause);
            Assert.IsTrue(kill.Value.AwardsScore && kill.Value.DropsPickup);

            var d = Make(Vector2.zero, ship);
            d.Killed += (_, k) => kill = k;
            FireRocket(Vector2.zero).Touch(Hitbox(d));
            Assert.AreEqual(EnemyDeathCause.Rocket, kill.Value.Cause);
            Assert.IsTrue(d == null);
        }
    }
}
