using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.EditMode
{
    public class AirWeaponTests
    {
        const float Step = 1f / 120f;

        PlayerShipController ship;
        PlayerHealth health;
        AirWeaponLoadout loadout;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Player");
            ship = go.AddComponent<PlayerShipController>();
            health = go.AddComponent<PlayerHealth>();
            loadout = go.AddComponent<AirWeaponLoadout>();
            // Awake doesn't run outside Play Mode; see PlayerShipController.ResetState.
            ship.ResetState();
            health.ResetState();
            loadout.ResetState();
            ship.SetAimDirection(0f);
        }

        [TearDown]
        public void TearDown()
        {
            DestroyAll<PlayerShipController>();
            DestroyAll<EnemyWeaponTarget>();
            DestroyAll<LaserBeam>();
            DestroyAll<RailgunShot>();
            DestroyAll<Rocket>();
            DestroyAll<RocketExplosion>();
            DestroyAll<ShieldWall>();
        }

        static void DestroyAll<T>() where T : Component
        {
            foreach (var c in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
                if (c != null) Object.DestroyImmediate(c.gameObject);
        }

        /// <summary>A stand-in par_enemy: an EnemyWeaponTarget plus the hits it recorded.</summary>
        sealed class TestEnemy
        {
            public EnemyWeaponTarget Target;
            public readonly List<WeaponHit> Hits = new List<WeaponHit>();
            public GameObject gameObject => Target.gameObject;
            public Collider2D Collider => Target.GetComponent<Collider2D>();
        }

        static TestEnemy SpawnEnemy(Vector2 position, float radius = 0.2f)
        {
            var go = new GameObject("TestEnemy");
            go.transform.position = position;
            go.AddComponent<CircleCollider2D>().radius = radius;
            var enemy = new TestEnemy { Target = go.AddComponent<EnemyWeaponTarget>() };
            enemy.Target.Hit += enemy.Hits.Add;
            return enemy;
        }

        static void Run(System.Action<float> tick, float seconds)
        {
            for (float t = 0f; t < seconds; t += Step) tick(Step);
        }

        [Test]
        public void NextAirWeaponCyclesLaserRailgunRocketAndWraps()
        {
            AirWeapon? switched = null;
            loadout.Switched += w => switched = w;

            Assert.AreEqual(AirWeapon.Laser, loadout.Current);
            Assert.IsTrue(loadout.NextAirWeapon());
            Assert.AreEqual(AirWeapon.Railgun, loadout.Current);
            Assert.AreEqual(AirWeapon.Railgun, switched);
            loadout.NextAirWeapon();
            Assert.AreEqual(AirWeapon.Rocket, loadout.Current);
            loadout.NextAirWeapon();
            Assert.AreEqual(AirWeapon.Laser, loadout.Current);
        }

        [Test]
        public void CyclingSkipsLockedWeaponsAndStaysPutWithOnlyOne()
        {
            loadout.Lock(AirWeapon.Railgun);
            loadout.NextAirWeapon();
            Assert.AreEqual(AirWeapon.Rocket, loadout.Current);

            loadout.Lock(AirWeapon.Laser);
            Assert.IsFalse(loadout.NextAirWeapon());
            Assert.AreEqual(AirWeapon.Rocket, loadout.Current);

            loadout.Unlock(AirWeapon.Railgun);
            Assert.IsTrue(loadout.NextAirWeapon());
            Assert.AreEqual(AirWeapon.Railgun, loadout.Current);
        }

        [Test]
        public void LaserStaysOnWhileHeldAndRearmsOnRelease()
        {
            loadout.SetTrigger(true);
            loadout.Tick(Step);
            var beam = loadout.ActiveLaser;
            Assert.IsNotNull(beam);
            Assert.IsFalse(loadout.CanShoot);
            Assert.AreEqual(149, loadout.Ammo(AirWeapon.Laser), "one shot per trigger pull");

            Run(loadout.Tick, 1f);
            Assert.AreSame(beam, loadout.ActiveLaser, "no second beam while held");
            Assert.AreEqual(149, loadout.Ammo(AirWeapon.Laser));

            loadout.SetTrigger(false);
            Assert.IsTrue(beam == null, "release destroys obj_laser");
            Assert.IsNull(loadout.ActiveLaser);
            Assert.IsTrue(loadout.CanShoot);
        }

        [Test]
        public void LaserRunsFromMuzzleAlongAimAndKillsTheEnemyOnItsLine()
        {
            var onLine = SpawnEnemy(new Vector2(5f, 0f));
            var offLine = SpawnEnemy(new Vector2(5f, 3f));

            loadout.SetTrigger(true);
            loadout.Tick(Step);
            var beam = loadout.ActiveLaser;

            Assert.That(Vector2.Distance(beam.Start, loadout.MuzzlePosition), Is.LessThan(1e-4f));
            Assert.That(beam.End.x - beam.Start.x, Is.EqualTo(20f - 0.34f).Within(0.01f), "2000 px from the gun, starting 34 px out");
            Assert.That(beam.End.y, Is.EqualTo(beam.Start.y).Within(1e-4f));
            Assert.AreEqual(1, onLine.Hits.Count);
            Assert.AreEqual(WeaponHitKind.Laser, onLine.Hits[0].Kind);
            Assert.AreEqual(0, offLine.Hits.Count);
        }

        [Test]
        public void RailgunRearmsAfter240StepsAndFadesOutAfter80()
        {
            loadout.NextAirWeapon();
            loadout.SetTrigger(true);
            Assert.IsTrue(loadout.TryFire());
            var rail = Object.FindAnyObjectByType<RailgunShot>();
            Assert.IsNotNull(rail);
            Assert.AreEqual(RailgunShot.DefaultAlpha, rail.Alpha);
            Assert.That(rail.SwirlPoints.Count, Is.EqualTo(Mathf.CeilToInt(2000f / 56f)));

            Assert.IsFalse(loadout.TryFire());
            loadout.SetTrigger(false);
            Assert.IsFalse(loadout.CanShoot, "releasing does not re-arm the railgun");
            Run(loadout.Tick, 2f - 3 * Step);
            Assert.IsFalse(loadout.CanShoot, "still re-arming just before 240 steps");
            Run(loadout.Tick, 4 * Step);
            Assert.IsTrue(loadout.CanShoot);

            var fresh = new GameObject("Rail").AddComponent<RailgunShot>();
            fresh.Init(Vector2.zero, 0f, 20f);
            int steps = 0;
            while (fresh.Tick(Step)) steps++;
            Assert.That(steps, Is.InRange(78, 80), "0.8 alpha fading 0.01 per step");
        }

        [Test]
        public void RailgunKillsEnemyOnTheRailWhileVisible()
        {
            var enemy = SpawnEnemy(new Vector2(8f, 0f));
            var rail = new GameObject("Rail").AddComponent<RailgunShot>();
            rail.Init(Vector2.zero, 0f, 20f);
            rail.Tick(Step);
            Assert.AreEqual(1, enemy.Hits.Count);
            Assert.AreEqual(WeaponHitKind.Railgun, enemy.Hits[0].Kind);
        }

        [Test]
        public void RocketsFireEvery25StepsFromTheMuzzleAlongTheAim()
        {
            loadout.NextAirWeapon();
            loadout.NextAirWeapon();
            ship.SetAimDirection(90f);
            loadout.SetTrigger(true);
            loadout.Tick(Step);

            var rockets = Object.FindObjectsByType<Rocket>(FindObjectsSortMode.None);
            Assert.AreEqual(1, rockets.Length);
            Assert.AreEqual(90f, rockets[0].Direction, 1e-3f);
            Assert.That(Vector2.Distance(rockets[0].Position, loadout.MuzzlePosition), Is.LessThan(1e-4f));

            Run(loadout.Tick, 20 * Step);
            Assert.AreEqual(1, Object.FindObjectsByType<Rocket>(FindObjectsSortMode.None).Length);
            Run(loadout.Tick, 6 * Step);
            Assert.AreEqual(2, Object.FindObjectsByType<Rocket>(FindObjectsSortMode.None).Length);
        }

        [Test]
        public void RocketsRunDryAt20AndRegenerate4PerSecondWithTriggerUp()
        {
            loadout.NextAirWeapon();
            loadout.NextAirWeapon();
            int dry = 0;
            loadout.DryFired += _ => dry++;

            loadout.SetTrigger(true);
            Run(loadout.Tick, 6f);
            Assert.AreEqual(0, loadout.Ammo(AirWeapon.Rocket));
            Assert.Greater(dry, 0);

            loadout.SetTrigger(false);
            Run(loadout.Tick, 1f + Step);
            Assert.AreEqual(4, loadout.Ammo(AirWeapon.Rocket));
        }

        [Test]
        public void RocketIgnitesAfter50StepsAndAcceleratesTo6PxPerStep()
        {
            var rocket = new GameObject("Rocket").AddComponent<Rocket>();
            rocket.Init(Vector2.zero, 0f);

            Run(rocket.Tick, 48 * Step);
            Assert.IsFalse(rocket.IsActive);
            Assert.AreEqual(Vector2.zero, rocket.Position);

            Run(rocket.Tick, 3 * Step);
            Assert.IsTrue(rocket.IsActive);
            Assert.AreEqual(1f, rocket.SpeedPxPerStep);

            Run(rocket.Tick, 40 * Step);
            Assert.AreEqual(Rocket.MaxSpeedPxPerStep, rocket.SpeedPxPerStep);
            Assert.Greater(rocket.Position.x, 0f);
            Assert.That(Mathf.Abs(rocket.Position.y), Is.LessThan(1e-4f));
        }

        [Test]
        public void RocketHomesOnTheNearestEnemyOnlyWithin100Px()
        {
            var near = SpawnEnemy(new Vector2(0.3f, 0.5f));
            SpawnEnemy(new Vector2(5f, 5f));
            var rocket = new GameObject("Rocket").AddComponent<Rocket>();
            rocket.Init(Vector2.zero, 0f);

            rocket.Tick(Step);
            Assert.AreSame(near.Target, rocket.Target);
            Assert.Greater(rocket.Direction, 0f, "turns toward the target");
            Assert.LessOrEqual(rocket.Direction, Rocket.TurnRateDegreesPerStep + 1e-3f);

            var far = new GameObject("Rocket2").AddComponent<Rocket>();
            Object.DestroyImmediate(near.gameObject);
            far.Init(new Vector2(-3f, -3f), 0f);
            far.Tick(Step);
            Assert.AreEqual(0f, far.Direction, 1e-3f, "target beyond lockon_range: no turn");
        }

        [Test]
        public void RocketExplodesOnTargetsAndSparesItsShipUntilOlderThanRoomSpeed()
        {
            var enemy = SpawnEnemy(new Vector2(3f, 0f));
            var rocket = new GameObject("Rocket").AddComponent<Rocket>();
            rocket.Init(Vector2.zero, 0f);
            rocket.Touch(enemy.Collider);
            Assert.AreEqual(1, enemy.Hits.Count);
            Assert.AreEqual(WeaponHitKind.Rocket, enemy.Hits[0].Kind);
            Assert.IsTrue(rocket == null, "rocket removed");
            Assert.IsNotNull(Object.FindAnyObjectByType<RocketExplosion>());

            health.Tick(1.6f); // past spawn protection
            var shipCollider = ship.gameObject.AddComponent<CircleCollider2D>();
            var young = new GameObject("Rocket").AddComponent<Rocket>();
            young.Init(Vector2.zero, 0f);
            young.Touch(shipCollider);
            Assert.AreEqual(6, health.Health, "a fresh rocket passes its launcher");

            Run(young.Tick, 1.05f);
            young.Touch(shipCollider);
            Assert.AreEqual(5, health.Health);
        }

        [Test]
        public void ExplosionHurtsThroughSpawnProtectionAndHitsEachTargetOnce()
        {
            var shipCollider = ship.gameObject.AddComponent<CircleCollider2D>();
            var enemy = SpawnEnemy(new Vector2(0.2f, 0f));
            var explosion = new GameObject("Boom").AddComponent<RocketExplosion>();
            explosion.Init(Vector2.zero);

            Assert.IsTrue(health.IsSpawnProtected);
            explosion.Touch(shipCollider);
            Assert.AreEqual(5, health.Health);
            explosion.Touch(enemy.Collider);
            explosion.Touch(enemy.Collider);
            Assert.AreEqual(1, enemy.Hits.Count);
            Assert.AreEqual(WeaponHitKind.RocketExplosion, enemy.Hits[0].Kind);

            int steps = 0;
            while (explosion.Tick(Step)) steps++;
            Assert.That(steps, Is.InRange(38, 40), "spr_bigexplosion: 20 frames at image_speed 0.5");
        }

        [Test]
        public void ABulletShootsARocketDown()
        {
            var rocket = new GameObject("Rocket").AddComponent<Rocket>();
            rocket.Init(Vector2.zero, 0f);
            Assert.IsTrue(rocket.OnProjectileHit(null), "the bullet is consumed");
            Assert.IsTrue(rocket == null);
            Assert.IsNull(Object.FindAnyObjectByType<RocketExplosion>(), "obj_tinyexplosion, not obj_rocket_explosion");
        }

        [Test]
        public void RocketBouncesOffAShieldWallOncePerWall()
        {
            var wall = new GameObject("Wall").AddComponent<ShieldWall>();
            var rocket = new GameObject("Rocket").AddComponent<Rocket>();
            rocket.Init(Vector2.zero, 0f);

            rocket.OnShieldHit(wall, Vector2.up);
            Assert.AreEqual(90f, rocket.Direction, 1e-3f);
            rocket.OnShieldHit(wall, Vector2.left);
            Assert.AreEqual(90f, rocket.Direction, 1e-3f, "GML last_impact: same wall bounces once");
        }

        [Test]
        public void WeaponsHoldFireWhileShieldingOrDead()
        {
            ship.Land();
            Assert.IsTrue(ship.TryShield());
            Assert.IsFalse(loadout.TryFire());

            ship.DropShield();
            ship.Die();
            Assert.IsFalse(loadout.TryFire());
            Assert.AreEqual(150, loadout.Ammo(AirWeapon.Laser));
        }

        [Test]
        public void SwitchingWeaponsRearmsAndDropsTheLaser()
        {
            loadout.SetTrigger(true);
            loadout.Tick(Step);
            Assert.IsNotNull(loadout.ActiveLaser);

            loadout.NextAirWeapon(); // railgun
            Assert.IsNull(loadout.ActiveLaser);
            Assert.IsTrue(loadout.TryFire());
            Assert.IsFalse(loadout.CanShoot);

            loadout.NextAirWeapon(); // rocket: next_air_weapon sets can_shoot = true
            Assert.IsTrue(loadout.CanShoot);
        }
    }
}
