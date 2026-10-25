using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using StringAssert = NUnit.Framework.Legacy.StringAssert;
using Unity.Hare.Content;
using Unity.Hare.Online;
using Unity.Hare.Player;

namespace Unity.Hare.Tests.Core
{
    /// <summary>
    /// The online authority's one world (U.HARE-T35): seats map to player ids, every seat's ship lives in one
    /// PlayerWorld, input arrives as the same commands local play sends, and one step moves every ship.
    /// </summary>
    public class SharedWorldTests
    {
        const float Dt = 1f / GameClock.StepsPerSecond;

        static SharedWorld NewWorld() => new SharedWorld((min, max) => 0f, new OnlineTuning());

        [Test]
        public void SeatZeroIsPlayerOne_AndTheMappingRoundTrips()
        {
            Assert.AreEqual(new PlayerId(1), SharedWorld.PlayerFor(0));
            for (int seat = 0; seat < 8; seat++) Assert.AreEqual(seat, SharedWorld.SeatOf(SharedWorld.PlayerFor(seat)));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => SharedWorld.PlayerFor(-1));
        }

        [Test]
        public void SpawnPointsAlternateAroundTheCentre()
        {
            var world = NewWorld();
            Assert.AreEqual(new Vec2(0f, 0f), world.SpawnPoint(0));
            Assert.AreEqual(new Vec2(1.5f, 0f), world.SpawnPoint(1));
            Assert.AreEqual(new Vec2(-1.5f, 0f), world.SpawnPoint(2));
            Assert.AreEqual(new Vec2(3f, 0f), world.SpawnPoint(3));
        }

        [Test]
        public void ASeatedShipStartsFlyingAtItsSpawnPoint()
        {
            var world = NewWorld();
            var id = world.Seat(1);

            Assert.AreEqual(new PlayerId(2), id);
            Assert.IsTrue(world.IsSeated(id));
            Assert.AreEqual(world.SpawnPoint(1), world.PositionOf(id));
            Assert.AreEqual(PlayerFlightState.Moving, world.Ship(id).Flight.State);
            Assert.AreEqual(270f, world.Ship(id).Flight.Direction);
        }

        [Test]
        public void TheStepMovesEveryShipAlongItsHeadingAtItsSpeed()
        {
            var world = NewWorld();
            var a = world.Seat(0);
            var b = world.Seat(1);
            float speed = world.Ship(a).Flight.Speed;

            world.Step(Dt);

            // Direction 270 (GML: straight down the screen, -y in world space).
            Assert.AreEqual(0f, world.PositionOf(a).X, 1e-4f);
            Assert.AreEqual(-speed * Dt, world.PositionOf(a).Y, 1e-4f);
            Assert.AreEqual(1.5f, world.PositionOf(b).X, 1e-4f);
            Assert.AreEqual(-speed * Dt, world.PositionOf(b).Y, 1e-4f);
        }

        [Test]
        public void InputIsTheSameCommandsLocalPlaySends_AndOnlyMovesItsOwnShip()
        {
            var world = NewWorld();
            var a = world.Seat(0);
            var b = world.Seat(1);

            Assert.IsTrue(world.Execute(new PressBoostKey(a)).Accepted);
            world.Execute(new SetTurn(a, 1f));
            for (int i = 0; i < 12; i++) world.Step(Dt);

            Assert.IsTrue(world.Ship(a).Flight.IsBoosting);
            Assert.Greater(world.Ship(a).Flight.Direction, 270f, "turned counter-clockwise");
            Assert.IsFalse(world.Ship(b).Flight.IsBoosting);
            Assert.AreEqual(270f, world.Ship(b).Flight.Direction);
        }

        [Test]
        public void AReturningSeatGetsTheSameShipWhereItLeftIt()
        {
            var world = NewWorld();
            var id = world.Seat(0);
            world.Execute(new PressBoostKey(id));
            world.Step(Dt);
            var where = world.PositionOf(id);

            Assert.AreEqual(id, world.Seat(0));
            Assert.AreEqual(where, world.PositionOf(id));
            Assert.IsTrue(world.Ship(id).Flight.IsBoosting, "reseating does not reset the ship");
            Assert.AreEqual(1, world.Seated.Count);
        }

