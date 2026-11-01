using NUnit.Framework;
using UnityEngine;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Rocks;
using Unity.Rea.Hare.Weapons;
using static Unity.Rea.Hare.Tests.EditMode.EnemyTestKit;

namespace Unity.Rea.Hare.Tests.EditMode
{
    /// <summary>
    /// U.HARE-T53: the ship on the rock field, through its view. A flying ship whose mask touches a rock perches on it
    /// (normal_detect, moved out of the rock, state_idle), rides it as it spins and drifts, leaps off, is flung when the
    /// rock leaves play or a rocket hits it there, and lets go when the rock is cleared away.
    /// </summary>
    public class PlayerLandingTests
    {
        [SetUp]
        public void SetUp()
        {
            Random.InitState(5353);
            Gml.Paused = false;
        }

        [TearDown]
        public void TearDown()
        {
            DestroyAll<Rock>();
            DestroyAll<Flotsum>();
            DestroyAll<ShieldWall>();
            Cleanup();
        }

        /// <summary>A ship flying straight down (reset_player's heading) at <paramref name="at"/>.</summary>
        static PlayerShipController FlyingShip(Vector2 at)
        {
            var ship = Ship(at);
            ship.ResetState();
            return ship;
        }

        /// <summary>A still rock (spr_rock1) at the origin spinning <paramref name="spin"/> degrees a step.</summary>
        static Rock StillRock(float spin = 0f)
        {
            var rock = Rock.Create(Vector2.zero, 100, null, 0);
            typeof(Rock).GetProperty(nameof(Rock.Spin)).SetValue(rock, spin);
            typeof(Rock).GetProperty(nameof(Rock.OriginalSpin)).SetValue(rock, spin);
            return rock;
        }

        static PlayerSeat Seat(PlayerShipController ship) => ship.GetComponent<PlayerSeat>();

        /// <summary>Flies a ship down onto the top of <paramref name="rock"/> and lands it.</summary>
        static PlayerShipController LandedOn(Rock rock)
        {
            var ship = FlyingShip(new Vector2(0f, 0.9f));
            ship.Step(Step);
            Assert.AreEqual(PlayerFlightState.Idle, ship.State, "the ship's mask touched the rock, so it landed");
            return ship;
        }

        [Test]
        public void AFlyingShipWhoseMaskTouchesARockPerchesOnItFacingAway()
        {
            var rock = StillRock();
            var ship = LandedOn(rock);

            Assert.AreSame(rock, ship.Perch, "current_rock");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(ship.Facing, 90f)), 30f, "image_angle = normal_detect's normal, up off the rock's top");
            Assert.AreEqual(ship.Facing, Mathf.Repeat(ship.transform.eulerAngles.z, 360f), 1e-3f, "drawn at image_angle");
            Assert.IsFalse(PerchProbe.PerchMaskTouches(ship.transform.position, ship.Facing, rock.GetComponent<Collider2D>()),
                "moved out of collision along the normal");

