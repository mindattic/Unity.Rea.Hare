using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Player;

namespace Unity.Hare.Tests.EditMode
{
    public class PlayerShipControllerTests
    {
        PlayerShipController NewController()
        {
            var go = new GameObject("Player");
            var player = go.AddComponent<PlayerShipController>();
            player.ResetState(); // Awake doesn't run outside Play Mode; see PlayerShipController.ResetState
            return player;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in GameObject.FindObjectsByType<PlayerShipController>(FindObjectsSortMode.None))
                Object.DestroyImmediate(go.gameObject);
        }

        [Test]
        public void StartsInMovingStateAtFullEnergy()
        {
            var player = NewController();
            Assert.AreEqual(PlayerFlightState.Moving, player.State);
            Assert.AreEqual(100f, player.Energy);
        }

        [Test]
        public void BoostDoublesSpeedWhenEnergyIsSufficient()
        {
            var player = NewController();
            Assert.IsTrue(player.TryBoost());
            Assert.IsTrue(player.IsBoosting);
            Assert.AreEqual(10f, player.Speed); // flight_speed(5) * 2, GML player_boost
        }

        [Test]
        public void BoostAndBrakeAreMutuallyExclusive()
        {
            var player = NewController();
            Assert.IsTrue(player.TryBoost());
            Assert.IsFalse(player.TryBrake());

            var other = NewController();
            Assert.IsTrue(other.TryBrake());
            Assert.IsFalse(other.TryBoost());
        }

        [Test]
        public void LeapFailsWhileMoving()
        {
            var player = NewController();
            Assert.IsFalse(player.TryLeap(270f));
        }

        [Test]
        public void LeapFailsOutsideJumpArc()
        {
            var player = NewController();
            player.Land();
            // Facing (Direction) starts at 270; jump_arc is 100 degrees either side.
            Assert.IsFalse(player.TryLeap(270f - 180f));
        }

        [Test]
        public void LeapSucceedsWithinJumpArcAndEntersMoving()
        {
            var player = NewController();
            player.Land();
            Assert.AreEqual(PlayerFlightState.Idle, player.State);

            Assert.IsTrue(player.TryLeap(270f + 50f));
            Assert.AreEqual(PlayerFlightState.Moving, player.State);
            Assert.AreEqual(5f, player.Speed); // flight_speed, GML player_leap
        }
    }
}
