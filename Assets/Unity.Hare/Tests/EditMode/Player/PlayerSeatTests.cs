using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>
    /// The glue between the thin views and Core: commands go through a seat, the events come back to the views.
    /// The rules themselves are tested in Tests/Core.Tests.
    /// </summary>
    public class PlayerSeatTests
    {
        static PlayerShipController NewShip(string name = "Player")
        {
            var go = new GameObject(name);
            var ship = go.AddComponent<PlayerShipController>();
            ship.ResetState(); // Awake doesn't run outside Play Mode
            return ship;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var p in Object.FindObjectsByType<PlayerShipController>(FindObjectsSortMode.None))
                Object.DestroyImmediate(p.gameObject);
            foreach (var w in Object.FindObjectsByType<ShieldWall>(FindObjectsSortMode.None))
                Object.DestroyImmediate(w.gameObject);
        }

        [Test]
        public void EveryShipHasItsOwnSeatAndWorld()
        {
            var a = NewShip("A");
            var b = NewShip("B");
            a.Land();
            Assert.AreEqual(PlayerFlightState.Idle, a.State);
            Assert.AreEqual(PlayerFlightState.Moving, b.State, "landing one ship leaves the other flying");
            Assert.AreNotSame(PlayerSeat.Of(a.gameObject).World, PlayerSeat.Of(b.gameObject).World);
        }

        [Test]
        public void ViewsOnTheSameShipShareOneSeat()
        {
            var ship = NewShip();
            var health = ship.gameObject.AddComponent<PlayerHealth>();
            health.ResetState();
            Assert.AreSame(PlayerSeat.Of(ship.gameObject), PlayerSeat.Of(health.gameObject));
        }

        [Test]
        public void LandingAndLeapingRaiseTheViewsEvents()
        {
            var ship = NewShip();
            int landed = 0, leapt = 0;
            ship.Landed += () => landed++;
            ship.Leapt += () => leapt++;

            ship.Land();
            Assert.AreEqual(1, landed);
            Assert.IsTrue(ship.TryLeap(ship.Direction));
            Assert.AreEqual(1, leapt);
        }

        [Test]
        public void ATakenHitReachesTheHealthViewsEventsAndDeathStopsTheShip()
        {
            var ship = NewShip();
            var health = ship.gameObject.AddComponent<PlayerHealth>();
            health.ResetState();
            int damaged = -1, lives = -1;
            health.Damaged += hp => damaged = hp;
            health.Died += l => lives = l;

            health.Tick(2f); // past spawn protection
            Assert.IsTrue(health.TryTakeHit());
            Assert.AreEqual(5, damaged);

            for (int i = 0; i < 5; i++)
            {
                health.Tick(2f);
                health.TryTakeHit();
            }
            Assert.AreEqual(2, lives);
            Assert.AreEqual(PlayerFlightState.Dead, ship.State);
        }

        [Test]
        public void TheShieldWallFollowsTheRulesNotTheOtherWayRound()
        {
            var ship = NewShip();
            ship.Land();
            Assert.IsTrue(ship.TryShield());
            Assert.IsNotNull(ship.Shield);

            ship.DropShield();
            Assert.IsNull(ship.Shield);
            Assert.AreEqual(0, Object.FindObjectsByType<ShieldWall>(FindObjectsSortMode.None).Length);
        }

        [Test]
        public void AWeaponComponentInstallsItsGunOnTheSeat()
        {
            var ship = NewShip();
            var vulcan = ship.gameObject.AddComponent<VulcanWeapon>();
            vulcan.ResetState();
            var seat = PlayerSeat.Of(ship.gameObject);
            Assert.AreEqual(1, seat.Ship.Guns.Guns.Count);
            Assert.AreEqual(GunType.Vulcan, seat.Ship.Guns.Guns[0].Type);
        }
    }
}
