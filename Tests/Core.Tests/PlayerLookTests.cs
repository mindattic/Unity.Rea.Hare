using System.Linq;
using NUnit.Framework;
using Unity.Hare.Content;
using Unity.Hare.Player;
using Unity.Hare.Weapons;
using Assert = NUnit.Framework.Legacy.ClassicAssert;

namespace Unity.Hare.Tests.Core
{
    /// <summary>
    /// U.HARE-T55: what obj_player draws and the trails it leaves (ShipLook): Alarm 2's smoke trail, Alarm 3's spark
    /// trail while turning, the justspawned jet, the held ground weapon and the draw flags.
    /// </summary>
    public class PlayerLookTests
    {
        const float Step = PlayerRig.Step;

        static PlayerRig NewRig(float jitter = 0f)
        {
            var rig = new PlayerRig(jitter: jitter);
            rig.Do(new ResetFlight(rig.Id)); // obj_player Create + setup_level's reset_player
            return rig;
        }

        static int Run(PlayerRig rig, int steps) => Count<SmokeTrailPuffed>(rig, steps);

        static int Count<T>(PlayerRig rig, int steps) where T : PlayerEvent
        {
            int seen = 0;
            for (int i = 0; i < steps; i++) seen += rig.Do(new StepFlight(rig.Id, Step)).Events.OfType<T>().Count();
            return seen;
        }

        // ---- bullet_asset and the B key (U.HARE-T93) ----

        [Test]
        public void TheVulcansRoundsStartAsSprBulletVulcan()
        {
            Assert.AreEqual("spr_bullet_vulcan", new PlayerRig().Ship.Look.BulletSprite, "obj_player Create");
        }

        [Test]
        public void TheBKeyCyclesTheBulletLookAndWrapsRound()
        {
            var rig = NewRig();
            string[] expected = { "spr_bullet_blue", "spr_bullet_orange", "spr_bullet_flash", "spr_bullet", "spr_bullet_vulcan" };
            foreach (string sprite in expected)
            {
                Assert.IsTrue(rig.Do(new CycleBulletSprite(rig.Id)).Accepted);
                Assert.AreEqual(sprite, rig.Ship.Look.BulletSprite);
            }
        }

        [Test]
        public void ANewShipForTheNextLevelStartsBackOnSprBulletVulcan()
        {
            var rig = NewRig();
            rig.Do(new CycleBulletSprite(rig.Id));
            rig.Do(new SetUpShip(rig.Id)); // setup_level's new obj_player
            Assert.AreEqual("spr_bullet_vulcan", rig.Ship.Look.BulletSprite);
        }

        [Test]
        public void TheBulletLooksAreData()
        {
            var registry = ContentRegistry.FromJson("{ \"weaponPhysicals\": { \"Vulcan\": { \"BulletSprites\": [\"spr_bullet\"] } } }");
            CollectionAssert(registry.WeaponPhysicals.Vulcan.BulletSprites, "spr_bullet");
            NUnit.Framework.Assert.Throws<ContentValidationException>(() =>
                ContentRegistry.FromJson("{ \"weaponPhysicals\": { \"Vulcan\": { \"BulletSprites\": [] } } }"));
        }

        static void CollectionAssert(string[] actual, params string[] expected) =>
            NUnit.Framework.Legacy.CollectionAssert.AreEqual(expected, actual);

        // ---- Alarm 2: the smoke trail ----

        [Test]
        public void SmokeTrailIsNotArmedBeforeResetPlayer()
        {
            var rig = new PlayerRig();
            Assert.IsFalse(rig.Ship.Look.SmokeTrailArmed, "obj_player Create never arms alarm_smoke_trail");
            Assert.AreEqual(0, Run(rig, 200));
        }

        [Test]
        public void SmokeTrailPuffsEverySmokeDelayStepsWhileFlying()
        {
            var rig = NewRig();
            Assert.IsTrue(rig.Ship.Look.SmokeTrailArmed);
            Assert.AreEqual(0, Run(rig, 39), "smoke_delay = 40");
            Assert.AreEqual(1, Run(rig, 1));
            Assert.AreEqual(2, Run(rig, 80));
        }

