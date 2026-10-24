using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.Core
{
    /// <summary>The ship's starting guns as data (obj_player Create), validated before a ship is built from them.</summary>
    public class ShipLoadoutTests
    {
        [Test]
        public void TheDefaultIsObjPlayersCreate()
        {
            var loadout = new ShipLoadout();
            CollectionAssert.IsEmpty(loadout.Validate());

            var rig = new PlayerRig();
            int key = 0;
            foreach (var mount in loadout.Guns) rig.Install(++key, mount.Type, mount.Slot);
            rig.Do(new ResetGroundLoadout(rig.Id));

            // next_ground_weapon's whole cycle, ground_weapon = weapon_vulcan first.
            CollectionAssert.AreEqual(
                new[] { GroundWeapon.Vulcan, GroundWeapon.Laser, GroundWeapon.Railgun, GroundWeapon.Shotgun, GroundWeapon.Flamer },
                rig.Ship.Ground.Weapons);
            Assert.AreEqual(GroundWeapon.Vulcan, rig.Ship.Ground.Current);

            // ground_weapon_secondary = weapon_shotgun, air_weapon_secondary = weapon_mine.
            var secondaries = rig.Ship.Guns.Guns.Where(g => g.Slot == WeaponSlot.Secondary).Select(g => g.Type);
            CollectionAssert.AreEquivalent(new[] { GunType.Shotgun, GunType.MineLauncher }, secondaries);
        }

        [Test]
        public void TheShipStartsOnTheVulcanWhateverOrderItsGunsWereInstalledIn()
        {
            // A scene wakes its components in no promised order: the loadout first, then the guns back to front.
            var rig = new PlayerRig();
            rig.Do(new ResetGroundLoadout(rig.Id));
            var guns = new ShipLoadout().Guns;
            for (int i = guns.Length - 1; i >= 0; i--) rig.Install(i + 1, guns[i].Type, guns[i].Slot);
            Assert.AreEqual(GroundWeapon.Vulcan, rig.Ship.Ground.Current, "ground_weapon = weapon_vulcan");

            // Once the player has switched, a gun added later does not take the slot from them.
            rig.Do(new NextGroundWeapon(rig.Id));
            Assert.AreEqual(GroundWeapon.Laser, rig.Ship.Ground.Current);
            rig.Do(new RemoveGun(rig.Id, 1));
            rig.Install(99, GunType.Vulcan, WeaponSlot.Primary);
            Assert.AreEqual(GroundWeapon.Laser, rig.Ship.Ground.Current);
        }

        [Test]
        public void ADefaultShipLandingCostsTheShotgunSecondaryFour()
        {
            var rig = new PlayerRig();
            int key = 0;
            foreach (var mount in new ShipLoadout().Guns) rig.Install(++key, mount.Type, mount.Slot);
            rig.Do(new ResetGroundLoadout(rig.Id));
            rig.Do(new ResetAirLoadout(rig.Id));
            rig.Landed(); // set_ammo_cost

            Assert.AreEqual(4, rig.Ammo.Cost(AmmoPool.GroundSecondary));
            Assert.AreEqual(1, rig.Ammo.Cost(AmmoPool.GroundPrimary));
            Assert.AreEqual(120, rig.Ammo.Max(AmmoPool.GroundPrimary), "the vulcan as ground primary");
        }

        [Test]
        public void ALoadoutThatCannotFireAsTheOriginalIsRefused()
        {
            Assert.IsNotEmpty(Make(new GunMount(GunType.Vulcan, WeaponSlot.Secondary)).Validate());
            Assert.IsNotEmpty(Make(new GunMount(GunType.MineLauncher, WeaponSlot.Primary)).Validate());
            Assert.IsNotEmpty(Make(new GunMount(GunType.Laser, WeaponSlot.Primary), new GunMount(GunType.Laser, WeaponSlot.Primary)).Validate());
            Assert.IsNotEmpty(Make(new GunMount(GunType.Shotgun, WeaponSlot.Secondary), new GunMount(GunType.Flamer, WeaponSlot.Secondary)).Validate());
            Assert.IsNotEmpty(Make(new GunMount((GunType)99, WeaponSlot.Primary)).Validate());
            Assert.IsNotEmpty(new ShipLoadout { Guns = null }.Validate());
            Assert.Throws<System.InvalidOperationException>(() => Make(new GunMount(GunType.Vulcan, WeaponSlot.Secondary)).EnsureValid());

            CollectionAssert.IsEmpty(Make(new GunMount(GunType.Shotgun, WeaponSlot.Primary), new GunMount(GunType.Shotgun, WeaponSlot.Secondary)).Validate());
        }

        static ShipLoadout Make(params GunMount[] guns) => new ShipLoadout { Guns = guns };
    }
}
