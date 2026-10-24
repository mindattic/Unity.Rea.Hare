using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.Core
{
    /// <summary>next_ground_weapon and next_air_weapon (the weapon cycles) the air primary (rocket and mine) and the perched laser and railgun, run outside Unity.</summary>
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
        public void NextAirWeaponTogglesRocketAndMine()
        {
            AirReady();
            Assert.AreEqual(AirWeapon.Rocket, Air.Current);
            Assert.IsTrue(rig.Do(new NextAirWeapon(rig.Id)).Accepted);
            Assert.AreEqual(AirWeapon.Mine, Air.Current);
            Assert.AreEqual(AirWeapon.Mine, rig.Seen<AirWeaponSwitched>().Last().Weapon);
            rig.Do(new NextAirWeapon(rig.Id));
            Assert.AreEqual(AirWeapon.Rocket, Air.Current);
        }

        [Test]
        public void CyclingStaysPutWithOnlyOneAirWeapon()
        {
            AirReady();
            rig.Do(new LockAirWeapon(rig.Id, AirWeapon.Mine));
            Assert.IsFalse(rig.Do(new NextAirWeapon(rig.Id)).Accepted);
            Assert.AreEqual(AirWeapon.Rocket, Air.Current);

            rig.Do(new UnlockAirWeapon(rig.Id, AirWeapon.Mine));
            Assert.IsTrue(rig.Do(new NextAirWeapon(rig.Id)).Accepted);
            Assert.AreEqual(AirWeapon.Mine, Air.Current);
        }

        [Test]
        public void TheAirCycleIsData()
        {
            var table = new WeaponTable { AirCycle = new[] { AirWeapon.Mine, AirWeapon.Rocket } };
            var custom = new PlayerRig(table: table);
            custom.Do(new ResetAirLoadout(custom.Id));
            Assert.AreEqual(AirWeapon.Mine, custom.Ship.Air.Current);
            custom.Do(new NextAirWeapon(custom.Id));
            Assert.AreEqual(AirWeapon.Rocket, custom.Ship.Air.Current);
        }

        [Test]
        public void RocketsFireEvery25Steps()
        {
            AirReady();
            rig.Do(new SetAirTrigger(rig.Id, true));
            rig.Do(new TickAir(rig.Id, PlayerRig.Step));
            Assert.AreEqual(AirWeapon.Rocket, rig.Seen<AirWeaponFired>().Single().Weapon);

            rig.RunAir(20 * PlayerRig.Step);
            Assert.AreEqual(1, rig.Seen<AirWeaponFired>().Count());
            rig.RunAir(6 * PlayerRig.Step);
            Assert.AreEqual(2, rig.Seen<AirWeaponFired>().Count());
        }

        [Test]
        public void TheAirMineSpendsTheAirPrimaryAndRearmsAfterTheMinesDelay()
        {
            AirReady();
            rig.Do(new NextAirWeapon(rig.Id));
            rig.Do(new SetAirTrigger(rig.Id, true));
            Assert.IsTrue(rig.Do(new FireAir(rig.Id)).Accepted);
            Assert.AreEqual(AirWeapon.Mine, rig.Seen<AirWeaponFired>().Single().Weapon);
            Assert.AreEqual(19, rig.Ammo.Current(AmmoPool.AirPrimary));
            Assert.AreEqual(8, rig.Ammo.Current(AmmoPool.AirSecondary));

            Assert.IsFalse(rig.Do(new FireAir(rig.Id)).Accepted);
            rig.Do(new SetAirTrigger(rig.Id, false));
            rig.RunAir(28 * PlayerRig.Step);
            Assert.IsFalse(Air.CanShoot);
            rig.RunAir(3 * PlayerRig.Step);
            Assert.IsTrue(Air.CanShoot);
        }

        [Test]
        public void RocketsRunDryAt20AndRegenerate4PerSecondWithTriggerUp()
        {
            AirReady();

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
        public void AirWeaponsHoldFireWhilePerchedShieldingOrDead()
        {
            AirReady();
            rig.Landed();
            Assert.IsFalse(rig.Do(new FireAir(rig.Id)).Accepted, "perched: left mouse fires the ground weapon");
            Assert.IsTrue(rig.Do(new RaiseShield(rig.Id)).Accepted);
            Assert.IsFalse(rig.Do(new FireAir(rig.Id)).Accepted);

            rig.Do(new DropShield(rig.Id));
            rig.Do(new HaltShip(rig.Id));
            Assert.IsFalse(rig.Do(new FireAir(rig.Id)).Accepted);
            Assert.AreEqual(20, rig.Ammo.Current(AmmoPool.AirPrimary));
        }

        [Test]
        public void SwitchingAirWeaponsRearms()
        {
            AirReady();
            rig.Do(new FireAir(rig.Id));
            Assert.IsFalse(Air.CanShoot);

            rig.Do(new NextAirWeapon(rig.Id)); // next_air_weapon sets can_shoot = true
            Assert.IsTrue(Air.CanShoot);
        }

        // ---- the laser and railgun are perched primaries on Q's cycle ----

        const int LaserKey = 11;
        const int RailKey = 12;

        void PerchedWith(params GunType[] types)
        {
            int key = 1;
            foreach (var type in types) rig.Install(key++, type, WeaponSlot.Primary);
            rig.Do(new ResetGroundLoadout(rig.Id));
            rig.Landed();
        }

        [Test]
        public void QCyclesVulcanLaserRailgunShotgunFlamer()
        {
            PerchedWith(GunType.Vulcan, GunType.Laser, GunType.Railgun, GunType.Shotgun, GunType.Flamer);
            Assert.AreEqual(GroundWeapon.Vulcan, Ground.Current);
            for (int i = 0; i < 5; i++) rig.Do(new NextGroundWeapon(rig.Id));
            CollectionAssert.AreEqual(
                new[] { GroundWeapon.Laser, GroundWeapon.Railgun, GroundWeapon.Shotgun, GroundWeapon.Flamer, GroundWeapon.Vulcan },
                rig.Seen<GroundWeaponSwitched>().Select(s => s.Weapon).ToArray());
        }

        [Test]
        public void TheLaserStaysOnWhileHeldAndRearmsOnRelease()
        {
            rig.Install(LaserKey, GunType.Laser, WeaponSlot.Primary);
            rig.Landed();
            rig.Do(new PullTrigger(rig.Id, LaserKey));
            Assert.IsTrue(rig.Do(new FireGun(rig.Id, LaserKey)).Accepted);
            Assert.IsTrue(rig.Ship.Guns.Find(LaserKey).IsBeamOn);
            Assert.AreEqual(149, rig.Ammo.Current(AmmoPool.GroundPrimary), "one shot per trigger pull");

            for (int i = 0; i < 240; i++) rig.Do(new TickGun(rig.Id, LaserKey, PlayerRig.Step));
            Assert.IsFalse(rig.Do(new FireGun(rig.Id, LaserKey)).Accepted, "no second beam while held");
            Assert.AreEqual(0, rig.Seen<GunDryFired>().Count());
            Assert.AreEqual(149, rig.Ammo.Current(AmmoPool.GroundPrimary));

            rig.Do(new ReleaseTrigger(rig.Id, LaserKey));
            Assert.AreEqual(1, rig.Seen<GunBeamStopped>().Count(), "release destroys obj_laser");
            Assert.IsFalse(rig.Ship.Guns.Find(LaserKey).IsBeamOn);
            Assert.IsTrue(rig.Ship.Guns.Find(LaserKey).CanShoot);
        }

        [Test]
        public void ALaserFiredWithoutAPullStillRearmsOnRelease()
        {
            rig.Install(LaserKey, GunType.Laser, WeaponSlot.Primary);
            rig.Landed();
            Assert.IsTrue(rig.Do(new FireGun(rig.Id, LaserKey)).Accepted);
            Assert.IsFalse(rig.Do(new FireGun(rig.Id, LaserKey)).Accepted);

            rig.Do(new ReleaseTrigger(rig.Id, LaserKey));
            Assert.IsFalse(rig.Ship.Guns.Find(LaserKey).IsBeamOn);
            Assert.IsTrue(rig.Do(new FireGun(rig.Id, LaserKey)).Accepted);
        }

        [Test]
        public void SwitchingAwayFromTheLaserPutsTheBeamOut()
        {
            PerchedWith(GunType.Vulcan, GunType.Laser);
            rig.Do(new NextGroundWeapon(rig.Id)); // laser
            rig.Do(new PullTrigger(rig.Id, 2));
            rig.Do(new FireGun(rig.Id, 2));
            Assert.IsTrue(rig.Ship.Guns.Find(2).IsBeamOn);

            rig.Do(new NextGroundWeapon(rig.Id));
            Assert.IsFalse(rig.Ship.Guns.Find(2).IsBeamOn);
            Assert.AreEqual(1, rig.Seen<GunBeamStopped>().Count());
        }

        [Test]
        public void AShieldSwitchesTheLaserOff()
        {
            rig.Install(LaserKey, GunType.Laser, WeaponSlot.Primary);
            rig.Landed();
            rig.Do(new PullTrigger(rig.Id, LaserKey));
            rig.Do(new FireGun(rig.Id, LaserKey));
            Assert.IsTrue(rig.Do(new RaiseShield(rig.Id)).Accepted);
            rig.Do(new TickGun(rig.Id, LaserKey, PlayerRig.Step));
            Assert.IsFalse(rig.Ship.Guns.Find(LaserKey).IsBeamOn);
            Assert.AreEqual(1, rig.Seen<GunBeamStopped>().Count());
        }

        [Test]
        public void TheRailgunRearmsAfter240Steps()
        {
            rig.Install(RailKey, GunType.Railgun, WeaponSlot.Primary);
            rig.Landed();
            Assert.IsTrue(rig.Do(new FireGun(rig.Id, RailKey)).Accepted);
            Assert.AreEqual(149, rig.Ammo.Current(AmmoPool.GroundPrimary));
            Assert.IsFalse(rig.Do(new FireGun(rig.Id, RailKey)).Accepted);

            rig.RunGun(RailKey, 238);
            Assert.IsFalse(rig.Ship.Guns.Find(RailKey).CanShoot, "still re-arming just before 240 steps");
            rig.RunGun(RailKey, 3);
            Assert.IsTrue(rig.Ship.Guns.Find(RailKey).CanShoot);
        }

        [Test]
        public void TheLaserAndRailgunFireOnlyPerched()
        {
            rig.Install(LaserKey, GunType.Laser, WeaponSlot.Primary);
            rig.Install(RailKey, GunType.Railgun, WeaponSlot.Primary); // the ship starts in flight
            Assert.IsFalse(rig.Do(new FireGun(rig.Id, LaserKey)).Accepted);
            Assert.IsFalse(rig.Do(new FireGun(rig.Id, RailKey)).Accepted);
            Assert.AreEqual(150, rig.Ammo.Current(AmmoPool.GroundPrimary));
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