        [Test]
        public void SmokeTrailRearmsButStaysQuietWhilePerched()
        {
            var rig = NewRig().Landed();
            Assert.AreEqual(0, Run(rig, 200), "Alarm 2 only puffs if(state == state_moving)");
            Assert.IsTrue(rig.Ship.Look.SmokeTrailArmed, "it re-arms itself every time");
        }

        [Test]
        public void LeapRestartsTheSmokeTrailClock()
        {
            var rig = NewRig().Landed();
            Run(rig, 25);
            Assert.IsTrue(rig.Do(new Leap(rig.Id, rig.Flight.Facing)).Accepted);
            Assert.AreEqual(0, Run(rig, 39), "player_leap: alarm[alarm_smoke_trail] = smoke_delay");
            Assert.AreEqual(1, Run(rig, 1));
        }

        [Test]
        public void ADeadShipLeavesNoSmoke()
        {
            var rig = NewRig();
            rig.Do(new HaltShip(rig.Id));
            Assert.AreEqual(0, Run(rig, 120));
        }

        // ---- Alarm 3: the spark trail ----

        [Test]
        public void SparkTrailSparksEverySparkDelayStepsWhileTurning()
        {
            var rig = NewRig();
            Run(rig, 10); // reset_player's spark alarm rings with no turn and stops
            Assert.IsFalse(rig.Ship.Look.SparkTrailArmed);

            rig.Do(new SetTurn(rig.Id, 1f));
            Assert.AreEqual(0, Count<SparkTrailSparked>(rig, 8), "rotate_*: alarm[alarm_spark_trail] = spark_delay (8)");
            Assert.AreEqual(1, Count<SparkTrailSparked>(rig, 1));
            Assert.AreEqual(3, Count<SparkTrailSparked>(rig, 24), "re-armed every 8 steps while turning");

            rig.Do(new SetTurn(rig.Id, 0f));
            Assert.IsFalse(rig.Flight.IsTurning, "the A/D release: turning = false");
            Assert.AreEqual(0, Count<SparkTrailSparked>(rig, 40));
            Assert.IsFalse(rig.Ship.Look.SparkTrailArmed);
        }

        [TestCase(0f, ShipColor.Amber, 0)]
        [TestCase(2.5f, ShipColor.Pink, 2)]
        [TestCase(3.5f, ShipColor.CornflowerBlue, 3)]
        public void SparksAreAmberPinkOrCornflowerWithAJitter(float roll, ShipColor color, int jitter)
        {
            var rig = NewRig(roll);
            rig.Do(new SetTurn(rig.Id, 1f));
            Count<SparkTrailSparked>(rig, 9);
            var spark = rig.Seen<SparkTrailSparked>().First();
            Assert.AreEqual(color, spark.Color, "irandom_range(1, 3): 1 amber, 2 pink, 3 cornflower blue");
            Assert.AreEqual(jitter, spark.JitterXPx, "irandom_range(-4, 4)");
            Assert.AreEqual(jitter, spark.JitterYPx);
        }

        [Test]
        public void DeathStopsTheTurn()
        {
            var rig = NewRig();
            rig.Do(new SetTurn(rig.Id, 1f));
            rig.Do(new StepFlight(rig.Id, Step));
            Assert.IsTrue(rig.Flight.IsTurning);
            rig.Do(new HaltShip(rig.Id));
            Assert.IsFalse(rig.Flight.IsTurning, "player_hurt: turning = false");
        }

        // ---- The justspawned jet ----

        [Test]
        public void SpawnJetPuffsEveryStepWhileSpawnProtected()
        {
            var rig = NewRig();
            Assert.IsTrue(rig.Health.IsSpawnProtected);
            Assert.AreEqual(10, Count<SpawnJetPuffed>(rig, 10), "one ef_smoke a step");

            // A 50 Hz physics step still puffs once per GML step.
            int puffs = 0;
            for (int i = 0; i < 10; i++) puffs += rig.Do(new StepFlight(rig.Id, 0.02f)).Events.OfType<SpawnJetPuffed>().Count();
            Assert.AreEqual(24, puffs);

            rig.Do(new TickHealth(rig.Id, 2f));
            Assert.IsFalse(rig.Health.IsSpawnProtected);
            Assert.AreEqual(0, Count<SpawnJetPuffed>(rig, 10));
        }

