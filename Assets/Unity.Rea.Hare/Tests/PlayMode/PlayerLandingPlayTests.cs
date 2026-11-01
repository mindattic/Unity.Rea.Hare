using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Rocks;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T53, played: under Unity's own loop the ship flies onto a rock and perches on it, rides it, and real Space
    /// presses crouch it and, on the release, leap it off; Alt held in flight spins it and the facing winds back after.
    /// </summary>
    public class PlayerLandingPlayTests : InputTestFixture
    {
        Keyboard keyboard;
        GameObject shipObject;
        Rock rock;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            Gml.Paused = false;
        }

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            if (shipObject != null) UnityEngine.Object.Destroy(shipObject);
            if (rock != null) UnityEngine.Object.Destroy(rock.gameObject);
            foreach (var flotsum in UnityEngine.Object.FindObjectsByType<Flotsum>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(flotsum.gameObject);
            yield return null;
        }

        static IEnumerator WaitFor(Func<bool> condition, string what, float seconds = 3f)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > until) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        /// <summary>
        /// Presses from the frame's Update phase (a coroutine resumed by WaitForFixedUpdate is in the fixed step, where the
        /// test runtime's dynamic-update state is not current), then lets the ship's Update read it.
        /// </summary>
        IEnumerator Hold(ButtonControl key)
        {
            yield return null;
            Press(key, queueEventOnly: true);
            yield return null;
            yield return null;
        }

        IEnumerator LetGo(ButtonControl key)
        {
            yield return null;
            Release(key, queueEventOnly: true);
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheShipLandsOnARockRidesItCrouchesAndLeapsOffOnSpace()
        {
            rock = Rock.Create(Vector2.zero, 100, null, 0);
            shipObject = PlayerShipBuilder.Build(new Vector3(0f, 2f, 0f));
            var ship = shipObject.GetComponent<PlayerShipController>();
            Assert.AreEqual(PlayerFlightState.Moving, ship.State, "reset_player: flying in, heading down");

            yield return WaitFor(() => ship.State == PlayerFlightState.Idle, "the ship to land on the rock below it");
            Assert.AreSame(rock, ship.Perch);
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(ship.Facing, 90f)), 45f, "perched facing up off the rock");
            float distance = Vector2.Distance(ship.transform.position, rock.Position);

            rock.SetMotion(0f, 1f);
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();
            Assert.AreEqual(distance, Vector2.Distance(ship.transform.position, rock.Position), 0.02f, "it rides the drifting rock");
            Assert.AreEqual(PlayerFlightState.Idle, ship.State);
            rock.SetMotion(0f, 0f);

            yield return Hold(keyboard.spaceKey);
            Assert.IsTrue(ship.IsCrouching, "Space down while perched: crouching");
            Assert.IsFalse(ship.IsShielding, "Space is not the shield key");

            ship.SetAimDirection(ship.Facing);
            yield return LetGo(keyboard.spaceKey);
            Assert.AreEqual(PlayerFlightState.Moving, ship.State, "Space up: player_leap");
            Assert.IsFalse(ship.IsCrouching);
            Assert.IsNull(ship.Perch);

            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.AreEqual(PlayerFlightState.Moving, ship.State, "it flies off rather than landing straight back");
            Assert.Greater(Vector2.Distance(ship.transform.position, rock.Position), distance + 0.1f);
        }

        [UnityTest]
        public IEnumerator AltSpinsTheShipInFlightAndItWindsBackOnRelease()
        {
            shipObject = PlayerShipBuilder.Build(new Vector3(0f, 2f, 0f));
            var ship = shipObject.GetComponent<PlayerShipController>();
            yield return null; // the ship's first Update enables its controls

            yield return Hold(keyboard.leftAltKey);
            Assert.Greater(ship.Spin, 0f, "spin = 3 while Alt is held in flight");
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(ship.Facing, ship.Direction)), 5f, "the ship turns, its heading does not");

            yield return LetGo(keyboard.leftAltKey);
            Assert.AreEqual(0f, ship.Spin);
            yield return WaitFor(() => !ship.WasSpinning, "the facing to wind back onto the heading");
            Assert.AreEqual(0f, Mathf.DeltaAngle(ship.Facing, ship.Direction), 1e-2f);
        }
    }
}
