using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.EditMode
{
    public static class PerchedWeaponTestUtil
    {
        public static T NewPerchedWeapon<T>() where T : PerchedWeapon
        {
            var go = new GameObject("Player");
            var player = go.AddComponent<PlayerShipController>();
            player.ResetState(); // Awake doesn't run in EditMode
            player.Land();
            var weapon = go.AddComponent<T>();
            weapon.ResetState();
            return weapon;
        }

        public static void DestroyAll()
        {
            foreach (var b in Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None))
                Object.DestroyImmediate(b.gameObject);
            foreach (var f in Object.FindObjectsByType<Flame>(FindObjectsSortMode.None))
                Object.DestroyImmediate(f.gameObject);
            foreach (var p in Object.FindObjectsByType<PlayerShipController>(FindObjectsSortMode.None))
                Object.DestroyImmediate(p.gameObject);
            foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
                Object.DestroyImmediate(c.gameObject);
        }
    }

    public class ShotgunTests
    {
        class Target : MonoBehaviour, IProjectileTarget
        {
            public int Hits;
            public bool OnProjectileHit(Bullet bullet) { Hits++; return true; }
        }

        [TearDown]
        public void TearDown() => PerchedWeaponTestUtil.DestroyAll();

        static int PelletsAlive() => Object.FindObjectsByType<Bullet>(FindObjectsSortMode.None).Length;

        [Test]
        public void ShotgunDefaultsToTheSecondaryTrigger()
        {
            var shotgun = PerchedWeaponTestUtil.NewPerchedWeapon<Shotgun>();
            Assert.AreEqual(WeaponSlot.Secondary, shotgun.Slot); // GML: Ctrl + left mouse, fire_secondary
        }

        [Test]
        public void ShotgunDoesNotFireWhileFlying()
        {
            var shotgun = PerchedWeaponTestUtil.NewPerchedWeapon<Shotgun>();
            var player = shotgun.GetComponent<PlayerShipController>();
            Assert.IsTrue(player.TryLeap(player.Direction));

            Assert.IsFalse(shotgun.TryFire());
            Assert.AreEqual(0, PelletsAlive());
            Assert.AreEqual(shotgun.MaxAmmo, shotgun.Ammo);
        }

        [Test]
        public void ShotgunDoesNotFireWhileShieldingOrDead()
        {
            var shotgun = PerchedWeaponTestUtil.NewPerchedWeapon<Shotgun>();
            var player = shotgun.GetComponent<PlayerShipController>();

            Assert.IsTrue(player.TryShield());
            Assert.IsFalse(shotgun.TryFire());
            player.DropShield();

            player.Die();
            Assert.IsFalse(shotgun.TryFire());
            Assert.AreEqual(0, PelletsAlive());
        }

        [Test]
        public void BlastSpawnsSixteenPelletsWithinSpreadOfAim()
        {
            var shotgun = PerchedWeaponTestUtil.NewPerchedWeapon<Shotgun>();
            var player = shotgun.GetComponent<PlayerShipController>();
            player.SetAimDirection(30f);
            Vector2 muzzle = shotgun.MuzzlePosition;

            Assert.IsTrue(shotgun.TryFire());
            Assert.AreEqual(16, shotgun.LastBlast.Count);
            Assert.AreEqual(16, PelletsAlive());

            Vector2 aim = new Vector2(Mathf.Cos(30f * Mathf.Deg2Rad), Mathf.Sin(30f * Mathf.Deg2Rad));
            foreach (var pellet in shotgun.LastBlast)
            {
                float spread = Mathf.DeltaAngle(30f, pellet.Direction);
                Assert.LessOrEqual(Mathf.Abs(spread), 8f + 1e-3f);
                Assert.AreEqual(Mathf.Round(spread), spread, 1e-3f, "spread is whole degrees (irandom_range)");
                Assert.AreEqual(12f, pellet.Speed);
                Assert.AreSame(player.gameObject, pellet.Owner);

                // Pellets start strung out 0..grouping along the aim from the muzzle.
                Vector2 offset = (Vector2)pellet.transform.position - muzzle;
                float along = Vector2.Dot(offset, aim);
                Assert.That(along, Is.InRange(-1e-4f, shotgun.Grouping + 1e-4f));
                Assert.AreEqual(along, offset.magnitude, 1e-4f);
            }
        }

        [Test]
        public void ShotgunReloadsForOneSecondBetweenBlasts()
        {
            var shotgun = PerchedWeaponTestUtil.NewPerchedWeapon<Shotgun>();
            Assert.IsTrue(shotgun.TryFire());
            Assert.IsFalse(shotgun.TryFire());

            shotgun.Tick(0.9f);
            Assert.IsFalse(shotgun.TryFire());
            shotgun.Tick(0.11f);
            Assert.IsTrue(shotgun.TryFire());
        }

        [Test]
        public void ShotgunSpendsFourOfTwelveAndRunsDryAfterThreeBlasts()
        {
            var shotgun = PerchedWeaponTestUtil.NewPerchedWeapon<Shotgun>();
            int empty = 0;
            shotgun.OutOfAmmo += () => empty++;
            Assert.AreEqual(12, shotgun.Ammo);

            shotgun.TriggerHeld = true; // GML: no ammo regen while the mouse button is down
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(shotgun.TryFire(), $"blast {i + 1}");
                Assert.AreEqual(12 - 4 * (i + 1), shotgun.Ammo);
                shotgun.Tick(1f);
            }

            Assert.AreEqual(0, shotgun.Ammo);
            Assert.IsFalse(shotgun.TryFire());
            Assert.AreEqual(1, empty);

            // Releasing starts the regen alarm with its full delay.
            shotgun.TriggerHeld = false;
            shotgun.AmmoEconomy.Tick(0.9f);
            Assert.AreEqual(0, shotgun.Ammo);
            shotgun.AmmoEconomy.Tick(0.11f);
            Assert.AreEqual(2, shotgun.Ammo);
        }

        [Test]
        public void ShotgunAmmoRefillsTwoPerSecondUpToTwelve()
        {
            var shotgun = PerchedWeaponTestUtil.NewPerchedWeapon<Shotgun>();
            shotgun.TriggerHeld = true;
            shotgun.TryFire();
            shotgun.TriggerHeld = false;
            Assert.AreEqual(8, shotgun.Ammo);

            shotgun.AmmoEconomy.Tick(1f);
            Assert.AreEqual(10, shotgun.Ammo);
            shotgun.AmmoEconomy.Tick(5f);
            Assert.AreEqual(12, shotgun.Ammo);
        }

        [Test]
        public void PelletHitsATargetAndIsSpentButSkipsItsOwnShip()
        {
            var shotgun = PerchedWeaponTestUtil.NewPerchedWeapon<Shotgun>();
            var shipCollider = shotgun.gameObject.AddComponent<CircleCollider2D>();
            shotgun.TryFire();
            var pellet = shotgun.LastBlast[0];

            Assert.IsFalse(pellet.TryHit(shipCollider));
            Assert.IsTrue(pellet != null);

            var enemy = new GameObject("Enemy");
            var enemyCollider = enemy.AddComponent<CircleCollider2D>();
            var target = enemy.AddComponent<Target>();
            Assert.IsTrue(pellet.TryHit(enemyCollider));
            Assert.AreEqual(1, target.Hits);
            Assert.IsTrue(pellet == null);
        }
    }
}