        [Test]
        public void LandingEndsTheJet()
        {
            var rig = NewRig();
            rig.Do(new LandOn(rig.Id, new LandingSurface(SurfaceKind.Rock, 7, 90f, 270f, 0.5f, 0f, 0f)));
            Assert.AreEqual(0, Count<SpawnJetPuffed>(rig, 10), "the landing collisions: justspawned = false");
        }

        // ---- What it draws ----

        [Test]
        public void HoldsTheVulcanUntilAnotherGroundWeaponFires()
        {
            var rig = NewRig().Landed();
            Assert.AreEqual(GroundWeapon.Vulcan, rig.Ship.Look.HeldWeapon, "Create: ground_weapon_asset = spr_weapon_vulcan");
            Assert.AreEqual("spr_weapon_vulcan", rig.Ship.Look.HeldWeaponSprite);

            int mine = rig.Install(2, GunType.MineLauncher, WeaponSlot.Secondary);
            rig.Do(new FireGun(rig.Id, mine));
            Assert.AreEqual(GroundWeapon.Vulcan, rig.Ship.Look.HeldWeapon, "the mine is no ground weapon");

            int shotgun = rig.Install(1, GunType.Shotgun, WeaponSlot.Secondary);
            Assert.IsTrue(rig.Do(new FireGun(rig.Id, shotgun)).Has<GunFired>());
            Assert.AreEqual(GroundWeapon.Shotgun, rig.Ship.Look.HeldWeapon, "shoot_shotgun: ground_weapon_asset = spr_weapon_shotgun");
            Assert.AreEqual("spr_weapon_shotgun", rig.Ship.Look.HeldWeaponSprite);
        }

        [TestCase(GroundWeapon.Vulcan, "spr_weapon_vulcan")]
        [TestCase(GroundWeapon.Laser, "spr_weapon_laser")]
        [TestCase(GroundWeapon.Railgun, "spr_weapon_railgun")]
        [TestCase(GroundWeapon.Shotgun, "spr_weapon_shotgun")]
        [TestCase(GroundWeapon.Flamer, "spr_weapon_flamer")]
        public void EachGroundWeaponHasItsSprite(GroundWeapon weapon, string sprite) =>
            Assert.AreEqual(sprite, ShipLook.WeaponSprite(weapon));

        [Test]
        public void TheGunShowsOnlyWhilePerchedAndNothingShowsWhileDead()
        {
            var rig = NewRig();
            Assert.IsTrue(rig.Ship.Look.IsDrawn);
            Assert.IsFalse(rig.Ship.Look.ShowsGun, "Draw: the air weapon line is commented out");
            rig.Landed();
            Assert.IsTrue(rig.Ship.Look.ShowsGun);
            rig.Do(new HaltShip(rig.Id));
            Assert.IsFalse(rig.Ship.Look.IsDrawn, "Draw: if(state != state_dead)");
            Assert.IsFalse(rig.Ship.Look.ShowsGun);
        }

        [Test]
        public void PoseFollowsStateCrouchShieldAndHurt()
        {
            var rig = NewRig();
            Assert.AreEqual(ShipPose.Leap, rig.Ship.Pose);
            rig.Landed();
            Assert.AreEqual(ShipPose.Idle, rig.Ship.Pose);
            rig.Do(new PressLeapKey(rig.Id));
            Assert.AreEqual(ShipPose.Crouch, rig.Ship.Pose);
            rig.Do(new TickHealth(rig.Id, 2f)); // past justspawned
            Assert.IsTrue(rig.Do(new TakeHit(rig.Id)).Accepted);
            Assert.AreEqual(ShipPose.CrouchHurt, rig.Ship.Pose);
        }

        [Test]
        public void ScaleAndTimingComeFromTheLookSection()
        {
            Assert.AreEqual(1f, new PlayerRig().Ship.Look.Scale, "obj_player Create: scale = 1");
            var registry = ContentRegistry.FromJson("{ \"look\": { \"Scale\": 2, \"SmokeDelaySteps\": 20 } }");
            Assert.AreEqual(2f, registry.Look.Scale);
            Assert.AreEqual(20f, registry.Look.SmokeDelaySteps);
            Assert.AreEqual(8f, registry.Look.SparkDelaySteps);
            NUnit.Framework.Assert.Throws<ContentValidationException>(() => ContentRegistry.FromJson("{ \"look\": { \"Scale\": 0 } }"));
        }
    }
}
