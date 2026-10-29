using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Enemies;
using Unity.Hare.Player;
using Unity.Hare.Weapons;
using static Unity.Hare.Tests.EditMode.EnemyTestKit;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>UH-T14: obj_trilobyte's wandering and the obj_enemy_mine it lays.</summary>
    public class TrilobyteTests
    {
        [SetUp] public void SetUp() => EnemyDirector.Reset(9090);
        [TearDown] public void TearDown() => Cleanup();

        static Trilobyte Make(Vector2 at, float direction = 0f)
        {
            var t = Spawn<Trilobyte>();
            t.Init(at, Gml.Room, null, direction);
            return t;
        }

        static EnemyMine Mine(Vector2 at, PlayerShipController ship, int fuseSeconds = 30)
        {
            var mine = new GameObject("EnemyMine").AddComponent<EnemyMine>();
            mine.Init(at, null, ship, 0f, fuseSeconds);
            return mine;
        }

        static void MineSteps(EnemyMine mine, int steps)
        {
            for (int i = 0; i < steps && mine != null && !mine.IsGone; i++) mine.Tick(Step);
        }

        [Test]
        public void CrawlsAtTwoPixelsAStepAndTurnsItsBodyToTheHeading()
        {
            var t = Make(Vector2.zero, 0f);
            Assert.AreEqual(Trilobyte.SpeedPx, t.SpeedPxPerStep);
            Assert.AreEqual(t.Direction, t.ImageAngle);
            Vector2 start = t.Position;
            Steps(t, 1); // Alarm 0 nudges the heading up to 20 degrees
            Assert.AreEqual(Gml.Px(2f), (t.Position - start).magnitude, 1e-5f);
            float gap = Mathf.Abs(Mathf.DeltaAngle(t.ImageAngle, t.Direction));
            Assume.That(Mathf.Abs(Mathf.DeltaAngle(0f, t.Direction)), Is.GreaterThan(3f));
            Assert.AreEqual(2f, Mathf.Abs(Mathf.DeltaAngle(0f, t.ImageAngle)), 1e-3f, "2 degrees a step");
            Assert.Less(gap, 20f);
        }

        [Test]
        public void LaysAMineAlongItsHeadingWithin620Steps()
        {
            var t = Make(Vector2.zero, 0f);
            EnemyMine laid = null;
            int smokes = 0;
            t.MineLaid += (_, m) => laid = m;
            t.Smoked += _ => smokes++;
            int guard = 0;
            while (laid == null && guard++ < 620) Steps(t, 1);
            Assert.IsNotNull(laid);
            Assert.AreEqual(t.Direction, laid.ImageAngle, 1e-3f);
            Assert.AreSame(t, laid.Owner);
            Assert.AreEqual(guard / 10, smokes, "red smoke every 10 steps");
        }

        [Test]
        public void DiesToShipBulletsAndRocketsOnly()
        {
            var ship = Ship(new Vector2(10f, 0f));
            var t = Make(Vector2.zero);
            EnemyKill? kill = null;
            t.Killed += (_, k) => kill = k;
            Assert.IsFalse(t.OnProjectileHit(EnemyRound(Vector2.zero)));
            t.OnWeaponHit(new WeaponHit(WeaponHitKind.RocketExplosion, Vector2.zero, Vector2.right));
            Assert.IsFalse(t.IsDead);
            ShipRound(ship, Vector2.zero).TryHit(Hitbox(t));
            Assert.AreEqual(Trilobyte.Points, kill.Value.Points);
            Assert.IsTrue(kill.Value.AwardsScore);

            var r = Make(Vector2.zero);
            r.Killed += (_, k) => kill = k;
            FireRocket(Vector2.zero).Touch(Hitbox(r));
            Assert.AreEqual(EnemyDeathCause.Rocket, kill.Value.Cause);
        }

        [Test]
        public void ReversesOnceItsWidthOutsideTheRoom()
        {
            var t = Make(new Vector2(Gml.Room.xMax + Gml.Px(Trilobyte.SizePx) + 0.05f, 0f), 0f);
            Steps(t, 1);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(180f, t.Direction)), Is.LessThanOrEqualTo(20f));
        }

        [Test]
        public void TheMineArmsAfterThreeSecondsThenHomesAndBlowsUpNearTheShip()
        {
            var ship = Ship(new Vector2(Gml.Px(100f), 0f));
            var mine = Mine(Vector2.zero, ship);
            int armed = 0;
            mine.Armed += _ => armed++;
            MineSteps(mine, 359);
            Assert.IsFalse(mine.IsActive);
            Assert.AreEqual(Vector3.zero, mine.transform.position, "still until armed");
            MineSteps(mine, 1);
            Assert.IsTrue(mine.IsActive);
            Assert.AreEqual(1, armed);

            MineSteps(mine, 10);
            // Alarm 0 runs before Step, so the arming step already moves: 11 steps of 1 px.
            Assert.AreEqual(Gml.Px(11f), mine.transform.position.x, 1e-4f, "1 px/step toward the ship");
            // No a_near_b: lined up, it hunts 3 degrees either side of the ship's bearing.
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(0f, mine.ImageAngle)), Is.LessThanOrEqualTo(3f + 1e-3f));

            EnemyKill? kill = null;
            mine.Detonated += (_, k) => kill = k;
            MineSteps(mine, 200);
            Assert.IsTrue(mine == null);
            Assert.AreEqual(EnemyDeathCause.Proximity, kill.Value.Cause);
            Assert.AreEqual(1, Count<RocketExplosion>());
        }

        [Test]
        public void TheMineIgnoresAFarShipAndItsFuseSetsItOff()
        {
            var ship = Ship(new Vector2(10f, 0f));
            var mine = Mine(Vector2.zero, ship, fuseSeconds: 20);
            MineSteps(mine, 2399);
            Assert.AreEqual(Vector3.zero, mine.transform.position);
            EnemyKill? kill = null;
            mine.Detonated += (_, k) => kill = k;
            MineSteps(mine, 1);
            Assert.AreEqual(EnemyDeathCause.Fuse, kill.Value.Cause);
            Assert.AreEqual(1, Count<RocketExplosion>());
        }

        [Test]
        public void ShotsSetTheMineOffButBeamsAndRocketsDoNotSeekIt()
        {
            var ship = Ship(new Vector2(10f, 0f));
            var mine = Mine(Vector2.zero, ship);
            Assert.IsNull(Rocket.FindNearestEnemy(Vector2.zero), "a par_projectile, not a par_enemy");
            EnemyKill? kill = null;
            mine.Detonated += (_, k) => kill = k;
            var bullet = ShipRound(ship, new Vector2(0.05f, 0f));
            Assert.IsTrue(bullet.TryHit(mine.GetComponent<Collider2D>()));
            Assert.IsTrue(mine == null && bullet == null);
            Assert.IsTrue(kill.Value.AwardsScore);
            Assert.AreEqual(EnemyMine.Points, kill.Value.Points);
            Assert.AreEqual(new Vector3(0.05f, 0f, 0f), Object.FindAnyObjectByType<RocketExplosion>().transform.position);

            var second = Mine(Vector2.zero, ship);
            var rocket = FireRocket(Vector2.zero);
            rocket.Touch(second.GetComponent<Collider2D>());
            Assert.IsTrue(second == null && rocket == null);
        }
    }
}