        [Test]
        public void AReleasedSeatLeavesTheWorld_AndComesBackFresh()
        {
            var world = NewWorld();
            var id = world.Seat(0);
            world.Execute(new PressBoostKey(id));
            world.Step(Dt);

            Assert.IsTrue(world.Release(0));
            Assert.IsFalse(world.IsSeated(id));
            Assert.IsFalse(world.Players.Has(id));
            Assert.IsFalse(world.Release(0));
            Assert.Throws<KeyNotFoundException>(() => world.Execute(new Boost(id)));

            world.Seat(0);
            Assert.AreEqual(world.SpawnPoint(0), world.PositionOf(id));
            Assert.IsFalse(world.Ship(id).Flight.IsBoosting);
        }

        [Test]
        public void TheStepReturnsTheEventsItRaised()
        {
            var world = NewWorld();
            var id = world.Seat(0);
            world.Execute(new Land(id));
            world.Execute(new PressBoostKey(id)); // perched: the boost key raises the shield
            Assert.IsTrue(world.Ship(id).Flight.IsShielding);

            var events = new List<PlayerEvent>();
            for (int i = 0; i < 120 * 30 && world.Ship(id).Flight.IsShielding; i++) events.AddRange(world.Step(Dt));

            Assert.IsTrue(events.OfType<ShieldDropped>().Any(e => e.Id == id), "the shield collapses once its energy runs out");
            Assert.AreEqual(Vec2.Zero, world.Ship(id).Flight.Velocity, "a perched ship does not move");
        }

        // ---- the key commands local play and the NetEntity both send ----

        [Test]
        public void TheBoostAndBrakeKeysRaiseTheShieldWhilePerched_AndItsReleaseDropsIt()
        {
            var rig = new PlayerRig();
            Assert.IsTrue(rig.Do(new PressBrakeKey(rig.Id)).Accepted);
            Assert.IsTrue(rig.Flight.IsBraking);
            Assert.IsFalse(rig.Do(new ReleaseShieldKey(rig.Id)).Accepted, "in flight the release does nothing");
            Assert.IsTrue(rig.Flight.IsBraking);

            rig.Landed();
            Assert.IsTrue(rig.Do(new PressBrakeKey(rig.Id)).Accepted);
            Assert.IsTrue(rig.Flight.IsShielding);
            Assert.IsTrue(rig.Do(new ReleaseShieldKey(rig.Id)).Accepted);
            Assert.IsFalse(rig.Flight.IsShielding);
        }

        [Test]
        public void VelocityIsSpeedAlongTheGmlDirection()
        {
            var rig = new PlayerRig();
            var v = rig.Flight.Velocity;
            Assert.AreEqual(0f, v.X, 1e-4f);
            Assert.AreEqual(-rig.Flight.Speed, v.Y, 1e-4f);
        }

        // ---- the content registry's online section ----

        [Test]
        public void TheOnlineSectionLoadsAndValidates()
        {
            var registry = ContentRegistry.FromJson("{ \"online\": { \"MaxPlayers\": 2, \"SpawnSpacing\": 3 } }");
            Assert.AreEqual(2, registry.Online.MaxPlayers);
            Assert.AreEqual(3f, registry.Online.SpawnSpacing);
            Assert.AreEqual(0.5f, registry.Online.AimResendDegrees);

            var bad = Assert.Throws<ContentValidationException>(() => ContentRegistry.FromJson("{ \"online\": { \"MaxPlayers\": 0, \"SpawnSpacing\": -1 } }"));
            StringAssert.Contains("online.MaxPlayers", bad.Message);
            StringAssert.Contains("online.SpawnSpacing", bad.Message);
        }
    }
}
