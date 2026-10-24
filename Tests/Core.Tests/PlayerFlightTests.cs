using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using Unity.Hare.Player;

namespace Unity.Hare.Tests.Core
{
    /// <summary>obj_player's flight and energy state machine and the Idle-only shield, run outside Unity.</summary>
    public class PlayerFlightTests
    {
        PlayerRig rig;

        [SetUp]
        public void SetUp() => rig = new PlayerRig();

        ShipFlight Flight => rig.Flight;
        void Step(float dt) => rig.Do(new StepFlight(rig.Id, dt));

        // ---- flight ----

        [Test]
        public void StartsInMovingStateAtFullEnergy()
        {
            Assert.AreEqual(PlayerFlightState.Moving, Flight.State);
            Assert.AreEqual(100f, Flight.Energy);
        }

        [Test]
        public void BoostDoublesSpeedWhenEnergyIsSufficient()
        {
            Assert.IsTrue(rig.Do(new Boost(rig.Id)).Accepted);
            Assert.IsTrue(Flight.IsBoosting);
            Assert.AreEqual(10f, Flight.Speed); // flight_speed(5) * 2, GML player_boost
        }

        [Test]
        public void BoostAndBrakeAreMutuallyExclusive()
        {
            Assert.IsTrue(rig.Do(new Boost(rig.Id)).Accepted);
            Assert.IsFalse(rig.Do(new Brake(rig.Id)).Accepted);

            var other = new PlayerRig();
            Assert.IsTrue(other.Do(new Brake(other.Id)).Accepted);
            Assert.IsFalse(other.Do(new Boost(other.Id)).Accepted);
        }

        [Test]
        public void BrakeEasesDownToHalfSpeedAndTurnsFaster()
        {
            rig.Do(new Brake(rig.Id));
            rig.Do(new SetTurn(rig.Id, 1f));
            Step(0.1f);
            Assert.AreEqual(270f + 24f, Flight.Direction, 1e-3f, "brake turns 240 degrees a second");
            Step(1f);
            Assert.AreEqual(2.5f, Flight.Speed, 1e-4f);
        }

        [Test]
        public void TurnInputRotatesAtTheOriginalsRate()
        {
            rig.Do(new SetTurn(rig.Id, -1f));
            Step(0.5f);
            Assert.AreEqual(270f - 60f, Flight.Direction, 1e-3f);
        }

        [Test]
        public void AimIsKeptInZeroToThreeSixty()
        {
            rig.Do(new SetAim(rig.Id, -90f));
            Assert.AreEqual(270f, Flight.AimDirection, 1e-3f);
        }

        [Test]
        public void LeapFailsWhileMoving()
        {
            Assert.IsFalse(rig.Do(new Leap(rig.Id, 270f)).Accepted);
        }

        [Test]
        public void LeapFailsOutsideJumpArc()
        {
            rig.Landed();
            // Facing (Direction) starts at 270; jump_arc is 100 degrees either side.
            Assert.IsFalse(rig.Do(new Leap(rig.Id, 270f - 180f)).Accepted);
        }

        [Test]
        public void LeapSucceedsWithinJumpArcAndEntersMoving()
        {
            rig.Landed();
            Assert.AreEqual(PlayerFlightState.Idle, Flight.State);

            var result = rig.Do(new Leap(rig.Id, 270f + 50f));
            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(1, result.Events.OfType<ShipLeapt>().Count());
            Assert.AreEqual(PlayerFlightState.Moving, Flight.State);
            Assert.AreEqual(5f, Flight.Speed); // flight_speed, GML player_leap
        }

        [Test]
        public void LandingStopsTheShipAndAnnouncesIt()
        {
            var result = rig.Do(new Land(rig.Id));
            Assert.AreEqual(1, result.Events.OfType<ShipLanded>().Count());
            Assert.AreEqual(PlayerFlightState.Idle, Flight.State);
            Assert.AreEqual(0f, Flight.Speed);
        }

