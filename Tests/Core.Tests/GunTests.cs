using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.Core
{
    /// <summary>The vulcan (fire_primary's weapon_vulcan branch), shotgun, flamer and mine launcher rules, run outside Unity.</summary>
    public class GunTests
    {
        const float Step = PlayerRig.Step;

        PlayerRig rig;
        int vulcan;

        [SetUp]
        public void SetUp()
        {
            rig = new PlayerRig();
            vulcan = rig.Install(1, GunType.Vulcan, WeaponSlot.Primary);
            rig.Landed(); // the vulcan is a ground weapon: fire_primary's state_idle branch
        }

        List<VulcanSound> Sounds => rig.Seen<VulcanCue>().Select(c => c.Sound).ToList();
        CommandResult Fire(int key) => rig.Do(new FireGun(rig.Id, key));
        void Press(int key) => rig.Do(new PullTrigger(rig.Id, key));
        void Release(int key) => rig.Do(new ReleaseTrigger(rig.Id, key));
        Gun Gun(int key) => rig.Ship.Guns.Find(key);

        // ---- vulcan ----

        [Test]
        public void StartsWithCreatesRoundsAndLandingCapsTheVulcanAt120()
        {
            // obj_player Create: ground_ammo_primary = 150; set_ammo_cost on landing lowers only the max.
            Assert.AreEqual(150, rig.Ammo.Current(AmmoPool.GroundPrimary));
            Assert.AreEqual(120, rig.Ammo.Max(AmmoPool.GroundPrimary));
            Assert.IsTrue(Gun(vulcan).CanShoot);
            Assert.IsFalse(Gun(vulcan).IsSpinning);
        }

        [Test]
        public void HoldingTheTriggerFiresOnTheFirstTick()
        {
            Press(vulcan);
            var result = rig.Do(new TickGun(rig.Id, vulcan, Step));

            Assert.AreEqual(1, result.Events.OfType<GunFired>().Count());
            Assert.AreEqual(vulcan, result.Find<GunFired>().Key);
            Assert.AreEqual(149, rig.Ammo.Current(AmmoPool.GroundPrimary), "subtract_ground_ammo: 1 round per shot");
            Assert.IsFalse(Gun(vulcan).CanShoot);
        }

        [Test]
        public void TheVulcanFiresAtGmlHaresFixedNineSteps()
        {
            Press(vulcan);
            rig.RunGun(vulcan, 1); // first shot; next waits fire_rate = 9 steps

            var gaps = new List<int>();
            int sinceLast = 0;
            while (gaps.Count < 10)
            {
                sinceLast++;
                if (rig.RunGun(vulcan, 1) == 1) { gaps.Add(sinceLast); sinceLast = 0; }
            }

            CollectionAssert.AreEqual(new[] { 9, 9, 9, 9, 9, 9, 9, 9, 9, 9 }, gaps);
        }

        [Test]
        public void TheFireIntervalIsDataSoTheEarlierRabbitRampIsOneLine()
        {
            var table = new WeaponTable { VulcanBaseRateSteps = 30 };
            var fixedRate = new PlayerRig(table: table);
            int gun = fixedRate.Install(1, GunType.Vulcan, WeaponSlot.Primary);
            fixedRate.Landed();
            fixedRate.Do(new PullTrigger(fixedRate.Id, gun));
            fixedRate.RunGun(gun, 1);

            var gaps = new List<int>();
            int sinceLast = 0;
            while (gaps.Count < 4)
            {
                sinceLast++;
                if (fixedRate.RunGun(gun, 1) == 1) { gaps.Add(sinceLast); sinceLast = 0; }
            }
            CollectionAssert.AreEqual(new[] { 30, 27, 24, 21 }, gaps);
        }

        [Test]
        public void SpinUpPlaysOnceThenTheLoopRunsUntilRelease()
        {
            Press(vulcan);
            rig.RunGun(vulcan, 200);

            Assert.IsTrue(Gun(vulcan).IsSpinning);
            Assert.IsTrue(Gun(vulcan).IsLoopPlaying);
            CollectionAssert.AreEqual(new[] { VulcanSound.SpinUp, VulcanSound.LoopStart }, Sounds);

            Release(vulcan);

            Assert.IsFalse(Gun(vulcan).IsSpinning);
            Assert.IsFalse(Gun(vulcan).IsLoopPlaying);
            CollectionAssert.AreEqual(
                new[] { VulcanSound.SpinUp, VulcanSound.LoopStart, VulcanSound.LoopStop, VulcanSound.SpinDown }, Sounds);
        }

        [Test]
        public void ReleasingResetsTheFireRateSoTheNextBurstSpinsUpAgain()
        {
            Press(vulcan);
            rig.RunGun(vulcan, 300);
            Assert.AreEqual(rig.Ship.Table.VulcanMinRateSteps, Gun(vulcan).FireRateSteps);

            Release(vulcan);
            Assert.AreEqual(rig.Ship.Table.VulcanBaseRateSteps, Gun(vulcan).FireRateSteps);
            rig.RunGun(vulcan, 20); // let the last cooldown run out

            rig.Events.Clear();
            Press(vulcan);
            rig.RunGun(vulcan, 1);
            CollectionAssert.AreEqual(new[] { VulcanSound.SpinUp, VulcanSound.LoopStart }, Sounds);
        }

        [Test]
        public void ReleasingWithoutHavingFiredDoesNotPlaySpinDown()
        {
            Release(vulcan);
            CollectionAssert.IsEmpty(Sounds);
        }

        [Test]
        public void RunningDryClicksAndSpinsDownInsteadOfFiring()
        {
            Press(vulcan);
            int shots = rig.RunGun(vulcan, 150 * 30);

            Assert.AreEqual(150, shots, "one bullet per round, no more");
            Assert.AreEqual(0, rig.Ammo.Current(AmmoPool.GroundPrimary));
            Assert.IsFalse(Gun(vulcan).IsSpinning);
            Assert.IsFalse(Gun(vulcan).IsLoopPlaying);
            Assert.IsTrue(rig.Seen<GunDryFired>().Any(), "sfx_empty_clip");
            CollectionAssert.Contains(Sounds, VulcanSound.SpinDown);
        }

        [Test]
        public void AmmoRegeneratesTenPerSecondOnlyAfterTheTriggerIsReleased()
        {
            rig.Do(new SetAmmo(rig.Id, AmmoPool.GroundPrimary, 100));

            Press(vulcan);
            rig.RunGun(vulcan, 250);
            int afterBurst = rig.Ammo.Current(AmmoPool.GroundPrimary);
            Assert.Less(afterBurst, 100);

            Release(vulcan);
            rig.Do(new TickAmmo(rig.Id, 0.9f));
            Assert.AreEqual(afterBurst, rig.Ammo.Current(AmmoPool.GroundPrimary), "nothing before ammo_regen_delay");
            rig.Do(new TickAmmo(rig.Id, 0.11f));
            Assert.AreEqual(afterBurst + 10, rig.Ammo.Current(AmmoPool.GroundPrimary));
            var reloads = rig.Seen<AmmoReloaded>().Where(r => r.Pool == AmmoPool.GroundPrimary).Select(r => r.Full).ToList();
            CollectionAssert.AreEqual(new[] { false }, reloads, "sfx_reload_half");

            rig.Do(new TickAmmo(rig.Id, 10f));
            Assert.AreEqual(120, rig.Ammo.Current(AmmoPool.GroundPrimary), "capped at the maximum");
            Assert.IsTrue(rig.Seen<AmmoReloaded>().Last(r => r.Pool == AmmoPool.GroundPrimary).Full, "sfx_reload_full");
        }

        [Test]
        public void HoldingTheTriggerStopsRegeneration()
        {
            rig.Do(new SetAmmo(rig.Id, AmmoPool.GroundPrimary, 100));
            Press(vulcan);
            rig.RunGun(vulcan, 250);
            Release(vulcan);
            rig.Do(new TickAmmo(rig.Id, 0.5f));

            rig.Do(new RaiseShield(rig.Id)); // fire is held back, but the trigger still blocks regen
            Press(vulcan);
            int held = rig.Ammo.Current(AmmoPool.GroundPrimary);
            rig.Do(new TickAmmo(rig.Id, 5f));
            Assert.AreEqual(held, rig.Ammo.Current(AmmoPool.GroundPrimary));
        }

        [Test]
        public void DoesNotFireInFlightWhileShieldingOrWhileDead()
        {
            rig.Do(new Leap(rig.Id, rig.Flight.Direction));
            Assert.AreEqual(PlayerFlightState.Moving, rig.Flight.State);
            Press(vulcan);
            Assert.AreEqual(0, rig.RunGun(vulcan, 60), "in the air the air loadout fires instead (UH-T9)");

            rig.Landed();
            Assert.IsTrue(rig.Do(new RaiseShield(rig.Id)).Accepted);
            Assert.AreEqual(0, rig.RunGun(vulcan, 60), "fire_primary: !player.shielding");

            rig.Do(new DropShield(rig.Id));
            rig.Do(new HaltShip(rig.Id));
            Assert.AreEqual(0, rig.RunGun(vulcan, 60));
            Assert.AreEqual(150, rig.Ammo.Current(AmmoPool.GroundPrimary));
        }

        [Test]
        public void DyingMidBurstSpinsTheBarrelDown()
        {
            Press(vulcan);
            rig.RunGun(vulcan, 40);
            Assert.IsTrue(Gun(vulcan).IsSpinning);

            rig.Do(new HaltShip(rig.Id));
            rig.RunGun(vulcan, 1);

            Assert.IsFalse(Gun(vulcan).IsSpinning);
            Assert.IsFalse(Gun(vulcan).IsLoopPlaying);
            Assert.AreEqual(VulcanSound.SpinDown, Sounds.Last());
        }

        // ---- shotgun ----

        [Test]
        public void ShotgunDoesNotFireWhileFlying()
        {
            int shotgun = rig.Install(2, GunType.Shotgun, WeaponSlot.Secondary);
            Assert.IsTrue(rig.Do(new Leap(rig.Id, rig.Flight.Direction)).Accepted);

            Assert.IsFalse(Fire(shotgun).Accepted);
            Assert.AreEqual(12, rig.Ammo.Current(AmmoPool.GroundSecondary));
        }

        [Test]
        public void ShotgunDoesNotFireWhileShieldingOrDead()
        {
            int shotgun = rig.Install(2, GunType.Shotgun, WeaponSlot.Secondary);

            Assert.IsTrue(rig.Do(new RaiseShield(rig.Id)).Accepted);
            Assert.IsFalse(Fire(shotgun).Accepted);
            rig.Do(new DropShield(rig.Id));

            rig.Do(new HaltShip(rig.Id));
            Assert.IsFalse(Fire(shotgun).Accepted);
            Assert.AreEqual(0, rig.Seen<GunFired>().Count());
        }

        [Test]
        public void ShotgunReloadsForOneSecondBetweenBlasts()
        {
            int shotgun = rig.Install(2, GunType.Shotgun, WeaponSlot.Secondary);
            Assert.IsTrue(Fire(shotgun).Accepted);
            Assert.IsFalse(Fire(shotgun).Accepted);

            rig.Do(new TickGun(rig.Id, shotgun, 0.9f));
            Assert.IsFalse(Fire(shotgun).Accepted);
            rig.Do(new TickGun(rig.Id, shotgun, 0.11f));
            Assert.IsTrue(Fire(shotgun).Accepted);
        }

        [Test]
        public void ShotgunSpendsFourOfTwelveAndRunsDryAfterThreeBlasts()
        {
            int shotgun = rig.Install(2, GunType.Shotgun, WeaponSlot.Secondary);
            rig.Do(new Land(rig.Id)); // set_ammo_cost with the shotgun in the secondary slot
            Assert.AreEqual(12, rig.Ammo.Current(AmmoPool.GroundSecondary));

            Press(shotgun); // GML: no ammo regen while the mouse button is down
            for (int i = 0; i < 3; i++)
            {
                Assert.IsTrue(Fire(shotgun).Accepted, $"blast {i + 1}");
                Assert.AreEqual(12 - 4 * (i + 1), rig.Ammo.Current(AmmoPool.GroundSecondary));
                rig.Do(new TickGun(rig.Id, shotgun, 1f));
            }

            Assert.AreEqual(0, rig.Ammo.Current(AmmoPool.GroundSecondary));
            Assert.IsFalse(Fire(shotgun).Accepted);
            Assert.AreEqual(1, rig.Seen<GunDryFired>().Count());

            // Releasing starts the regen alarm with its full delay.
            Release(shotgun);
            rig.Do(new TickAmmo(rig.Id, 0.9f));
            Assert.AreEqual(0, rig.Ammo.Current(AmmoPool.GroundSecondary));
            rig.Do(new TickAmmo(rig.Id, 0.11f));
            Assert.AreEqual(2, rig.Ammo.Current(AmmoPool.GroundSecondary));
        }

        [Test]
        public void ShotgunAmmoRefillsTwoPerSecondUpToTwelve()
        {
            int shotgun = rig.Install(2, GunType.Shotgun, WeaponSlot.Secondary);
            Press(shotgun);
            Fire(shotgun);
            Release(shotgun);
            Assert.AreEqual(8, rig.Ammo.Current(AmmoPool.GroundSecondary));

            rig.Do(new TickAmmo(rig.Id, 1f));
            Assert.AreEqual(10, rig.Ammo.Current(AmmoPool.GroundSecondary));
            rig.Do(new TickAmmo(rig.Id, 5f));
            Assert.AreEqual(12, rig.Ammo.Current(AmmoPool.GroundSecondary));
        }

        [Test]
        public void ShotgunAsAPrimarySpendsTheGroundPrimaryPool()
        {
            int shotgun = rig.Install(2, GunType.Shotgun, WeaponSlot.Primary);
            Assert.IsTrue(Fire(shotgun).Accepted);
            Assert.AreEqual(149, rig.Ammo.Current(AmmoPool.GroundPrimary));
            Assert.AreEqual(12, rig.Ammo.Current(AmmoPool.GroundSecondary));
        }

        // ---- flamer ----

        [Test]
        public void FlamethrowerDoesNotFireWhileFlyingOrShielding()
        {
            int flamer = rig.Install(2, GunType.Flamer, WeaponSlot.Primary);

            Assert.IsTrue(rig.Do(new RaiseShield(rig.Id)).Accepted);
            Assert.IsFalse(Fire(flamer).Accepted);
            rig.Do(new DropShield(rig.Id));

            Assert.IsTrue(rig.Do(new Leap(rig.Id, rig.Flight.Direction)).Accepted);
            Assert.IsFalse(Fire(flamer).Accepted);
            Assert.AreEqual(0, rig.Seen<GunFired>().Count());
        }

        [Test]
        public void FlamethrowerFiresEveryEightSteps()
        {
            int flamer = rig.Install(2, GunType.Flamer, WeaponSlot.Primary);
            Press(flamer);
            int shots = 0;
            // One second of held trigger at 120 steps/sec: 120 / 8 = 15 flames.
            for (int i = 0; i < 120; i++)
            {
                if (Fire(flamer).Accepted) shots++;
                rig.Do(new TickGun(rig.Id, flamer, Step));
            }
            Assert.AreEqual(15, shots);
            Assert.AreEqual(150 - 15, rig.Ammo.Current(AmmoPool.GroundPrimary));
        }

        // ---- mine launcher ----

        [Test]
        public void LauncherDropsAMineOnlyInFlight()
        {
            int launcher = rig.Install(3, GunType.MineLauncher, WeaponSlot.Secondary);
            Assert.IsFalse(Fire(launcher).Accepted, "perched: fire_secondary's ground branch fires the shotgun instead");

            rig.Do(new Leap(rig.Id, rig.Flight.Direction));
            Assert.IsTrue(Fire(launcher).Accepted);
            rig.Do(new TickGun(rig.Id, launcher, GameClock.Seconds(30f)));
            rig.Do(new HaltShip(rig.Id));
            Assert.IsFalse(Fire(launcher).Accepted);
            Assert.AreEqual(1, rig.Seen<GunFired>().Count(f => f.Type == GunType.MineLauncher));
        }

        [Test]
        public void LauncherRearmsAfterThirtyStepsAndHoldsEightMines()
        {
            int launcher = rig.Install(3, GunType.MineLauncher, WeaponSlot.Secondary);
            rig.Do(new Leap(rig.Id, rig.Flight.Direction));
            Assert.AreEqual(8, rig.Ammo.Max(AmmoPool.AirSecondary));
            Assert.AreEqual(1, rig.Ammo.Cost(AmmoPool.AirSecondary));

            Assert.IsTrue(Fire(launcher).Accepted);
            for (int i = 0; i < 29; i++) rig.Do(new TickGun(rig.Id, launcher, Step));
            Assert.IsFalse(Fire(launcher).Accepted);
            rig.Do(new TickGun(rig.Id, launcher, Step));
            Assert.IsTrue(Fire(launcher).Accepted);
            Assert.AreEqual(6, rig.Ammo.Current(AmmoPool.AirSecondary));
        }

        [Test]
        public void LauncherClicksWhenEmptyAndRegainsOneMinePerSecond()
        {
            int launcher = rig.Install(3, GunType.MineLauncher, WeaponSlot.Secondary);
            rig.Do(new Leap(rig.Id, rig.Flight.Direction));
            Press(launcher);
            for (int i = 0; i < 8; i++)
            {
                rig.Do(new RearmGun(rig.Id, launcher));
                Assert.IsTrue(Fire(launcher).Accepted);
            }
            rig.Do(new RearmGun(rig.Id, launcher));
            Assert.IsFalse(Fire(launcher).Accepted);
            Assert.AreEqual(1, rig.Seen<GunDryFired>().Count());

            Release(launcher); // GML: releasing starts Alarm 10
            rig.Do(new TickAmmo(rig.Id, 1f));
            Assert.AreEqual(1, rig.Ammo.Current(AmmoPool.AirSecondary));
        }

        // ---- data ----

        [Test]
        public void DamageIsATable()
        {
            var table = new WeaponTable();
            Assert.AreEqual(1, table.BulletDamage(GunType.Vulcan));
            Assert.AreEqual(1, table.BulletDamage(GunType.Shotgun));
            Assert.AreEqual(0, table.BulletDamage(GunType.Flamer));
        }

        [Test]
        public void EveryGunsIntervalComesFromTheTable()
        {
            var table = new WeaponTable();
            Assert.AreEqual(9f, table.GunIntervalSteps(GunType.Vulcan));
            Assert.AreEqual(120f, table.GunIntervalSteps(GunType.Shotgun));
            Assert.AreEqual(8f, table.GunIntervalSteps(GunType.Flamer));
            Assert.AreEqual(30f, table.GunIntervalSteps(GunType.MineLauncher));
            Assert.AreEqual(240f, table.GunIntervalSteps(GunType.Railgun));
            Assert.AreEqual(0f, table.GunIntervalSteps(GunType.Laser));
            Assert.AreEqual(25f, table.AirIntervalSteps(AirWeapon.Rocket));
            Assert.AreEqual(30f, table.AirIntervalSteps(AirWeapon.Mine));
        }

        [Test]
        public void SeatsFireTheirOwnGuns()
        {
            var other = new PlayerRig(id: 2);
            int mine = other.Install(1, GunType.Shotgun, WeaponSlot.Secondary);
            other.Landed();
            Press(vulcan);
            rig.RunGun(vulcan, 1);
            Assert.AreEqual(149, rig.Ammo.Current(AmmoPool.GroundPrimary));
            Assert.AreEqual(150, other.Ammo.Current(AmmoPool.GroundPrimary));
            Assert.IsTrue(other.Do(new FireGun(other.Id, mine)).Accepted);
        }
    }
}
