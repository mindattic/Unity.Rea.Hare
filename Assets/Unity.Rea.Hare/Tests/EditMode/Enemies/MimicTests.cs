using NUnit.Framework;
using UnityEngine;
using Unity.Rea.Hare.Enemies;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Weapons;
using static Unity.Rea.Hare.Tests.EditMode.EnemyTestKit;

namespace Unity.Rea.Hare.Tests.EditMode
{
    /// <summary>UH-T14: obj_mimic's rock disguise (obj_mimic_illusion) and its reveal.</summary>
    public class MimicTests
    {
        [SetUp] public void SetUp() => EnemyDirector.Reset(1010);
        [TearDown] public void TearDown() => Cleanup();

        static Mimic Make(Vector2 at, PlayerShipController ship, float speed = 0f, float spin = 0f)
        {
            var mimic = Spawn<Mimic>();
            mimic.Init(at, Gml.Room, ship, speed, spin);
            return mimic;
        }

        static void Reveal(Mimic mimic)
        {
            int guard = 0;
            while (mimic.IllusionAlpha > 0f && guard++ < 100) Steps(mimic, 1);
            Steps(mimic, 1);
        }

        [Test]
        public void StartsAsARockAndCreatesItsIllusion()
        {
            var mimic = Make(Vector2.zero, null);
            Assert.AreEqual(0f, mimic.ImageAlpha);
            Assert.AreEqual(1f, mimic.IllusionAlpha);
            Assert.IsTrue(mimic.IsDisguised);
            Assert.That(mimic.IllusionAsset, Is.InRange(1, Mimic.IllusionAssets));
            Assert.AreSame(mimic, mimic.Illusion.Parent);
            Assert.IsNotNull(mimic.Illusion.GetComponent<PlayerDamageSource>(), "the fake rock is a par_enemy");

            var stopped = Make(Vector2.zero, null, 0.02f, 0.02f);
            Assert.AreEqual(0f, stopped.SpeedPxPerStep, "within 0.03 of 0 rounds to 0");
            Assert.AreEqual(0f, stopped.Spin);

            var random = Spawn<Mimic>();
            random.Init(Vector2.zero, Gml.Room);
            Assert.That(random.SpeedPxPerStep, Is.InRange(-1.5f, 1.5f));
            Assert.AreEqual(0f, random.Direction, "direction is never set: GML's default 0");
        }

        [Test]
        public void FadesTheDisguiseWithin275PxAndBackOutside()
        {
            var ship = Ship(new Vector2(Gml.Px(53f + 200f), 0f));
            var mimic = Make(Vector2.zero, ship);
            Steps(mimic, 10);
            Assert.AreEqual(0.8f, mimic.IllusionAlpha, 1e-4f);
            Assert.AreEqual(0.2f, mimic.ImageAlpha, 1e-4f);
            Reveal(mimic);
            Assert.AreEqual(0f, mimic.IllusionAlpha);
            Assert.AreEqual(1f, mimic.ImageAlpha);

            ship.transform.position = new Vector3(10f, 0f, 0f);
            int guard = 0;
            while (!mimic.IsDisguised && guard++ < 100) Steps(mimic, 1);
            Assert.IsTrue(mimic.IsDisguised);
            Assert.AreEqual(0f, mimic.ImageAlpha, 1e-4f);
        }

        [Test]
        public void WhileDisguisedTheIllusionSoaksUpShotsAndTheMimicIsUntouchable()
        {
            var ship = Ship(new Vector2(10f, 0f));
            var mimic = Make(Vector2.zero, ship);
            var illusion = mimic.Illusion;

            var bullet = ShipRound(ship, Vector2.zero);
            Assert.IsFalse(mimic.OnProjectileHit(bullet));
            Assert.IsTrue(bullet.TryHit(Hitbox(illusion)));
            Assert.IsTrue(bullet == null, "the fake rock stops it");
            Assert.IsTrue(illusion.OnProjectileHit(EnemyRound(Vector2.zero)), "any owner's");

            var rocket = FireRocket(Vector2.zero);
            rocket.Touch(Hitbox(mimic));
            Assert.IsTrue(rocket != null, "the mimic lets it through");
            rocket.Touch(Hitbox(illusion));
            Assert.IsTrue(rocket == null);
            Assert.IsFalse(mimic.IsDead || illusion.IsDead);
        }

        [Test]
        public void RevealedItDiesToShotsAndABulletKillAlsoBlowsUp()
        {
            var ship = Ship(new Vector2(Gml.Px(150f), 0f));
            var mimic = Make(Vector2.zero, ship);
            var illusion = mimic.Illusion;
            Steps(mimic, 1);
            Assert.Less(mimic.IllusionAlpha, 1f);

            var passing = ShipRound(ship, Vector2.zero);
            Assert.IsFalse(illusion.OnProjectileHit(passing), "the illusion only blocks at full alpha");
            Assert.IsFalse(mimic.OnProjectileHit(EnemyRound(Vector2.zero)), "enemy rounds pass");

            EnemyKill? kill = null;
            mimic.Killed += (_, k) => kill = k;
            var bullet = ShipRound(ship, new Vector2(0.1f, 0f));
            bullet.TryHit(Hitbox(mimic));
            Assert.IsTrue(mimic == null);
            Assert.AreEqual(Mimic.Points, kill.Value.Points);
            Assert.IsTrue(kill.Value.AwardsScore && kill.Value.DropsPickup);
            Assert.AreEqual(new Vector3(0.1f, 0f, 0f), Object.FindAnyObjectByType<RocketExplosion>().transform.position);

            illusion.Tick(Step);
            Assert.IsTrue(illusion == null, "goes with its mimic");

            var second = Make(Vector2.zero, ship);
            Steps(second, 1);
            second.Killed += (_, k) => kill = k;
            FireRocket(Vector2.zero).Touch(Hitbox(second));
            Assert.AreEqual(EnemyDeathCause.Rocket, kill.Value.Cause);
        }

        [Test]
        public void DriftsAlongDirectionZeroAndReverses200PxOutside()
        {
            var mimic = Make(Vector2.zero, null, 1f);
            Steps(mimic, 10);
            Assert.AreEqual(Gml.Px(10f), mimic.Position.x, 1e-4f);

            var backwards = Make(Vector2.zero, null, -1f);
            Steps(backwards, 10);
            Assert.AreEqual(-Gml.Px(10f), backwards.Position.x, 1e-4f, "negative speed runs backwards");

            var outside = Make(new Vector2(Gml.Room.xMax + Gml.Px(Mimic.SizePx) + 0.01f, 0f), null, 1f);
            Steps(outside, 1);
            Assert.AreEqual(180f, outside.Direction, 1e-3f);
        }
    }
}