        [Test]
        public void ADeadShipCannotLand()
        {
            rig.Do(new HaltShip(rig.Id));
            Assert.IsFalse(rig.Do(new Land(rig.Id)).Accepted);
            Assert.AreEqual(PlayerFlightState.Dead, Flight.State);
        }

        // ---- shield ----

        [Test]
        public void ShieldCannotBeRaisedWhileFlying()
        {
            Assert.IsFalse(rig.Do(new RaiseShield(rig.Id)).Accepted);
            Assert.IsFalse(Flight.IsShielding);
        }

        [Test]
        public void ShieldRaisesFromIdle()
        {
            rig.Landed();
            var result = rig.Do(new RaiseShield(rig.Id));
            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(Flight.IsShielding);
            Assert.AreEqual(0.75f, result.Find<ShieldRaised>().Radius);
        }

        [Test]
        public void ShieldDoesNotStackWhileAlreadyRaised()
        {
            rig.Landed();
            Assert.IsTrue(rig.Do(new RaiseShield(rig.Id)).Accepted);
            Assert.IsFalse(rig.Do(new RaiseShield(rig.Id)).Accepted);
            Assert.AreEqual(1, rig.Seen<ShieldRaised>().Count());
        }

        [Test]
        public void ShieldRefusedBelowAThirdEnergy()
        {
            rig.Landed();
            Assert.IsTrue(rig.Do(new RaiseShield(rig.Id)).Accepted);
            // Drain to just under 33% (40/s placeholder drain), then release and retry.
            while (Flight.Energy >= 33f) Step(0.1f);
            rig.Do(new DropShield(rig.Id));
            Assert.IsFalse(rig.Do(new RaiseShield(rig.Id)).Accepted);
        }

        [Test]
        public void ShieldDrainsEnergyAndCollapsesWhenEmpty()
        {
            rig.Landed();
            rig.Do(new RaiseShield(rig.Id));

            Step(0.5f);
            Assert.Less(Flight.Energy, 100f);
            Assert.IsTrue(Flight.IsShielding);

            for (int i = 0; i < 100 && Flight.IsShielding; i++) Step(0.1f);
            Assert.IsFalse(Flight.IsShielding);
            Assert.AreEqual(1, rig.Seen<ShieldDropped>().Count());
        }

        [Test]
        public void DroppingShieldLetsEnergyRefill()
        {
            rig.Landed();
            rig.Do(new RaiseShield(rig.Id));
            Step(1f);
            float drained = Flight.Energy;

            rig.Do(new DropShield(rig.Id));
            Step(1f);
            Assert.Greater(Flight.Energy, drained);
        }

        [Test]
        public void LeapDropsShield()
        {
            rig.Landed();
            rig.Do(new RaiseShield(rig.Id));
            Assert.IsTrue(rig.Do(new Leap(rig.Id, 270f)).Accepted);
            Assert.IsFalse(Flight.IsShielding);
            Assert.AreEqual(1, rig.Seen<ShieldDropped>().Count());
        }

        [Test]
        public void DroppingAShieldThatIsNotUpRaisesNothing()
        {
            Assert.IsTrue(rig.Do(new DropShield(rig.Id)).Accepted);
            Assert.AreEqual(0, rig.Seen<ShieldDropped>().Count());
        }

        [Test]
        public void ResettingFlightPutsTheShipBackInTheAirAndDropsTheShield()
        {
            rig.Landed();
            rig.Do(new RaiseShield(rig.Id));
            rig.Do(new ResetFlight(rig.Id));
            Assert.AreEqual(PlayerFlightState.Moving, Flight.State);
            Assert.IsFalse(Flight.IsShielding);
            Assert.AreEqual(100f, Flight.Energy);
        }

        [Test]
        public void TuningIsData()
        {
            rig.Do(new ResetFlight(rig.Id, new FlightTuning { FlightSpeed = 7f }));
            Assert.AreEqual(7f, Flight.Speed);
            rig.Do(new Boost(rig.Id));
            Assert.AreEqual(14f, Flight.Speed);
        }
    }
}
