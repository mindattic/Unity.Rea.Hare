using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Enemies;
using Unity.Hare.Player;
using Unity.Hare.Weapons;
using static Unity.Hare.Tests.EditMode.EnemyTestKit;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>UH-T14: obj_wraith's float / charge / reappear loop and its stun.</summary>
    public class WraithTests
    {
        [SetUp] public void SetUp() => Random.InitState(6060);
        [TearDown] public void TearDown() => Cleanup();

        static Wraith Make(Vector2 at, PlayerShipController ship)
        {
            var wraith = Spawn<Wraith>();
            wraith.Init(at, Gml.Room, ship);
            return wraith;
        }

        [Test]
        public void FloatsTowardTheShipThenChargesPastItAndReappears()
        {
            var ship = Ship(Vector2.zero);
            var wraith = Make(new Vector2(-6f, 0f), ship); // 600 px away, 550 from its box
            Steps(wraith, 10);
            Assert.AreEqual(WraithState.Float, wraith.State);
            Assert.AreEqual(-6f + Gml.Px(20f), wraith.Position.x, 1e-4f, "2 px/step");
            Assert.AreEqual(1, wraith.Facing);

            int guard = 0;
            while (wraith.State == WraithState.Float && guard++ < 300) Steps(wraith, 1);
            Assert.AreEqual(WraithState.Charge, wraith.State);
            Assert.That(wraith.DistanceToPlayerPx(), Is.LessThanOrEqualTo(Wraith.ChargeRangePx));

            Steps(wraith, 1); // first pass: aim 600 px beyond the ship
            Assert.AreEqual(Gml.Px(Wraith.ChargeOvershootPx), wraith.ChargeTarget.x, 1e-3f);
            Assert.AreEqual(0f, wraith.ChargeTarget.y, 1e-3f);
            Assert.AreEqual(2f + Wraith.ChargeAccelerationPxPerStep, wraith.SpeedPxPerStep, 1e-4f);

            int reappeared = 0;
            wraith.Reappeared += _ => reappeared++;
            guard = 0;
            while (wraith.State == WraithState.Charge && guard++ < 1000) Steps(wraith, 1);
            Assert.AreEqual(1, reappeared);
            Assert.AreEqual(WraithState.Float, wraith.State);
            Assert.That(wraith.Position.magnitude, Is.InRange(Gml.Px(350f) - 1e-4f, Gml.Px(400f) + 1e-4f));
            Assert.That(wraith.SpeedPxPerStep, Is.LessThanOrEqualTo(Wraith.ChargeMaxSpeedPxPerStep + 0.02f));
        }

        [Test]
        public void ShipBulletsStunItAndOnlyAStunnedWraithDiesToRockets()
        {
            var ship = Ship(new Vector2(10f, 0f));
            var wraith = Make(Vector2.zero, ship);
            EnemyKill? kill = null;
            wraith.Killed += (_, k) => kill = k;

            var rocket = FireRocket(Vector2.zero);
            rocket.Touch(Hitbox(wraith));
            Assert.IsTrue(rocket != null, "rockets fly through it unless it is stunned");
            Assert.AreEqual(0, Count<RocketExplosion>());
            Assert.IsFalse(wraith.OnProjectileHit(EnemyRound(Vector2.zero)), "only other.owner == player.id");

            var bullet = ShipRound(ship, Vector2.zero);
            Assert.IsTrue(bullet.TryHit(Hitbox(wraith)));
            Assert.IsTrue(bullet == null);
            Assert.AreEqual(WraithState.Stunned, wraith.State);
            Assert.AreEqual(1f, wraith.ImageAlpha);
            Assert.That(wraith.StunStepsLeft, Is.InRange(Wraith.MinStunSteps, Wraith.MaxStunSteps));
            Steps(wraith, 1);
            Assert.AreEqual(0f, wraith.SpeedPxPerStep, "frozen");
            Assert.AreEqual(1f, wraith.ImageAlpha, "no breathing while stunned");

            rocket.Touch(Hitbox(wraith));
            Assert.IsTrue(wraith == null);
            Assert.AreEqual(EnemyDeathCause.Rocket, kill.Value.Cause);
            Assert.AreEqual(Wraith.Points, kill.Value.Points);
            Assert.IsTrue(kill.Value.DropsPickup);
        }

        [Test]
        public void TheStunWearsOffAndRocketExplosionsDoNothing()
        {
            var ship = Ship(new Vector2(10f, 0f));
            var wraith = Make(Vector2.zero, ship);
            ShipRound(ship, Vector2.zero).TryHit(Hitbox(wraith));
            wraith.OnWeaponHit(new WeaponHit(WeaponHitKind.RocketExplosion, Vector2.zero, Vector2.right));
            Assert.IsFalse(wraith.IsDead);
            int left = Mathf.CeilToInt(wraith.StunStepsLeft);
            Steps(wraith, left);
            Assert.AreEqual(WraithState.Float, wraith.State);
            Assert.That(wraith.ImageAlpha, Is.InRange(0.3f - 0.003f, 0.6f + 0.003f), "0.3-0.6, then one breath");
        }

        [Test]
        public void LeavesAnAfterimageEveryTenSteps()
        {
            var wraith = Make(Vector2.zero, null);
            Steps(wraith, 30);
            Assert.AreEqual(3, DebrisCount(DebrisKind.WraithBlur));
            var blur = Object.FindAnyObjectByType<FadingDebris>();
            Assert.AreEqual(Wraith.BlurFadePerStep, blur.FadePerStep);
            Assert.That(blur.Alpha, Is.LessThanOrEqualTo(Wraith.BlurAlpha));
        }

        [Test]
        public void ItsAlphaBreathesBetweenPointThreeAndPointSix()
        {
            var wraith = Make(Vector2.zero, null);
            float min = 1f, max = 0f;
            for (int i = 0; i < 400; i++)
            {
                Steps(wraith, 1);
                min = Mathf.Min(min, wraith.ImageAlpha);
                max = Mathf.Max(max, wraith.ImageAlpha);
            }
            Assert.AreEqual(0.3f, min, 0.003f);
            Assert.AreEqual(0.6f, max, 0.003f);
        }
    }
}
