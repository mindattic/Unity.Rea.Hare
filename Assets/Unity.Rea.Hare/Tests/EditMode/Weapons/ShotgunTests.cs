using NUnit.Framework;
using UnityEngine;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Tests.EditMode
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
            foreach (var b in Object.FindObjectsByType<Bullet>())
                Object.DestroyImmediate(b.gameObject);
            foreach (var f in Object.FindObjectsByType<Flame>())
                Object.DestroyImmediate(f.gameObject);
            foreach (var p in Object.FindObjectsByType<PlayerShipController>())
                Object.DestroyImmediate(p.gameObject);
            foreach (var c in Object.FindObjectsByType<Collider2D>())
                Object.DestroyImmediate(c.gameObject);
            foreach (var s in Object.FindObjectsByType<Pickups.ScrapView>())
                Object.DestroyImmediate(s.gameObject);
            Pickups.PickupDirector.Reset();
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

        static int PelletsAlive() => Object.FindObjectsByType<Bullet>().Length;

        [Test]
        public void ShotgunDefaultsToTheSecondaryTrigger()
        {
            var shotgun = PerchedWeaponTestUtil.NewPerchedWeapon<Shotgun>();
            Assert.AreEqual(WeaponSlot.Secondary, shotgun.Slot); // GML: Ctrl + left mouse, fire_secondary
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
                Assert.AreEqual(Gml.Px(12f) * Gml.StepsPerSecond, pellet.Speed, 1e-4f, "velocity = 12 px a step (U.HARE-T88)");
                Assert.AreSame(player.gameObject, pellet.Owner);

                // Pellets start strung out 0..grouping along the aim from the muzzle.
                Vector2 offset = (Vector2)pellet.transform.position - muzzle;
                float along = Vector2.Dot(offset, aim);
                Assert.That(along, Is.InRange(-1e-4f, shotgun.Grouping + 1e-4f));
                Assert.AreEqual(along, offset.magnitude, 1e-4f);
            }
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
