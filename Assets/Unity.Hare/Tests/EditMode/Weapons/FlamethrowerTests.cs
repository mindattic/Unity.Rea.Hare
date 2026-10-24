using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.EditMode
{
    public class FlamethrowerTests
    {
        const float Step = 1f / 120f; // one GML step at room_gamescreen speed

        class Burnable : MonoBehaviour, IEnemyTarget
        {
            public int Hits;
            public WeaponHitKind LastKind;
            public void OnWeaponHit(in WeaponHit hit) { Hits++; LastKind = hit.Kind; }
        }

        [TearDown]
        public void TearDown() => PerchedWeaponTestUtil.DestroyAll();

        static int FlamesAlive() => Object.FindObjectsByType<Flame>(FindObjectsSortMode.None).Length;

        static Burnable NewBurnable()
        {
            var go = new GameObject("Enemy");
            go.AddComponent<CircleCollider2D>().isTrigger = true;
            return go.AddComponent<Burnable>();
        }

        [Test]
        public void FlamethrowerDefaultsToThePrimaryTrigger()
        {
            var flamer = PerchedWeaponTestUtil.NewPerchedWeapon<Flamethrower>();
            Assert.AreEqual(WeaponSlot.Primary, flamer.Slot);
        }

        [Test]
        public void EachShotIsOneFlameStraightDownTheAim()
        {
            var flamer = PerchedWeaponTestUtil.NewPerchedWeapon<Flamethrower>();
            var player = flamer.GetComponent<PlayerShipController>();
            player.SetAimDirection(120f);
            Vector2 muzzle = flamer.MuzzlePosition;

            Assert.IsTrue(flamer.TryFire());
            Assert.AreEqual(1, FlamesAlive());
            var flame = flamer.LastFlame;
            Assert.AreEqual(120f, flame.Direction, 1e-4f);
            Assert.AreEqual(4f, flame.Speed);
            Assert.AreEqual(muzzle, (Vector2)flame.transform.position);
            Assert.AreEqual(149, flamer.Ammo);
        }

        [Test]
        public void FlameGrowsFromHalfToFullSizeIntoACone()
        {
            var flamer = PerchedWeaponTestUtil.NewPerchedWeapon<Flamethrower>();
            flamer.TryFire();
            var flame = flamer.LastFlame;
            Assert.AreEqual(0.5f, flame.Scale, 1e-4f);
            Assert.AreEqual(0.5f, flame.transform.localScale.x, 1e-4f);

            for (int i = 0; i < 10; i++) flame.Step(Step);
            Assert.AreEqual(0.6f, flame.Scale, 1e-3f); // +0.01 per step

            for (int i = 0; i < 60; i++) flame.Step(Step);
            Assert.AreEqual(1f, flame.Scale, 1e-4f); // caps at full size
        }

        [Test]
        public void FlameBurnsOutAfterItsAnimation()
        {
            var flamer = PerchedWeaponTestUtil.NewPerchedWeapon<Flamethrower>();
            flamer.TryFire();
            var flame = flamer.LastFlame;
            Vector2 start = flame.transform.position;

            for (int i = 0; i < 139; i++) flame.Step(Step);
            Assert.IsTrue(flame != null, "lives for 42 frames / 0.3 = 140 steps");
            Assert.AreEqual(4f * 139f / 120f, Vector2.Distance(start, flame.transform.position), 1e-3f);

            flame.Step(Step);
            Assert.IsTrue(flame == null);
        }

        [Test]
        public void FlameBurnsThroughEnemiesButNotItsOwnShip()
        {
            var flamer = PerchedWeaponTestUtil.NewPerchedWeapon<Flamethrower>();
            var shipCollider = flamer.gameObject.AddComponent<CircleCollider2D>();
            flamer.TryFire();
            var flame = flamer.LastFlame;

            Assert.IsFalse(flame.TryBurn(shipCollider));

            var first = NewBurnable();
            var second = NewBurnable();
            Assert.IsTrue(flame.TryBurn(first.GetComponent<Collider2D>()));
            Assert.IsTrue(flame.TryBurn(second.GetComponent<Collider2D>()));
            Assert.AreEqual(1, first.Hits);
            Assert.AreEqual(1, second.Hits);
            Assert.AreEqual(WeaponHitKind.Flame, first.LastKind);

            var rock = new GameObject("Rock").AddComponent<BoxCollider2D>(); // par_rock: not an IEnemyTarget
            Assert.IsFalse(flame.TryBurn(rock));
            Assert.IsTrue(flame != null, "a flame is never spent");
        }
    }
}
