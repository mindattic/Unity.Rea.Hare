using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.Core
{
    /// <summary>next_ground_weapon and next_air_weapon (the weapon cycles) and fire_secondary's air weapons, run outside Unity.</summary>
    public class LoadoutTests
    {
        PlayerRig rig;

        [SetUp]
        public void SetUp() => rig = new PlayerRig();

        // ---- air loadout ----

        void AirReady()
        {
            rig.Do(new ResetAirLoadout(rig.Id));
            rig.Do(new SetAim(rig.Id, 0f));
        }

        AirLoadout Air => rig.Ship.Air;

        [Test]
        public void NextAirWeaponCyclesLaserRailgunRocketAndWraps()
        {
            AirReady();
            Assert.AreEqual(AirWeapon.Laser, Air.Current);
            Assert.IsTrue(rig.Do(new NextAirWeapon(rig.Id)).Accepted);
            Assert.AreEqual(AirWeapon.Railgun, Air.Current);
            Assert.AreEqual(AirWeapon.Railgun, rig.Seen<AirWeaponSwitched>().Last().Weapon);
            rig.Do(new NextAirWeapon(rig.Id));
            Assert.AreEqual(AirWeapon.Rocket, Air.Current);
            rig.Do(new NextAirWeapon(rig.Id));
            Assert.AreEqual(AirWeapon.Laser, Air.Current);
        }

        [Test]
        public void CyclingSkipsLockedWeaponsAndStaysPutWithOnlyOne()
        {
            AirReady();
            rig.Do(new LockAirWeapon(rig.Id, AirWeapon.Railgun));
            rig.Do(new NextAirWeapon(rig.Id));
            Assert.AreEqual(AirWeapon.Rocket, Air.Current);

            rig.Do(new LockAirWeapon(rig.Id, AirWeapon.Laser));
            Assert.IsFalse(rig.Do(new NextAirWeapon(rig.Id)).Accepted);
            Assert.AreEqual(AirWeapon.Rocket, Air.Current);

            rig.Do(new UnlockAirWeapon(rig.Id, AirWeapon.Railgun));
            Assert.IsTrue(rig.Do(new NextAirWeapon(rig.Id)).Accepted);
            Assert.AreEqual(AirWeapon.Railgun, Air.Current);
        }

        [Test]
        public void TheAirCycleIsData()
        {
            var table = new WeaponTable { AirCycle = new[] { AirWeapon.Rocket, AirWeapon.Laser } };
            var custom = new PlayerRig(table: table);
            custom.Do(new ResetAirLoadout(custom.Id));
            Assert.AreEqual(AirWeapon.Rocket, custom.Ship.Air.Current);
            custom.Do(new NextAirWeapon(custom.Id));
            Assert.AreEqual(AirWeapon.Laser, custom.Ship.Air.Current);
            Assert.IsFalse(custom.Ship.Air.IsUnlocked(AirWeapon.Railgun));
        }

        [Test]
        public void LaserStaysOnWhileHeldAndRearmsOnRelease()
        {
            AirReady();
            rig.Do(new SetAirTrigger(rig.Id, true));
            rig.Do(new TickAir(rig.Id, PlayerRig.Step));
            Assert.IsTrue(Air.IsLaserOn);
            Assert.IsFalse(Air.CanShoot);
            Assert.AreEqual(149, rig.Ammo.Current(AmmoPool.GroundPrimary), "one shot per trigger pull");

            rig.RunAir(1f);
            Assert.AreEqual(1, rig.Seen<AirWeaponFired>().Count(), "no second beam while held");
            Assert.AreEqual(149, rig.Ammo.Current(AmmoPool.GroundPrimary));

            rig.Do(new SetAirTrigger(rig.Id, false));
            Assert.AreEqual(1, rig.Seen<AirLaserStopped>().Count(), "release destroys obj_laser");
            Assert.IsFalse(Air.IsLaserOn);
            Assert.IsTrue(Air.CanShoot);
        }

        [Test]
        public void RailgunRearmsAfter240Steps()
        {
            AirReady();
            rig.Do(new NextAirWeapon(rig.Id));
            rig.Do(new SetAirTrigger(rig.Id, true));
            Assert.IsTrue(rig.Do(new FireAir(rig.Id)).Accepted);
            Assert.AreEqual(AirWeapon.Railgun, rig.Seen<AirWeaponFired>().Single().Weapon);

            Assert.IsFalse(rig.Do(new FireAir(rig.Id)).Accepted);
            rig.Do(new SetAirTrigger(rig.Id, false));
            Assert.IsFalse(Air.CanShoot, "releasing does not re-arm the railgun");
            rig.RunAir(2f - 3 * PlayerRig.Step);
            Assert.IsFalse(Air.CanShoot, "still re-arming just before 240 steps");
            rig.RunAir(4 * PlayerRig.Step);
            Assert.IsTrue(Air.CanShoot);
        }

        [Test]
        public void RocketsFireEvery25Steps()
        {
            AirReady();
            rig.Do(new NextAirWeapon(rig.Id));
            rig.Do(new NextAirWeapon(rig.Id));
            rig.Do(new SetAirTrigger(rig.Id, true));
            rig.Do(new TickAir(rig.Id, PlayerRig.Step));
            Assert.AreEqual(1, rig.Seen<AirWeaponFired>().Count());

            rig.RunAir(20 * PlayerRig.Step);
            Assert.AreEqual(1, rig.Seen<AirWeaponFired>().Count());
            rig.RunAir(6 * PlayerRig.Step);
            Assert.AreEqual(2, rig.Seen<AirWeaponFired>().Count());
        }

        [Test]
        public void RocketsRunDryAt20AndRegenerate4PerSecondWithTriggerUp()
        {
            AirReady();
            rig.Do(new NextAirWeapon(rig.Id));
            rig.Do(new NextAirWeapon(rig.Id));

            rig.Do(new SetAirTrigger(rig.Id, true));
            rig.RunAir(6f);
            Assert.AreEqual(0, rig.Ammo.Current(AmmoPool.AirPrimary));
            Assert.Greater(rig.Seen<AirWeaponDryFired>().Count(), 0);

            rig.Do(new SetAirTrigger(rig.Id, false));
            for (float t = 0f; t < 1f + PlayerRig.Step; t += PlayerRig.Step)
            {
                rig.Do(new TickAir(rig.Id, PlayerRig.Step));
                rig.Do(new TickAmmo(rig.Id, PlayerRig.Step));
            }
            Assert.AreEqual(4, rig.Ammo.Current(AmmoPool.AirPrimary));
        }

        [Test]
        public void AirWeaponsHoldFireWhileShieldingOrDead()
        {
            AirReady();
            rig.Landed();
            Assert.IsTrue(rig.Do(new RaiseShield(rig.Id)).Accepted);
            Assert.IsFalse(rig.Do(new FireAir(rig.Id)).Accepted);

            rig.Do(new DropShield(rig.Id));
            rig.Do(new HaltShip(rig.Id));
            Assert.IsFalse(rig.Do(new FireAir(rig.Id)).Accepted);
            Assert.AreEqual(150, rig.Ammo.Current(AmmoPool.GroundPrimary));
        }

        [Test]
        public void SwitchingWeaponsRearmsAndDropsTheLaser()
        {
            AirReady();
            rig.Do(new SetAirTrigger(rig.Id, true));
            rig.Do(new TickAir(rig.Id, PlayerRig.Step));
            Assert.IsTrue(Air.IsLaserOn);

            rig.Do(new NextAirWeapon(rig.Id)); // railgun
            Assert.IsFalse(Air.IsLaserOn);
            Assert.IsTrue(rig.Do(new FireAir(rig.Id)).Accepted);
            Assert.IsFalse(Air.CanShoot);

            rig.Do(new NextAirWeapon(rig.Id)); // rocket: next_air_weapon sets can_shoot = true
            Assert.IsTrue(Air.CanShoot);
        }

        [Test]
        public void ADyingShipSwitchesTheLaserOff()
        {
            AirReady();
            rig.Do(new SetAirTrigger(rig.Id, true));
            rig.Do(new TickAir(rig.Id, PlayerRig.Step));
            rig.Do(new HaltShip(rig.Id));
            rig.Do(new TickAir(rig.Id, PlayerRig.Step));
            Assert.IsFalse(Air.IsLaserOn);
            Assert.AreEqual(1, rig.Seen<AirLaserStopped>().Count());
        }

        // ---- ground loadout ----

        void GroundShip()
        {
            rig.Install(1, GunType.Vulcan, WeaponSlot.Primary);
            rig.Install(2, GunType.Flamer, WeaponSlot.Primary);
            rig.Install(3, GunType.Shotgun, WeaponSlot.Primary);
            rig.Install(4, GunType.Shotgun, WeaponSlot.Secondary); // the default Ctrl + left mouse shotgun
            rig.Do(new ResetGroundLoadout(rig.Id));
            rig.Landed();
        }

        GroundLoadout Ground => rig.Ship.Ground;

        [Test]
        public void NextGroundWeaponCyclesVulcanShotgunFlamerAndWraps()
        {
            GroundShip();

            Assert.AreEqual(GroundWeapon.Vulcan, Ground.Current);
            Assert.AreEqual(3, Ground.Weapons.Count, "the secondary shotgun is not in the cycle");
            Assert.IsFalse(Ground.Has(GroundWeapon.Laser));

            Assert.IsTrue(rig.Do(new NextGroundWeapon(rig.Id)).Accepted);
            Assert.AreEqual(GroundWeapon.Shotgun, Ground.Current);
            Assert.IsTrue(rig.Do(new NextGroundWeapon(rig.Id)).Accepted);
            Assert.AreEqual(GroundWeapon.Flamer, Ground.Current);
            Assert.IsTrue(rig.Do(new NextGroundWeapon(rig.Id)).Accepted);
            Assert.AreEqual(GroundWeapon.Vulcan, Ground.Current);
            CollectionAssert.AreEqual(
                new[] { GroundWeapon.Shotgun, GroundWeapon.Flamer, GroundWeapon.Vulcan },
                rig.Seen<GroundWeaponSwitched>().Select(s => s.Weapon).ToArray());
        }

        [Test]
        public void TheGroundCycleIsData()
        {
            var table = new WeaponTable { GroundCycle = new[] { GroundWeapon.Flamer, GroundWeapon.Vulcan, GroundWeapon.Shotgun } };
            var custom = new PlayerRig(table: table);
            custom.Install(1, GunType.Vulcan, WeaponSlot.Primary);
            custom.Install(2, GunType.Flamer, WeaponSlot.Primary);
            custom.Do(new ResetGroundLoadout(custom.Id));
            Assert.AreEqual(GroundWeapon.Flamer, custom.Ship.Ground.Current);
            custom.Do(new NextGroundWeapon(custom.Id));
            Assert.AreEqual(GroundWeapon.Vulcan, custom.Ship.Ground.Current);
        }

        [Test]
        public void SwitchingRearmsTheNewWeaponAndReleasesTheOldTrigger()
        {
            GroundShip();
            rig.Do(new EquipGroundWeapon(rig.Id, GroundWeapon.Shotgun));
            Assert.IsTrue(rig.Do(new FireGun(rig.Id, 3)).Accepted);
            Assert.Greater(rig.Ship.Guns.Find(3).Cooldown, 0f);

            rig.Do(new EquipGroundWeapon(rig.Id, GroundWeapon.Vulcan));
            rig.Do(new PullTrigger(rig.Id, 1));
            rig.RunGun(1, 1);
            Assert.IsTrue(rig.Ship.Guns.Find(1).IsSpinning);

            rig.Do(new NextGroundWeapon(rig.Id)); // back to the shotgun
            Assert.IsFalse(rig.Ship.Guns.Find(1).TriggerHeld);
            Assert.IsFalse(rig.Ship.Guns.Find(1).IsSpinning, "stop_vulcan_sfx on the way out");
            Assert.AreEqual(0f, rig.Ship.Guns.Find(3).Cooldown, "next_ground_weapon: can_shoot = true");
            Assert.IsTrue(rig.Do(new FireGun(rig.Id, 3)).Accepted);
        }

        [Test]
        public void NextGroundWeaponDoesNothingWithASingleGroundPrimary()
        {
            rig.Install(1, GunType.Vulcan, WeaponSlot.Primary);
            rig.Do(new ResetGroundLoadout(rig.Id));

            Assert.AreEqual(GroundWeapon.Vulcan, Ground.Current);
            Assert.IsFalse(rig.Do(new NextGroundWeapon(rig.Id)).Accepted);
            Assert.AreEqual(GroundWeapon.Vulcan, Ground.Current);
        }

        [Test]
        public void RefreshPicksUpAGroundPrimaryAddedLater()
        {
            rig.Install(1, GunType.Vulcan, WeaponSlot.Primary);
            rig.Do(new ResetGroundLoadout(rig.Id));

            rig.Install(2, GunType.Flamer, WeaponSlot.Primary);
            rig.Do(new RefreshGroundLoadout(rig.Id));
            Assert.AreEqual(GroundWeapon.Vulcan, Ground.Current);
            Assert.IsTrue(rig.Do(new NextGroundWeapon(rig.Id)).Accepted);
            Assert.AreEqual(GroundWeapon.Flamer, Ground.Current);
        }

        [Test]
        public void RefreshKeepsTheEquippedWeaponOrFallsBackToTheFirst()
        {
            GroundShip();
            rig.Do(new EquipGroundWeapon(rig.Id, GroundWeapon.Flamer));
            rig.Do(new RefreshGroundLoadout(rig.Id));
            Assert.AreEqual(GroundWeapon.Flamer, Ground.Current);

            rig.Do(new RemoveGun(rig.Id, 2));
            rig.Do(new RefreshGroundLoadout(rig.Id));
            Assert.AreEqual(GroundWeapon.Vulcan, Ground.Current);
        }

        [Test]
        public void MovingAGunToAnotherSlotChangesTheCycle()
        {
            GroundShip();
            rig.Install(3, GunType.Shotgun, WeaponSlot.Secondary);
            rig.Do(new RefreshGroundLoadout(rig.Id));
            CollectionAssert.AreEqual(new[] { GroundWeapon.Vulcan, GroundWeapon.Flamer }, Ground.Weapons.ToArray());
        }

        [Test]
        public void TheLoadoutFollowsGunsInstalledAfterItWhateverTheOrder()
        {
            rig.Do(new ResetGroundLoadout(rig.Id)); // the loadout wakes before the guns
            rig.Install(1, GunType.Vulcan, WeaponSlot.Primary);
            rig.Install(2, GunType.Flamer, WeaponSlot.Primary);

            Assert.AreEqual(GroundWeapon.Vulcan, Ground.Current);
            CollectionAssert.AreEqual(new[] { GroundWeapon.Vulcan, GroundWeapon.Flamer }, Ground.Weapons.ToArray());
            Assert.AreEqual(2, rig.Seen<GroundLoadoutChanged>().Count());
        }
    }
}
