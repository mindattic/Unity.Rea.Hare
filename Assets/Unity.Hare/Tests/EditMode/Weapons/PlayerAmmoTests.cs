using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>UH-T12: the shared ammo economy (obj_player Create, set_ammo_cost, subtract_ground_ammo, Alarms 9/10).</summary>
    public class PlayerAmmoTests
    {
        PlayerShipController ship;
        PlayerAmmo ammo;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Player");
            ship = go.AddComponent<PlayerShipController>();
            ship.ResetState(); // Awake doesn't run in EditMode; the ship starts in flight
            ammo = PlayerAmmo.Of(go);
        }

        [TearDown]
        public void TearDown()
        {
            PerchedWeaponTestUtil.DestroyAll();
            foreach (var r in Object.FindObjectsByType<Rocket>(FindObjectsSortMode.None)) Object.DestroyImmediate(r.gameObject);
            foreach (var m in Object.FindObjectsByType<Mine>(FindObjectsSortMode.None)) Object.DestroyImmediate(m.gameObject);
            foreach (var l in Object.FindObjectsByType<LaserBeam>(FindObjectsSortMode.None)) Object.DestroyImmediate(l.gameObject);
        }

        T Add<T>() where T : Component => ship.gameObject.AddComponent<T>();

        static void AssertPool(PlayerAmmo ammo, AmmoPool pool, int current, int max, int cost, int regen)
        {
            Assert.AreEqual(current, ammo.Current(pool), $"{pool} current");
            Assert.AreEqual(max, ammo.Max(pool), $"{pool} max");
            Assert.AreEqual(cost, ammo.Cost(pool), $"{pool} cost");
            Assert.AreEqual(regen, ammo.Regen(pool), $"{pool} regen");
        }

        [Test]
        public void PoolsStartAtObjPlayersCreateValues()
        {
            AssertPool(ammo, AmmoPool.GroundPrimary, 150, 150, 1, 10);
            AssertPool(ammo, AmmoPool.GroundSecondary, 12, 12, 4, 2);
            AssertPool(ammo, AmmoPool.AirPrimary, 20, 20, 1, 4);
            AssertPool(ammo, AmmoPool.AirSecondary, 8, 8, 1, 1);
            Assert.Less(ammo.PrimaryRegenTimer, 0f, "Create never sets alarm_ammo_regen");
            Assert.Less(ammo.SecondaryRegenTimer, 0f);
        }

        [Test]
        public void SetAmmoCostCapsTheVulcanWithoutTouchingTheCount()
        {
            ammo.SetAmmoCost(GroundWeapon.Vulcan, GroundWeapon.Shotgun, AirWeapon.Rocket, true);
            AssertPool(ammo, AmmoPool.GroundPrimary, 150, 120, 1, 10);
            AssertPool(ammo, AmmoPool.GroundSecondary, 12, 12, 4, 2);
            AssertPool(ammo, AmmoPool.AirPrimary, 20, 20, 1, 4);
            AssertPool(ammo, AmmoPool.AirSecondary, 8, 8, 1, 1);
        }

        [Test]
        public void SetAmmoCostLeavesTheMaximumOfOtherGroundPrimaries()
        {
            ammo.SetAmmoCost(GroundWeapon.Vulcan, null, null, false);
            ammo.SetAmmoCost(GroundWeapon.Flamer, null, null, false);
            Assert.AreEqual(120, ammo.Max(AmmoPool.GroundPrimary), "only the vulcan branch writes the max");
        }

        [Test]
        public void ShotgunCostsFourAsSecondaryButOneAsPrimary()
        {
            ammo.SetAmmoCost(GroundWeapon.Shotgun, GroundWeapon.Flamer, null, false);
            Assert.AreEqual(1, ammo.Cost(AmmoPool.GroundPrimary));
            Assert.AreEqual(1, ammo.Cost(AmmoPool.GroundSecondary));

            ammo.SetAmmoCost(GroundWeapon.Flamer, GroundWeapon.Shotgun, null, false);
            Assert.AreEqual(4, ammo.Cost(AmmoPool.GroundSecondary));
            Assert.AreEqual(12, ammo.Max(AmmoPool.GroundSecondary));
        }

        [Test]
        public void LandingAndLeapingRunSetAmmoCostOnTheEquippedWeapons()
        {
            var vulcan = Add<VulcanWeapon>();
            vulcan.ResetState();
            var shotgun = Add<Shotgun>(); // ground secondary by default
            shotgun.ResetState();
            ammo.SetAmmoCost(GroundWeapon.Flamer, GroundWeapon.Flamer, null, false); // something to overwrite
            Assert.AreEqual(150, ammo.Max(AmmoPool.GroundPrimary));
            Assert.AreEqual(1, ammo.Cost(AmmoPool.GroundSecondary));

            ship.Land();
            Assert.AreEqual(120, ammo.Max(AmmoPool.GroundPrimary), "set_ammo_cost on landing");
            Assert.AreEqual(4, ammo.Cost(AmmoPool.GroundSecondary));

            ammo.SetAmmoCost(GroundWeapon.Flamer, GroundWeapon.Flamer, null, false);
            Assert.IsTrue(ship.TryLeap(ship.Direction));
            Assert.AreEqual(4, ammo.Cost(AmmoPool.GroundSecondary), "set_ammo_cost on leaping");
        }

        [Test]
        public void SetAmmoCostFollowsTheGroundLoadoutsEquippedPrimary()
        {
            Add<VulcanWeapon>().ResetState();
            var shotgun = Add<Shotgun>();
            shotgun.Slot = WeaponSlot.Primary;
            shotgun.ResetState();
            var loadout = Add<GroundWeaponLoadout>();
            loadout.ResetState();
            loadout.Equip(GroundWeapon.Shotgun);

            ship.Land();
            Assert.AreEqual(150, ammo.Max(AmmoPool.GroundPrimary), "the shotgun branch leaves the max alone");
            Assert.AreEqual(1, ammo.Cost(AmmoPool.GroundPrimary));
        }

        [Test]
        public void SubtractTakesTheCostAndNeverGoesBelowZero()
        {
            Assert.IsTrue(ammo.TrySpend(AmmoPool.GroundSecondary));
            Assert.AreEqual(8, ammo.Current(AmmoPool.GroundSecondary));

            ammo.SetCurrent(AmmoPool.GroundSecondary, 3);
            Assert.IsFalse(ammo.HasAmmo(AmmoPool.GroundSecondary), "3 < cost 4");
            Assert.IsFalse(ammo.TrySpend(AmmoPool.GroundSecondary));
            Assert.AreEqual(3, ammo.Current(AmmoPool.GroundSecondary), "nothing spent on a dry pull");

            ammo.Subtract(AmmoPool.GroundSecondary); // subtract_ground_ammo's clamp
            Assert.AreEqual(0, ammo.Current(AmmoPool.GroundSecondary));
        }

        [Test]
        public void GroundPrimariesShareOnePool()
        {
            ship.Land();
            var vulcan = Add<VulcanWeapon>();
            vulcan.ResetState();
            var flamer = Add<Flamethrower>();
            flamer.ResetState();
            var air = Add<AirWeaponLoadout>(); // its laser is a GML ground weapon too
            air.ResetState();

            Assert.IsTrue(vulcan.TryFire());
            Assert.IsTrue(flamer.TryFire());
            Assert.IsTrue(air.TryFire());
            Assert.AreEqual(147, vulcan.Ammo);
            Assert.AreEqual(147, flamer.Ammo);
            Assert.AreEqual(147, air.Ammo(AirWeapon.Laser));
            Assert.AreEqual(20, air.Ammo(AirWeapon.Rocket), "rockets spend air_ammo_primary");
        }

        [Test]
        public void RegenWaitsForTheFirstReleaseThenRunsEveryRoomSpeed()
        {
            ammo.SetCurrent(AmmoPool.GroundPrimary, 50);
            ammo.SetCurrent(AmmoPool.GroundSecondary, 0);
            ammo.Tick(5f);
            Assert.AreEqual(50, ammo.Current(AmmoPool.GroundPrimary), "no alarm until the trigger is released");

            ammo.PressTrigger();
            ammo.ReleaseTrigger();
            ammo.Tick(0.99f);
            Assert.AreEqual(50, ammo.Current(AmmoPool.GroundPrimary));
            ammo.Tick(0.02f);
            Assert.AreEqual(60, ammo.Current(AmmoPool.GroundPrimary));
            Assert.AreEqual(2, ammo.Current(AmmoPool.GroundSecondary));
            ammo.Tick(1f);
            Assert.AreEqual(70, ammo.Current(AmmoPool.GroundPrimary));
            Assert.AreEqual(4, ammo.Current(AmmoPool.GroundSecondary));
        }

        [Test]
        public void PressingStopsOnlyThePrimaryRegen()
        {
            ammo.SetCurrent(AmmoPool.GroundPrimary, 50);
            ammo.SetCurrent(AmmoPool.AirSecondary, 0);
            ammo.PressTrigger();
            ammo.ReleaseTrigger();

            ammo.PressTrigger(); // GML: alarm[alarm_ammo_regen] = 0, alarm 10 untouched
            ammo.Tick(3.01f);
            Assert.AreEqual(50, ammo.Current(AmmoPool.GroundPrimary));
            Assert.AreEqual(3, ammo.Current(AmmoPool.AirSecondary));
        }

        [Test]
        public void RegenWaitsUntilEveryHolderHasLetGo()
        {
            ammo.SetCurrent(AmmoPool.GroundPrimary, 50);
            object primary = new object(), secondary = new object();
            ammo.PressTrigger(primary);
            ammo.PressTrigger(secondary);
            ammo.ReleaseTrigger(primary);
            ammo.Tick(2f);
            Assert.AreEqual(50, ammo.Current(AmmoPool.GroundPrimary));

            ammo.ReleaseTrigger(secondary);
            ammo.Tick(1.01f);
            Assert.AreEqual(60, ammo.Current(AmmoPool.GroundPrimary));
        }

        [Test]
        public void RegenSnapsACountAboveTheMaximumDownSilently()
        {
            var reloads = new List<AmmoPool>();
            ammo.Reloaded += (pool, _) => reloads.Add(pool);
            ammo.SetAmmoCost(GroundWeapon.Vulcan, null, null, false);
            ammo.SetCurrent(AmmoPool.GroundPrimary, 140); // Create's 150 minus a burst, over the vulcan's 120

            ammo.PressTrigger();
            ammo.ReleaseTrigger();
            ammo.Tick(1.01f);
            Assert.AreEqual(120, ammo.Current(AmmoPool.GroundPrimary));
            CollectionAssert.IsEmpty(reloads);
        }

        [Test]
        public void ReloadSoundsFollowAlarmNinesComparisons()
        {
            var reloads = new List<(AmmoPool pool, bool full)>();
            ammo.Reloaded += (pool, full) => reloads.Add((pool, full));
            ammo.SetCurrent(AmmoPool.GroundPrimary, 140);
            ammo.SetCurrent(AmmoPool.AirPrimary, 16);

            ammo.PressTrigger();
            ammo.ReleaseTrigger();
            ammo.Tick(1.01f);

            // Ground reaches 150 exactly: >= max, so sfx_reload_full. Air reaches 20 exactly: GML tests > max, so half.
            CollectionAssert.AreEqual(new[] { (AmmoPool.GroundPrimary, true), (AmmoPool.AirPrimary, false) }, reloads);
            Assert.AreEqual(150, ammo.Current(AmmoPool.GroundPrimary));
            Assert.AreEqual(20, ammo.Current(AmmoPool.AirPrimary));
        }

        [Test]
        public void MineLauncherSpendsAirSecondaryAndShotgunGroundSecondary()
        {
            var launcher = Add<MineLauncher>();
            launcher.ResetState();
            Assert.IsTrue(launcher.TryFire()); // in flight
            Assert.AreEqual(7, ammo.Current(AmmoPool.AirSecondary));

            ship.Land();
            var shotgun = Add<Shotgun>();
            shotgun.ResetState();
            Assert.IsTrue(shotgun.TryFire());
            Assert.AreEqual(8, ammo.Current(AmmoPool.GroundSecondary));
            Assert.AreEqual(150, ammo.Current(AmmoPool.GroundPrimary));
        }
    }
}