            var flight = Seat(ship).Ship.Flight;
            Vector2 toCentre = rock.Position - (Vector2)ship.transform.position;
            Assert.AreEqual(toCentre.magnitude, flight.DistanceToCenter, 1e-4f, "distance_to_center");
            Assert.AreEqual(0f, Mathf.DeltaAngle(Mathf.Atan2(toCentre.y, toCentre.x) * Mathf.Rad2Deg, flight.LandAngle), 1e-3f, "land_angle");
            Assert.AreEqual(Vector2.zero, ship.GetComponent<Rigidbody2D>().linearVelocity);
        }

        [Test]
        public void AShipClearOfTheRockFliesOn()
        {
            StillRock();
            var ship = FlyingShip(new Vector2(0f, 4f));
            ship.Step(Step);
            Assert.AreEqual(PlayerFlightState.Moving, ship.State);
            Assert.IsNull(ship.Perch);
        }

        [Test]
        public void ARockOutOfViewIsFlownThrough()
        {
            StillRock();
            var ship = FlyingShip(new Vector2(0f, 0.9f));
            ship.InViewCheck = world => PerchProbe.InView(world.x, world.y, 40f, 40f, 19.2f, 10.8f); // view 0 far off
            ship.Step(Step);
            Assert.AreEqual(PlayerFlightState.Moving, ship.State, "par_rock's collision needs in_view(other.x, other.y)");

            ship.InViewCheck = world => PerchProbe.InView(world.x, world.y, -9.6f, -5.4f, 19.2f, 10.8f); // view 0 on it
            ship.Step(Step);
            Assert.AreEqual(PlayerFlightState.Idle, ship.State);
        }

        [Test]
        public void ThePerchedShipRidesItsRockAsItSpinsAndDrifts()
        {
            var rock = StillRock(0.5f);
            var ship = LandedOn(rock);
            var flight = Seat(ship).Ship.Flight;
            float landAngle = flight.LandAngle, facing = ship.Facing, distance = flight.DistanceToCenter;
            rock.SetMotion(0f, 1f);

            for (int i = 0; i < 30; i++)
            {
                rock.Tick(Step);
                ship.Step(Step);
            }

            Assert.AreEqual(Mathf.Repeat(landAngle + 15f, 360f), flight.LandAngle, 1e-2f, "land_angle += current_rock.spin, 30 steps");
            Assert.AreEqual(Mathf.Repeat(facing + 15f, 360f), ship.Facing, 1e-2f, "image_angle += current_rock.spin");
            float a = flight.LandAngle * Mathf.Deg2Rad;
            Vector2 expected = rock.Position - new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * distance;
            Assert.AreEqual(expected.x, ship.transform.position.x, 1e-4f, "x = current_rock.x - lengthdir_x(distance_to_center, land_angle)");
            Assert.AreEqual(expected.y, ship.transform.position.y, 1e-4f);
            Assert.Greater(rock.Position.x, 0.25f, "the rock drifted and took the ship with it");
            Assert.AreEqual(PlayerFlightState.Idle, ship.State);
        }

        [Test]
        public void LeapingTakesOffAlongTheAimAndDoesNotLandStraightBack()
        {
            var rock = StillRock();
            var ship = LandedOn(rock);
            float aim = ship.Facing + 40f;

            Assert.IsTrue(ship.TryLeap(aim));
            Assert.IsNull(ship.Perch);
            ship.Step(Step);
            Assert.AreEqual(PlayerFlightState.Moving, ship.State, "it takes off before it can land again");
            Assert.AreEqual(Mathf.Repeat(aim, 360f), ship.Facing, 1e-3f);
            Assert.Greater(ship.GetComponent<Rigidbody2D>().linearVelocity.magnitude, 0f);
        }

        [Test]
        public void ARockLeavingTheRoomFlingsTheShipOffAlongItsFacing()
        {
            var rock = StillRock();
            var ship = LandedOn(rock);
            float facing = ship.Facing;
            bool flung = false;
            ship.Flung += () => flung = true;

            rock.transform.position = new Vector3(Gml.Room.xMax + 10f, 0f, 0f);
            rock.Tick(Step);

            Assert.IsTrue(flung, "par_rock's room-edge check runs fling_player for the ship's rock");
            Assert.AreEqual(PlayerFlightState.Moving, ship.State);
            Assert.AreEqual(facing, Mathf.Repeat(ship.Direction, 360f), 1e-3f, "direction = image_angle");
            Assert.Greater(ship.Spin, 0f, "spin = 3");
            Assert.IsNull(ship.Perch);
        }

        [Test]
        public void AClearedRockIsLetGoOfWithoutAFling()
        {
            var rock = StillRock();
            var ship = LandedOn(rock);
            rock.Clear();

            Assert.AreEqual(PlayerFlightState.Idle, ship.State);
            Assert.IsNull(ship.Perch);
            Assert.IsNull(Seat(ship).Ship.Flight.CurrentRock);
        }

        [Test]
        public void ARocketHitFlingsAPerchedShipAndABulletDoesNot()
        {
            var rock = StillRock();
            var ship = LandedOn(rock);
            var health = ship.gameObject.AddComponent<PlayerHealth>();
            health.ResetState();
            Seat(ship).Send(new TickHealth(Seat(ship).Id, 2f)); // past the respawn protection

            Assert.IsTrue(health.TryTakeHit());
            Assert.AreEqual(PlayerFlightState.Idle, ship.State, "obj_bullet / par_enemy leave a perched ship where it is");

            Seat(ship).Send(new TickHealth(Seat(ship).Id, 2f)); // past the invulnerability
            Assert.IsTrue(health.TryTakeHit(reaction: HitReaction.SpinOrFling));
            Assert.AreEqual(PlayerFlightState.Moving, ship.State, "obj_rocket flings a ship perched on a rock");
            Assert.IsNull(ship.Perch);
        }

        [Test]
        public void HoldingSpinTurnsTheShipButNotItsHeading()
        {
            var ship = FlyingShip(new Vector2(0f, 4f));
            Seat(ship).Send(new PressSpinKey(Seat(ship).Id));
            for (int i = 0; i < 10; i++) ship.Step(Step);

            Assert.AreEqual(300f, ship.Facing, 1e-2f);
            Assert.AreEqual(300f, Mathf.Repeat(ship.transform.eulerAngles.z, 360f), 1e-2f, "drawn spinning");
            Vector2 velocity = ship.GetComponent<Rigidbody2D>().linearVelocity;
            Assert.AreEqual(270f, Mathf.Repeat(Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg, 360f), 1e-2f, "still flying down");
        }
    }
}
