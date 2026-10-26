using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.Core
{
    /// <summary>UH-T12's shared ammo economy (obj_player Create, set_ammo_cost, subtract_ground_ammo, Alarms 9/10), run outside Unity.</summary>
    public class PlayerAmmoTests
    {
        PlayerRig rig;

        [SetUp]
        public void SetUp() => rig = new PlayerRig();

        AmmoPools Ammo => rig.Ammo;

        static void AssertPool(AmmoPools ammo, AmmoPool pool, int current, int max, int cost, int regen)
        {
            Assert.AreEqual(current, ammo.Current(pool), $"{pool} current");
            Assert.AreEqual(max, ammo.Max(pool), $"{pool} max");
            Assert.AreEqual(cost, ammo.Cost(pool), $"{pool} cost");
            Assert.AreEqual(regen, ammo.Regen(pool), $"{pool} regen");
        }

        [Test]
        public void PoolsStartAtObjPlayersCreateValues()
        {
            AssertPool(Ammo, AmmoPool.GroundPrimary, 150, 150, 1, 10);
            AssertPool(Ammo, AmmoPool.GroundSecondary, 12, 12, 4, 2);
            AssertPool(Ammo, AmmoPool.AirPrimary, 20, 20, 1, 4);
            AssertPool(Ammo, AmmoPool.AirSecondary, 8, 8, 1, 1);
            Assert.Less(Ammo.PrimaryRegenTimer, 0f, "Create never sets alarm_ammo_regen");
            Assert.Less(Ammo.SecondaryRegenTimer, 0f);
        }

        [Test]
        public void SetAmmoCostCapsTheVulcanWithoutTouchingTheCount()
        {
            rig.Do(new SetAmmoCost(rig.Id, GroundWeapon.Vulcan, GroundWeapon.Shotgun, AirWeapon.Rocket, true));
            AssertPool(Ammo, AmmoPool.GroundPrimary, 150, 120, 1, 10);
            AssertPool(Ammo, AmmoPool.GroundSecondary, 12, 12, 4, 2);
            AssertPool(Ammo, AmmoPool.AirPrimary, 20, 20, 1, 4);
            AssertPool(Ammo, AmmoPool.AirSecondary, 8, 8, 1, 1);
        }

        [Test]
        public void SetAmmoCostLeavesTheMaximumOfOtherGroundPrimaries()
        {
            rig.Do(new SetAmmoCost(rig.Id, GroundWeapon.Vulcan, null, null, false));
            rig.Do(new SetAmmoCost(rig.Id, GroundWeapon.Flamer, null, null, false));
            Assert.AreEqual(120, Ammo.Max(AmmoPool.GroundPrimary), "only the vulcan branch writes the max");
        }

        [Test]
        public void ShotgunCostsFourAsSecondaryButOneAsPrimary()
        {
            rig.Do(new SetAmmoCost(rig.Id, GroundWeapon.Shotgun, GroundWeapon.Flamer, null, false));
            Assert.AreEqual(1, Ammo.Cost(AmmoPool.GroundPrimary));
            Assert.AreEqual(1, Ammo.Cost(AmmoPool.GroundSecondary));

            rig.Do(new SetAmmoCost(rig.Id, GroundWeapon.Flamer, GroundWeapon.Shotgun, null, false));
            Assert.AreEqual(4, Ammo.Cost(AmmoPool.GroundSecondary));
            Assert.AreEqual(12, Ammo.Max(AmmoPool.GroundSecondary));
        }

        [Test]
        public void RocketsSetTheSecondaryCostNotThePrimarysSic()
        {
            rig.Do(new SetAmmoCost(rig.Id, null, null, AirWeapon.Rocket, false));
            Assert.AreEqual(20, Ammo.Max(AmmoPool.AirPrimary));
            Assert.AreEqual(1, Ammo.Cost(AmmoPool.AirSecondary));
        }

        [Test]
        public void LandingAndLeapingRunSetAmmoCostOnTheEquippedWeapons()
        {
            rig.Install(1, GunType.Vulcan, WeaponSlot.Primary);
            rig.Install(2, GunType.Shotgun, WeaponSlot.Secondary); // ground secondary by default
            rig.Do(new SetAmmoCost(rig.Id, GroundWeapon.Flamer, GroundWeapon.Flamer, null, false)); // something to overwrite
            Assert.AreEqual(150, Ammo.Max(AmmoPool.GroundPrimary));
            Assert.AreEqual(1, Ammo.Cost(AmmoPool.GroundSecondary));

            rig.Do(new Land(rig.Id));
            Assert.AreEqual(120, Ammo.Max(AmmoPool.GroundPrimary), "set_ammo_cost on landing");
            Assert.AreEqual(4, Ammo.Cost(AmmoPool.GroundSecondary));

            rig.Do(new SetAmmoCost(rig.Id, GroundWeapon.Flamer, GroundWeapon.Flamer, null, false));
            Assert.IsTrue(rig.Do(new Leap(rig.Id, rig.Flight.Direction)).Accepted);
            Assert.AreEqual(4, Ammo.Cost(AmmoPool.GroundSecondary), "set_ammo_cost on leaping");
        }

        [Test]
        public void SetAmmoCostFollowsTheGroundLoadoutsEquippedPrimary()
        {
            rig.Install(1, GunType.Vulcan, WeaponSlot.Primary);
            rig.Install(2, GunType.Shotgun, WeaponSlot.Primary);
            rig.Do(new ResetGroundLoadout(rig.Id));
            rig.Do(new EquipGroundWeapon(rig.Id, GroundWeapon.Shotgun));

            rig.Do(new Land(rig.Id));
            Assert.AreEqual(150, Ammo.Max(AmmoPool.GroundPrimary), "the shotgun branch leaves the max alone");
            Assert.AreEqual(1, Ammo.Cost(AmmoPool.GroundPrimary));
        }

        [Test]
        public void SubtractTakesTheCostAndNeverGoesBelowZero()
        {
            Assert.IsTrue(Ammo.TrySpend(AmmoPool.GroundSecondary));
            Assert.AreEqual(8, Ammo.Current(AmmoPool.GroundSecondary));

            rig.Do(new SetAmmo(rig.Id, AmmoPool.GroundSecondary, 3));
            Assert.IsFalse(Ammo.HasAmmo(AmmoPool.GroundSecondary), "3 < cost 4");
            Assert.IsFalse(Ammo.TrySpend(AmmoPool.GroundSecondary));
            Assert.AreEqual(3, Ammo.Current(AmmoPool.GroundSecondary), "nothing spent on a dry pull");

            Ammo.Subtract(AmmoPool.GroundSecondary); // subtract_ground_ammo's clamp
            Assert.AreEqual(0, Ammo.Current(AmmoPool.GroundSecondary));
        }

        [Test]
        public void RefillTopsAPoolUpToItsMaximum()
        {
            rig.Do(new SetAmmo(rig.Id, AmmoPool.AirPrimary, 2));
            rig.Do(new RefillAmmo(rig.Id, AmmoPool.AirPrimary));
            Assert.AreEqual(20, Ammo.Current(AmmoPool.AirPrimary));
        }

        [Test]
        public void GroundPrimariesShareOnePool()
        {
            int vulcan = rig.Install(1, GunType.Vulcan, WeaponSlot.Primary);
            int flamer = rig.Install(2, GunType.Flamer, WeaponSlot.Primary);
            int laser = rig.Install(3, GunType.Laser, WeaponSlot.Primary); // a GML ground weapon too
            rig.Do(new ResetAirLoadout(rig.Id));
            rig.Landed();

            Assert.IsTrue(rig.Do(new FireGun(rig.Id, vulcan)).Accepted);
            Assert.IsTrue(rig.Do(new FireGun(rig.Id, flamer)).Accepted);
            Assert.IsTrue(rig.Do(new FireGun(rig.Id, laser)).Accepted);
            rig.Do(new Leap(rig.Id, rig.Flight.Direction)); // the air loadout fires in flight
            Assert.IsTrue(rig.Do(new FireAir(rig.Id)).Accepted);
            Assert.AreEqual(147, Ammo.Current(AmmoPool.GroundPrimary));
            Assert.AreEqual(19, Ammo.Current(AmmoPool.AirPrimary), "rockets spend air_ammo_primary");
        }

        [Test]
        public void RegenWaitsForTheFirstReleaseThenRunsEveryRoomSpeed()
        {
            rig.Do(new SetAmmo(rig.Id, AmmoPool.GroundPrimary, 50));
            rig.Do(new SetAmmo(rig.Id, AmmoPool.GroundSecondary, 0));
            rig.Do(new TickAmmo(rig.Id, 5f));
            Assert.AreEqual(50, Ammo.Current(AmmoPool.GroundPrimary), "no alarm until the trigger is released");

            rig.Do(new PressAmmoTrigger(rig.Id));
            rig.Do(new ReleaseAmmoTrigger(rig.Id));
            rig.Do(new TickAmmo(rig.Id, 0.99f));
            Assert.AreEqual(50, Ammo.Current(AmmoPool.GroundPrimary));
            rig.Do(new TickAmmo(rig.Id, 0.02f));
            Assert.AreEqual(60, Ammo.Current(AmmoPool.GroundPrimary));
            Assert.AreEqual(2, Ammo.Current(AmmoPool.GroundSecondary));
            rig.Do(new TickAmmo(rig.Id, 1f));
            Assert.AreEqual(70, Ammo.Current(AmmoPool.GroundPrimary));
            Assert.AreEqual(4, Ammo.Current(AmmoPool.GroundSecondary));
        }

        [Test]
        public void PressingStopsOnlyThePrimaryRegen()
        {
            rig.Do(new SetAmmo(rig.Id, AmmoPool.GroundPrimary, 50));
            rig.Do(new SetAmmo(rig.Id, AmmoPool.AirSecondary, 0));
            rig.Do(new PressAmmoTrigger(rig.Id));
            rig.Do(new ReleaseAmmoTrigger(rig.Id));

            rig.Do(new PressAmmoTrigger(rig.Id)); // GML: alarm[alarm_ammo_regen] = 0, alarm 10 untouched
            rig.Do(new TickAmmo(rig.Id, 3.01f));
            Assert.AreEqual(50, Ammo.Current(AmmoPool.GroundPrimary));
            Assert.AreEqual(3, Ammo.Current(AmmoPool.AirSecondary));
        }

        [Test]
        public void RegenWaitsUntilEveryHolderHasLetGo()
        {
            rig.Do(new SetAmmo(rig.Id, AmmoPool.GroundPrimary, 50));
            rig.Do(new PressAmmoTrigger(rig.Id, 11));
            rig.Do(new PressAmmoTrigger(rig.Id, 12));
            rig.Do(new ReleaseAmmoTrigger(rig.Id, 11));
            rig.Do(new TickAmmo(rig.Id, 2f));
            Assert.AreEqual(50, Ammo.Current(AmmoPool.GroundPrimary));

            rig.Do(new ReleaseAmmoTrigger(rig.Id, 12));
            rig.Do(new TickAmmo(rig.Id, 1.01f));
            Assert.AreEqual(60, Ammo.Current(AmmoPool.GroundPrimary));
        }

        [Test]
        public void LandingWithTheVulcanLeavesTheGroundPrimaryAboveItsLoweredMaximum()
        {
            // HARE.gmx: Create sets ground_ammo_primary 150; set_ammo_cost only writes the max (120), so "150 / 120" is original behaviour.
            rig.Install(1, GunType.Vulcan, WeaponSlot.Primary);
            rig.Do(new Land(rig.Id));
            Assert.AreEqual(120, Ammo.Max(AmmoPool.GroundPrimary));
            Assert.AreEqual(150, Ammo.Current(AmmoPool.GroundPrimary), "set_ammo_cost does not touch the count");
        }

        [Test]
        public void RegenSnapsACountAboveTheMaximumDownSilently()
        {
            rig.Do(new SetAmmoCost(rig.Id, GroundWeapon.Vulcan, null, null, false));
            rig.Do(new SetAmmo(rig.Id, AmmoPool.GroundPrimary, 140)); // Create's 150 minus a burst, over the vulcan's 120

            rig.Do(new PressAmmoTrigger(rig.Id));
            rig.Do(new ReleaseAmmoTrigger(rig.Id));
            rig.Do(new TickAmmo(rig.Id, 1.01f));
            Assert.AreEqual(120, Ammo.Current(AmmoPool.GroundPrimary));
            CollectionAssert.IsEmpty(rig.Seen<AmmoReloaded>().ToList());
        }

        [Test]
        public void ReloadSoundsFollowAlarmNinesComparisons()
        {
            rig.Do(new SetAmmo(rig.Id, AmmoPool.GroundPrimary, 140));
            rig.Do(new SetAmmo(rig.Id, AmmoPool.AirPrimary, 16));

            rig.Do(new PressAmmoTrigger(rig.Id));
            rig.Do(new ReleaseAmmoTrigger(rig.Id));
            rig.Do(new TickAmmo(rig.Id, 1.01f));

            // Ground reaches 150 exactly: >= max, so sfx_reload_full. Air reaches 20 exactly: GML tests > max, so half.
            var reloads = rig.Seen<AmmoReloaded>().Select(r => (r.Pool, r.Full)).ToList();
            CollectionAssert.AreEqual(new[] { (AmmoPool.GroundPrimary, true), (AmmoPool.AirPrimary, false) }, reloads);
            Assert.AreEqual(150, Ammo.Current(AmmoPool.GroundPrimary));
            Assert.AreEqual(20, Ammo.Current(AmmoPool.AirPrimary));
        }

        [Test]
        public void MineLauncherSpendsAirSecondaryAndShotgunGroundSecondary()
        {
            int launcher = rig.Install(1, GunType.MineLauncher, WeaponSlot.Secondary);
            Assert.IsTrue(rig.Do(new FireGun(rig.Id, launcher)).Accepted); // in flight
            Assert.AreEqual(7, Ammo.Current(AmmoPool.AirSecondary));

            rig.Landed();
            int shotgun = rig.Install(2, GunType.Shotgun, WeaponSlot.Secondary);
            Assert.IsTrue(rig.Do(new FireGun(rig.Id, shotgun)).Accepted);
            Assert.AreEqual(8, Ammo.Current(AmmoPool.GroundSecondary));
            Assert.AreEqual(150, Ammo.Current(AmmoPool.GroundPrimary));
        }

        [Test]
        public void TwoSeatsKeepSeparateAmmo()
        {
            var other = rig.World.AddPlayer(new PlayerId(2));
            rig.Do(new SetAmmo(rig.Id, AmmoPool.GroundPrimary, 5));
            Assert.AreEqual(5, rig.Ammo.Current(AmmoPool.GroundPrimary));
            Assert.AreEqual(150, other.Ammo.Current(AmmoPool.GroundPrimary));
        }
    }
}
