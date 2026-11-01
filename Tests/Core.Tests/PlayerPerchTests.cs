using System;
using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Tests.Core
{
    /// <summary>
    /// U.HARE-T53: obj_player on the rock field, run outside Unity. The par_rock / par_structure landing collisions,
    /// riding a spinning rock (land_angle, distance_to_center), player_leap within jump_arc of the facing with the rock's
    /// take-off bonus, fling_player, crouching, and the spin while airborne (spin = 3, the end_flash alarm, was_spinning).
    /// </summary>
    public class PlayerPerchTests
    {
        const float Step = PlayerRig.Step;
        PlayerRig rig;

        [SetUp]
        public void SetUp() => rig = new PlayerRig();

        ShipFlight Flight => rig.Flight;

        static LandingSurface Rock(int id = 7, float normal = 90f, float landAngle = 270f, float distance = 1.2f,
            float speed = 0f, float spin = 0f) =>
            new LandingSurface(SurfaceKind.Rock, id, normal, landAngle, distance, speed, spin);

        // ---- landing ----

        [Test]
        public void LandingOnARockPerchesFacingTheNormalAndRemembersTheRock()
        {
            rig.Do(new SetTurn(rig.Id, 1f));
            rig.Do(new Boost(rig.Id));
            var result = rig.Do(new LandOn(rig.Id, Rock(normal: 45f, landAngle: 225f, distance: 1.5f, speed: 1f, spin: 0.2f)));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(PlayerFlightState.Idle, Flight.State);
            Assert.AreEqual(45f, Flight.Facing, 1e-4f, "image_angle = normal");
            Assert.AreEqual(7, Flight.CurrentRock);
            Assert.AreEqual(225f, Flight.LandAngle, 1e-4f);
            Assert.AreEqual(1.5f, Flight.DistanceToCenter, 1e-4f);
            Assert.AreEqual(0f, Flight.Speed);
            Assert.IsFalse(Flight.IsBoosting, "rocketing = false");
            var landed = result.Find<ShipLanded>();
            Assert.AreEqual(SurfaceKind.Rock, landed.Surface);
            Assert.AreEqual(7, landed.SurfaceId);
        }

        [Test]
        public void LandingStopsTheSpinAndEndsSpawnProtection()
        {
            rig.Do(new PressSpinKey(rig.Id));
            Assert.Greater(Flight.Spin, 0f);
            Assert.IsTrue(rig.Health.IsSpawnProtected);

            rig.Do(new LandOn(rig.Id, Rock()));
            Assert.AreEqual(0f, Flight.Spin, "spin = 0");
            Assert.IsFalse(Flight.WasSpinning, "was_spinning = false");
            Assert.IsFalse(rig.Health.IsSpawnProtected, "justspawned = false");
        }

        [Test]
        public void LandingRunsSetAmmoCost()
        {
            rig.Install(1, GunType.Vulcan, WeaponSlot.Primary);
            rig.Do(new SetAmmoCost(rig.Id, GroundWeapon.Flamer, null, null, false));
            Assert.AreEqual(150, rig.Ammo.Max(AmmoPool.GroundPrimary));

            rig.Do(new LandOn(rig.Id, Rock()));
            Assert.AreEqual(120, rig.Ammo.Max(AmmoPool.GroundPrimary), "set_ammo_cost on landing");
        }

        [Test]
        public void APerchedShipDoesNotLandAgain()
        {
            rig.Do(new LandOn(rig.Id, Rock(id: 1)));
            Assert.IsFalse(rig.Do(new LandOn(rig.Id, Rock(id: 2))).Accepted, "only state_moving lands");
            Assert.AreEqual(1, Flight.CurrentRock);
        }

        [Test]
        public void LandingOnAStructureRemembersItAndRidesNothing()
        {
            var result = rig.Do(new LandOn(rig.Id, new LandingSurface(SurfaceKind.Structure, 3, 180f)));
            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(3, Flight.CurrentStructure);
            Assert.IsNull(Flight.CurrentRock);
            Assert.AreEqual(180f, Flight.Facing, 1e-4f);
            Assert.IsFalse(rig.Do(new RideRock(rig.Id, 1f, 0f, Step)).Accepted, "a structure does not carry the ship");
        }

        [Test]
        public void NoNormalFoundFacesThreeFiftyNine()
        {
            rig.Do(new LandOn(rig.Id, Rock(normal: -1f)));
            Assert.AreEqual(359f, Flight.Facing, 1e-4f, "image_angle = -1, normalised");
        }

        [Test]
        public void PerchingEnablesTheShieldAndTheGroundGuns()
        {
            int vulcan = rig.Install(1, GunType.Vulcan, WeaponSlot.Primary);
            Assert.IsFalse(rig.Do(new FireGun(rig.Id, vulcan)).Accepted, "no ground fire in flight");
            Assert.IsFalse(rig.Do(new PressBoostKey(rig.Id)).Events.OfType<ShieldRaised>().Any());

            rig.Do(new LandOn(rig.Id, Rock()));
            Assert.IsTrue(rig.Do(new FireGun(rig.Id, vulcan)).Accepted, "ground weapons fire once perched");
            Assert.IsTrue(rig.Do(new PressBoostKey(rig.Id)).Has<ShieldRaised>(), "W raises the shield once perched");
        }

        // ---- riding the rock ----

        [Test]
        public void RidingASpinningRockTurnsTheLandAngleAndTheFacingWithIt()
        {
            rig.Do(new LandOn(rig.Id, Rock(normal: 90f, landAngle: 270f)));
            for (int i = 0; i < 10; i++) rig.Do(new RideRock(rig.Id, 0.3f, 1f, Step)); // spin 0.3 a step for 10 steps

            Assert.AreEqual(273f, Flight.LandAngle, 1e-3f, "land_angle += current_rock.spin");
            Assert.AreEqual(93f, Flight.Facing, 1e-3f, "image_angle += current_rock.spin");
            Assert.AreEqual(1.2f, Flight.DistanceToCenter, 1e-5f, "distance_to_center is kept");
        }

        [Test]
        public void RidingNeedsARock()
        {
            Assert.IsFalse(rig.Do(new RideRock(rig.Id, 1f, 0f, Step)).Accepted, "flying");
            rig.Landed();
            Assert.IsFalse(rig.Do(new RideRock(rig.Id, 1f, 0f, Step)).Accepted, "perched with no rock");
        }

        // ---- player_leap ----

        [Test]
        public void TheLeapIsMeasuredFromTheFacingNotTheOldHeading()
        {
            rig.Do(new LandOn(rig.Id, Rock(normal: 90f))); // flew in heading 270, now faces up
            Assert.IsFalse(rig.Do(new Leap(rig.Id, 270f)).Accepted, "back down into the rock is outside jump_arc");
            Assert.IsTrue(Flight.CanLeap(90f + 100f), "jump_arc is 100 either side");
            Assert.IsFalse(Flight.CanLeap(90f + 101f));
            Assert.IsTrue(rig.Do(new Leap(rig.Id, 30f)).Accepted);
        }

        [Test]
        public void LeapingFliesTowardTheAimWithTheRocksSpeedAndSpinAndLeavesIt()
        {
            rig.Do(new LandOn(rig.Id, Rock(normal: 90f, speed: -1.5f, spin: -0.25f)));
            Assert.IsTrue(rig.Do(new Leap(rig.Id, 60f)).Accepted);

            Assert.AreEqual(PlayerFlightState.Moving, Flight.State);
            Assert.AreEqual(60f, Flight.Direction, 1e-4f);
            Assert.AreEqual(60f, Flight.Facing, 1e-4f, "image_angle = direction");
            Assert.AreEqual(Flight.Tuning.FlightSpeed + 1.5f + 0.25f, Flight.Speed, 1e-4f, "speed += abs(rock.speed) + abs(rock.spin)");
            Assert.IsNull(Flight.CurrentRock, "current_rock = noone");
            Assert.IsNull(Flight.CurrentStructure, "current_structure = noone");
        }

        [Test]
        public void BoostingAfterATakeOffDoublesTheCurrentSpeedRockBonusIncluded()
        {
            rig.Do(new LandOn(rig.Id, Rock(normal: 90f, speed: -1.5f, spin: -0.25f)));
            rig.Do(new Leap(rig.Id, 60f));
            float takeOff = Flight.Speed;

            Assert.IsTrue(rig.Do(new Boost(rig.Id)).Accepted);
            Assert.AreEqual(takeOff * 2f, Flight.Speed, 1e-4f, "player_boost: player.speed = player.speed * 2 (U.HARE-T88)");
            Assert.AreNotEqual(Flight.Tuning.FlightSpeed * 2f, Flight.Speed, "not flight_speed * 2");
        }

        [Test]
        public void LeapingOffAStructureTakesOffAtFlightSpeed()
        {
            rig.Do(new LandOn(rig.Id, new LandingSurface(SurfaceKind.Structure, 3, 90f)));
            rig.Do(new Leap(rig.Id, 90f));
            Assert.AreEqual(Flight.Tuning.FlightSpeed, Flight.Speed, 1e-4f);
            Assert.IsNull(Flight.CurrentStructure);
        }

        [Test]
        public void TheRidesLatestSpinIsTheTakeOffBonus()
        {
            rig.Do(new LandOn(rig.Id, Rock(normal: 90f, speed: 0f, spin: 0f)));
            rig.Do(new RideRock(rig.Id, 2f, 1f, Step));
            rig.Do(new Leap(rig.Id, 90f));
            Assert.AreEqual(Flight.Tuning.FlightSpeed + 3f, Flight.Speed, 1e-4f);
        }

        // ---- crouching ----

        [Test]
        public void TheLeapKeyCrouchesWhilePerchedAndTheLeapStandsUp()
        {
            rig.Do(new PressLeapKey(rig.Id));
            Assert.IsFalse(Flight.IsCrouching, "no crouch in flight");
            rig.Do(new LandOn(rig.Id, Rock(normal: 90f)));
            Assert.IsTrue(rig.Do(new PressLeapKey(rig.Id)).Accepted);
            Assert.IsTrue(Flight.IsCrouching, "crouching = true");

            rig.Do(new Leap(rig.Id, 90f));
            Assert.IsFalse(Flight.IsCrouching, "player_leap: crouching = false");
        }

        [Test]
        public void ReleasingWOrSStandsTheShipUp()
        {
            rig.Do(new LandOn(rig.Id, Rock()));
            rig.Do(new PressLeapKey(rig.Id));
            rig.Do(new ReleaseShieldKey(rig.Id));
            Assert.IsFalse(Flight.IsCrouching);
        }

        [Test]
        public void TheLeapKeyBoostsOrBrakesInFlight()
        {
            Assert.IsTrue(rig.Do(new PressLeapKey(rig.Id, AirPress.Boost)).Accepted);
            Assert.IsTrue(Flight.IsBoosting, "Space: player_boost");

            var braking = new PlayerRig();
            Assert.IsTrue(braking.Do(new PressLeapKey(braking.Id, AirPress.Brake)).Accepted);
            Assert.IsTrue(braking.Flight.IsBraking, "right mouse: player_brake");
            Assert.IsFalse(braking.Flight.IsCrouching);
        }

        // ---- fling_player ----

        [Test]
        public void FlingThrowsTheShipOffAlongItsFacingSpinning()
        {
            rig.Do(new LandOn(rig.Id, Rock(normal: 120f, speed: 1f, spin: 0.5f)));
            rig.Do(new RaiseShield(rig.Id));
            Assert.IsTrue(Flight.IsShielding);

            var result = rig.Do(new Fling(rig.Id));
            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(PlayerFlightState.Moving, Flight.State);
            Assert.AreEqual(120f, Flight.Direction, 1e-4f, "direction = image_angle");
            Assert.AreEqual(Flight.Tuning.FlightSpeed + 1.5f, Flight.Speed, 1e-4f);
            Assert.AreEqual(Flight.Tuning.SpinDegreesPerSecond, Flight.Spin, "spin = 3");
            Assert.IsFalse(Flight.IsShielding, "shielding = false");
            Assert.IsNull(Flight.CurrentRock);
            Assert.IsTrue(result.Has<ShieldDropped>());
            Assert.AreEqual(120f, result.Find<ShipFlung>().Direction, 1e-4f);
        }

        [Test]
        public void TheFlingSpinStopsAtEndFlashAndTheFacingWindsBack()
        {
            rig.Do(new LandOn(rig.Id, Rock(normal: 120f)));
            rig.Do(new Fling(rig.Id));
            int flashSteps = (int)rig.Health.Tuning.InvulnerableSteps;

            for (int i = 0; i < flashSteps - 1; i++) rig.Do(new StepFlight(rig.Id, Step));
            Assert.Greater(Flight.Spin, 0f, "still spinning before flash_time");
            Assert.AreNotEqual(120f, Flight.Facing, "the facing spun away from the heading");
            Assert.AreEqual(120f, Flight.Direction, 1e-3f, "spin turns image_angle, not direction");

            rig.Do(new StepFlight(rig.Id, Step));
            Assert.AreEqual(0f, Flight.Spin, "alarm_end_flash: spin = 0");
            Assert.IsTrue(Flight.WasSpinning, "was_spinning in flight");

            for (int i = 0; i < 200 && Flight.WasSpinning; i++) rig.Do(new StepFlight(rig.Id, Step));
            Assert.IsFalse(Flight.WasSpinning);
            Assert.AreEqual(120f, Flight.Facing, 1e-3f, "image_angle = direction");
        }

        [Test]
        public void ADeadShipIsNotFlung()
        {
            rig.Do(new LandOn(rig.Id, Rock()));
            rig.Do(new HaltShip(rig.Id));
            Assert.IsFalse(rig.Do(new Fling(rig.Id)).Accepted);
            Assert.AreEqual(PlayerFlightState.Dead, Flight.State);
        }

        [Test]
        public void ARockClearedAwayIsLetGoOfWithoutAFling()
        {
            rig.Do(new LandOn(rig.Id, Rock()));
            Assert.IsTrue(rig.Do(new LetGoOfRock(rig.Id)).Accepted);
            Assert.IsNull(Flight.CurrentRock);
            Assert.AreEqual(PlayerFlightState.Idle, Flight.State);
        }

        // ---- spinning in the air ----

        [Test]
        public void HoldingSpinTurnsTheFacingThreeDegreesAStep()
        {
            Assert.IsTrue(rig.Do(new PressSpinKey(rig.Id)).Accepted);
            for (int i = 0; i < 10; i++) rig.Do(new StepFlight(rig.Id, Step));
            Assert.AreEqual(300f, Flight.Facing, 1e-3f, "image_angle += spin");
            Assert.AreEqual(270f, Flight.Direction, 1e-3f);

            Assert.IsTrue(rig.Do(new ReleaseSpinKey(rig.Id)).Accepted);
            Assert.AreEqual(0f, Flight.Spin);
            Assert.IsTrue(Flight.WasSpinning);
            for (int i = 0; i < 200 && Flight.WasSpinning; i++) rig.Do(new StepFlight(rig.Id, Step));
            Assert.AreEqual(270f, Flight.Facing, 1e-3f, "wound back onto the heading");
        }

        [Test]
        public void SpinNeedsFlight()
        {
            rig.Landed();
            Assert.IsFalse(rig.Do(new PressSpinKey(rig.Id)).Accepted);
            Assert.IsFalse(rig.Do(new ReleaseSpinKey(rig.Id)).Accepted);
            Assert.AreEqual(0f, Flight.Spin);
        }

        [Test]
        public void TurningTurnsTheFacingWithTheHeading()
        {
            rig.Do(new SetTurn(rig.Id, 1f));
            rig.Do(new StepFlight(rig.Id, 0.5f));
            Assert.AreEqual(330f, Flight.Direction, 1e-3f);
            Assert.AreEqual(330f, Flight.Facing, 1e-3f, "rotate_counterclockwise turns image_angle with direction");
        }

        // ---- hits ----

        [Test]
        public void AHitInFlightSetsTheShipSpinning()
        {
            AdvancePastSpawnProtection();
            Assert.IsTrue(rig.Do(new TakeHit(rig.Id)).Accepted);
            Assert.AreEqual(Flight.Tuning.SpinDegreesPerSecond, Flight.Spin, "obj_bullet / par_enemy: spin = 3");
        }

        [Test]
        public void ARocketExplosionDoesNotSpinTheShip()
        {
            Assert.IsTrue(rig.Do(new TakeHit(rig.Id, ignoresSpawnProtection: true)).Accepted);
            Assert.AreEqual(0f, Flight.Spin);
        }

        [Test]
        public void ARocketFlingsAShipPerchedOnARock()
        {
            AdvancePastSpawnProtection();
            rig.Do(new LandOn(rig.Id, Rock(normal: 30f)));
            var result = rig.Do(new TakeHit(rig.Id, reaction: HitReaction.SpinOrFling));

            Assert.IsTrue(result.Has<ShipFlung>());
            Assert.AreEqual(PlayerFlightState.Moving, Flight.State);
            Assert.AreEqual(30f, Flight.Direction, 1e-4f);
            Assert.IsNull(Flight.CurrentRock);
            Assert.AreEqual(rig.Health.MaxHealth - 1, rig.Health.Health, "player_hurt still runs");
        }

        [Test]
        public void ABulletDoesNotFlingAPerchedShip()
        {
            AdvancePastSpawnProtection();
            rig.Do(new LandOn(rig.Id, Rock()));
            rig.Do(new TakeHit(rig.Id));
            Assert.AreEqual(PlayerFlightState.Idle, Flight.State, "obj_bullet's fling_player is commented out");
            Assert.AreEqual(7, Flight.CurrentRock);
        }

        [Test]
        public void ResetClearsThePerchAndTheSpin()
        {
            rig.Do(new LandOn(rig.Id, Rock()));
            rig.Do(new PressLeapKey(rig.Id));
            rig.Do(new ResetFlight(rig.Id));
            Assert.IsNull(Flight.CurrentRock);
            Assert.IsFalse(Flight.IsCrouching);
            Assert.AreEqual(Flight.Direction, Flight.Facing);
        }

        // ---- normal_detect ----

        [Test]
        public void NormalDetectPointsAwayFromTheSolidSide()
        {
            Assert.AreEqual(90f, SurfaceNormal.Detect(8f, 1f, (x, y) => y < 0f), 1e-3f, "solid below: up");
            Assert.AreEqual(0f, SurfaceNormal.Detect(8f, 1f, (x, y) => x < 0f), 1e-3f, "solid left: right");
            Assert.AreEqual(225f, SurfaceNormal.Detect(8f, 1f, (x, y) => x + y > 0f), 1.5f, "solid up-right: down-left");
        }

        [Test]
        public void NormalDetectIsMinusOneWhenEverythingIsSolid()
        {
            Assert.AreEqual(-1f, SurfaceNormal.Detect(4f, 1f, (x, y) => true));
        }

        void AdvancePastSpawnProtection()
        {
            float seconds = GameClock.Seconds(rig.Health.Tuning.SpawnProtectionSteps) + 0.01f;
            rig.Do(new TickHealth(rig.Id, seconds));
        }
    }
}
