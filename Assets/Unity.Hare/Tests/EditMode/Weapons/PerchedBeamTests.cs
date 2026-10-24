using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>The laser and the railgun as perched primaries (gmlHARE: ground weapons on Q's cycle, left mouse).</summary>
    public class PerchedBeamTests
    {
        const float Step = 1f / 120f;

        [TearDown]
        public void TearDown()
        {
            PerchedWeaponTestUtil.DestroyAll();
            foreach (var b in Object.FindObjectsByType<LaserBeam>(FindObjectsSortMode.None)) Object.DestroyImmediate(b.gameObject);
            foreach (var r in Object.FindObjectsByType<RailgunShot>(FindObjectsSortMode.None)) Object.DestroyImmediate(r.gameObject);
        }

        sealed class TestEnemy
        {
            public readonly List<WeaponHit> Hits = new List<WeaponHit>();
        }

        static TestEnemy SpawnEnemy(Vector2 position, float radius = 0.2f)
        {
            var go = new GameObject("TestEnemy");
            go.transform.position = position;
            go.AddComponent<CircleCollider2D>().radius = radius;
            var enemy = new TestEnemy();
            go.AddComponent<EnemyWeaponTarget>().Hit += enemy.Hits.Add;
            return enemy;
        }

        static void Run(PerchedWeapon weapon, float seconds)
        {
            for (float t = 0f; t < seconds; t += Step) weapon.Tick(Step);
        }

        [Test]
        public void TheLaserAndRailgunAreGroundPrimaries()
        {
            var laser = PerchedWeaponTestUtil.NewPerchedWeapon<LaserGun>();
            Assert.AreEqual(WeaponSlot.Primary, laser.Slot);
            Assert.AreEqual(GroundWeapon.Laser, laser.GroundKind);
            Assert.IsTrue(laser.IsGroundPrimary);
            Assert.AreEqual(AmmoPool.GroundPrimary, laser.AmmoPool);

            var rail = PerchedWeaponTestUtil.NewPerchedWeapon<RailgunGun>();
            Assert.AreEqual(GroundWeapon.Railgun, rail.GroundKind);
            Assert.IsTrue(rail.IsGroundPrimary);
        }

        [Test]
        public void LaserStaysOnWhileHeldAndRearmsOnRelease()
        {
            var laser = PerchedWeaponTestUtil.NewPerchedWeapon<LaserGun>();
            laser.TriggerHeld = true;
            Assert.IsTrue(laser.TryFire());
            var beam = laser.ActiveLaser;
            Assert.IsNotNull(beam);
            Assert.IsFalse(laser.CanFire);
            Assert.AreEqual(149, laser.Ammo, "one shot per trigger pull");

            Run(laser, 1f);
            Assert.IsFalse(laser.TryFire());
            Assert.AreSame(beam, laser.ActiveLaser, "no second beam while held");
            Assert.AreEqual(149, laser.Ammo);

            laser.TriggerHeld = false;
            Assert.IsTrue(beam == null, "release destroys obj_laser");
            Assert.IsNull(laser.ActiveLaser);
            Assert.IsTrue(laser.CanFire);
        }

        [Test]
        public void LaserRunsFromMuzzleAlongAimAndKillsTheEnemyOnItsLine()
        {
            var onLine = SpawnEnemy(new Vector2(5f, 0f));
            var offLine = SpawnEnemy(new Vector2(5f, 3f));
            var laser = PerchedWeaponTestUtil.NewPerchedWeapon<LaserGun>();
            laser.GetComponent<PlayerShipController>().SetAimDirection(0f);

            laser.TriggerHeld = true;
            laser.TryFire();
            var beam = laser.ActiveLaser;

            Assert.That(Vector2.Distance(beam.Start, laser.MuzzlePosition), Is.LessThan(1e-4f));
            Assert.That(beam.End.x - beam.Start.x, Is.EqualTo(20f).Within(0.01f), "2000 px from the muzzle");
            Assert.That(beam.End.y, Is.EqualTo(beam.Start.y).Within(1e-4f));
            Assert.AreEqual(1, onLine.Hits.Count);
            Assert.AreEqual(WeaponHitKind.Laser, onLine.Hits[0].Kind);
            Assert.AreEqual(0, offLine.Hits.Count);
        }

        [Test]
        public void RailgunRearmsAfter240StepsAndFadesOutAfter80()
        {
            var gun = PerchedWeaponTestUtil.NewPerchedWeapon<RailgunGun>();
            Assert.IsTrue(gun.TryFire());
            var rail = Object.FindAnyObjectByType<RailgunShot>();
            Assert.IsNotNull(rail);
            Assert.AreEqual(RailgunShot.DefaultAlpha, rail.Alpha);
            Assert.That(rail.SwirlPoints.Count, Is.EqualTo(Mathf.CeilToInt(2000f / 56f)));

            Assert.IsFalse(gun.TryFire());
            Run(gun, 2f - 3 * Step);
            Assert.IsFalse(gun.CanFire, "still re-arming just before 240 steps");
            Run(gun, 4 * Step);
            Assert.IsTrue(gun.CanFire);

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
    }
}
