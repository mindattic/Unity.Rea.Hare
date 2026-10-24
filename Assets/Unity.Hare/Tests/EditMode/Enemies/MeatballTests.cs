using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Enemies;
using Unity.Hare.Player;
using Unity.Hare.Weapons;
using static Unity.Hare.Tests.EditMode.EnemyTestKit;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>UH-T14: obj_meatball's sprays, darkening damage and explosive burst.</summary>
    public class MeatballTests
    {
        [SetUp] public void SetUp() => EnemyDirector.Reset(5050);
        [TearDown] public void TearDown() => Cleanup();

        static Meatball Make(Vector2 at, PlayerShipController ship)
        {
            var meatball = Spawn<Meatball>();
            meatball.Init(at, Gml.Room, ship);
            return meatball;
        }

        [Test]
        public void SpraysFourToSixEnemyRoundsOnItsFirstStep()
        {
            var meatball = Make(Vector2.zero, null);
            int sprayed = 0;
            meatball.Sprayed += (_, n) => sprayed = n;
            Steps(meatball, 1);
            Assert.That(sprayed, Is.InRange(4, 6));
            Assert.AreEqual(sprayed, EnemyRoundCount());
            foreach (var b in Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None))
            {
                Assert.IsTrue(b.GetComponent<PlayerDamageSource>().DestroyOnHit, "enemy rounds hurt the ship and go");
                Assert.AreEqual(Gml.Px(2f) * Gml.StepsPerSecond, b.Speed, 1e-4f, "2 px/step");
            }
        }

        [Test]
        public void ChasesAtOnePixelAStepUntilItsEdgeIsWithin100()
        {
            // The bbox centre is 34 px above the origin and 89 px wide each way.
            var ship = Ship(new Vector2(Gml.Px(89f + 150f), Gml.Px(34f)));
            var meatball = Make(Vector2.zero, ship);
            Steps(meatball, 1);
            Assert.AreEqual(1f, meatball.SpeedPxPerStep);
            var near = Make(new Vector2(Gml.Px(60f), 0f), ship);
            Steps(near, 1);
            Assert.AreEqual(0f, near.SpeedPxPerStep);
        }

        [Test]
        public void ShipBulletsDarkenAndShoveItThenTheOneAfter128BurstsIt()
        {
            var ship = Ship(new Vector2(20f, 0f));
            var meatball = Make(Vector2.zero, ship);
            EnemyKill? kill = null;
            meatball.Killed += (_, k) => kill = k;

            Assert.IsFalse(meatball.OnProjectileHit(EnemyRound(Vector2.zero)), "enemy rounds pass");
            Object.DestroyImmediate(Object.FindAnyObjectByType<Bullet>().gameObject);

            var first = ShipRound(ship, Vector2.zero, 0f);
            float shove = first.Speed / Gml.StepsPerSecond / 2f;
            first.TryHit(Hitbox(meatball));
            Assert.AreEqual(250, meatball.Rgb);
            Assert.AreEqual(shove, meatball.Position.x, 1e-5f, "x += other.hspeed / 2");

            int hits = 1;
            while (meatball != null && hits < 100)
            {
                ShipRound(ship, meatball.Position).TryHit(Hitbox(meatball));
                hits++;
            }
            Assert.AreEqual(27, hits, "255 -> 130 in 25 hits, 125 on the 26th, burst on the 27th");
            Assert.AreEqual(Meatball.Points, kill.Value.Points);
            Assert.IsTrue(kill.Value.AwardsScore && kill.Value.DropsPickup);
            Assert.That(EnemyRoundCount(), Is.InRange(15, 30), "explosive destruction");
            Assert.AreEqual(1, Count<GrenadeExplosion>());
            Assert.That(DebrisCount(DebrisKind.MeatballGib), Is.InRange(8, 12));
        }

        [Test]
        public void RocketsCostSixteenAndAlwaysExplodeItsExplosionsDoNothing()
        {
            var meatball = Make(Vector2.zero, null);
            for (int i = 0; i < 3; i++)
            {
                var rocket = FireRocket(Vector2.zero);
                rocket.Touch(Hitbox(meatball));
                Assert.IsTrue(rocket == null);
            }
            Assert.AreEqual(255 - 3 * 16, meatball.Rgb);
            Assert.AreEqual(3, Count<RocketExplosion>());

            Object.FindAnyObjectByType<RocketExplosion>().Touch(Hitbox(meatball));
            Assert.AreEqual(255 - 3 * 16, meatball.Rgb, "no obj_rocket_explosion event");

            EnemyKill? kill = null;
            meatball.Killed += (_, k) => kill = k;
            int rockets = 3;
            while (meatball != null && rockets < 50) { FireRocket(Vector2.zero).Touch(Hitbox(meatball)); rockets++; }
            Assert.AreEqual(EnemyDeathCause.Rocket, kill.Value.Cause);
            Assert.IsTrue(kill.Value.AwardsScore, "a rocket's burst always scores");
        }

        [Test]
        public void HealsOneShadeEvery120Steps()
        {
            var meatball = Make(Vector2.zero, null);
            FireRocket(Vector2.zero).Touch(Hitbox(meatball));
            Assert.AreEqual(239, meatball.Rgb);
            Steps(meatball, 120);
            Assert.AreEqual(240, meatball.Rgb);
            Steps(meatball, 240);
            Assert.AreEqual(242, meatball.Rgb);
        }
    }
}
