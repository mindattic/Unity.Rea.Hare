using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Hare.Input;
using Unity.Hare.Player;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>
    /// U.HARE-T42: HareControls.inputactions reads the bindings the five views (PlayerShipController,
    /// AirWeaponLoadout, GroundWeaponLoadout, PerchedWeapon, VulcanWeapon) used to read as raw
    /// Keyboard.current/Mouse.current polling. These tests simulate real device input (InputTestFixture) against
    /// the generated wrapper itself, independent of any view, to prove the bindings do what the views assume.
    /// </summary>
    public class HareControlsTests : InputTestFixture
    {
        Keyboard keyboard;
        Mouse mouse;
        Gamepad gamepad;
        HareControls controls;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            controls = new HareControls();
            controls.Player.Enable();
        }

        public override void TearDown()
        {
            controls.Player.Disable();
            // Dispose() calls Object.Destroy(asset), which only works in Play mode; EditMode tests never run
            // there, so tear the asset down directly the way PlayerShipController.OnDestroy does (U.HARE-T42).
            Object.DestroyImmediate(controls.asset);
            controls = null;
            // HareControls's generated finalizer logs an error if its Player/Meta map is still enabled when
            // collected; without this, that log can land on a later, unrelated test whenever the GC happens to
            // run it. Forcing collection here keeps it attributed to this test (which has already disabled it).
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            base.TearDown();
        }

        [Test]
        public void ARotatesCounterclockwiseAndDClockwise()
        {
            Press(keyboard.aKey);
            Assert.AreEqual(1f, controls.Player.Rotate.ReadValue<float>());
            Release(keyboard.aKey);

            Press(keyboard.dKey);
            Assert.AreEqual(-1f, controls.Player.Rotate.ReadValue<float>());
        }

        [Test]
        public void ArrowKeysMatchAAndD()
        {
            Press(keyboard.leftArrowKey);
            Assert.AreEqual(1f, controls.Player.Rotate.ReadValue<float>(), "left arrow matches A (counterclockwise)");
            Release(keyboard.leftArrowKey);

            Press(keyboard.rightArrowKey);
            Assert.AreEqual(-1f, controls.Player.Rotate.ReadValue<float>(), "right arrow matches D (clockwise)");
        }

        [Test]
        public void BothRotateKeysCancelToZero()
        {
            Press(keyboard.aKey);
            Press(keyboard.dKey);
            Assert.AreEqual(0f, controls.Player.Rotate.ReadValue<float>());
        }

        [Test]
        public void GamepadStickMatchesKeyboardTurnDirection()
        {
            Set(gamepad.leftStick, new Vector2(-1f, 0f));
            Assert.Greater(controls.Player.Rotate.ReadValue<float>(), 0f, "left on the stick should turn the ship the same way A does");

            Set(gamepad.leftStick, new Vector2(1f, 0f));
            Assert.Less(controls.Player.Rotate.ReadValue<float>(), 0f, "right on the stick should turn the ship the same way D does");
        }

        [Test]
        public void WTriggersBoost()
        {
            Press(keyboard.wKey);
            Assert.IsTrue(controls.Player.Boost.WasPressedThisFrame());
        }

        /// <summary>
        /// U.HARE-T53: Space and the right mouse button are obj_player's leap keys (crouch or boost/brake on the press,
        /// player_leap on the release), not boost: Space no longer raises the shield while perched.
        /// </summary>
        [Test]
        public void SpaceOrRightMouseIsTheLeapKeyNotBoost()
        {
            Press(keyboard.spaceKey);
            Assert.IsTrue(controls.Player.Leap.WasPressedThisFrame());
            Assert.IsFalse(controls.Player.Boost.IsPressed(), "Space is not the boost key any more");
            Release(keyboard.spaceKey);
            Assert.IsTrue(controls.Player.Leap.WasReleasedThisFrame(), "player_leap runs on the release");

            Press(mouse.rightButton);
            Assert.IsTrue(controls.Player.Leap.WasPressedThisFrame());
            Assert.IsInstanceOf<Mouse>(controls.Player.Leap.activeControl.device, "the press can tell the mouse's brake from Space's boost");
        }

        [Test]
        public void AltOrMiddleMouseSpins()
        {
            Press(keyboard.leftAltKey);
            Assert.IsTrue(controls.Player.Spin.WasPressedThisFrame());
            Release(keyboard.leftAltKey);
            Assert.IsTrue(controls.Player.Spin.WasReleasedThisFrame());

            Press(mouse.middleButton);
            Assert.IsTrue(controls.Player.Spin.IsPressed());
        }

        [Test]
        public void CtrlHoldsAlternate()
        {
            Press(keyboard.leftCtrlKey);
            Assert.IsTrue(controls.Player.Alternate.IsPressed());
        }

        [Test]
        public void SOrLeftShiftTriggersBrake()
        {
            Press(keyboard.sKey);
            Assert.IsTrue(controls.Player.Brake.WasPressedThisFrame());
            Release(keyboard.sKey);

            Press(keyboard.leftShiftKey);
            Assert.IsTrue(controls.Player.Brake.WasPressedThisFrame());
        }

        [Test]
        public void LeftMouseAloneTriggersFirePrimaryOnly()
        {
            Press(mouse.leftButton);
            Assert.IsTrue(controls.Player.FirePrimary.IsPressed());
            Assert.IsFalse(controls.Player.FireSecondary.IsPressed());
        }

        [Test]
        public void CtrlPlusLeftMouseTriggersFireSecondary()
        {
            Press(keyboard.leftCtrlKey);
            Press(mouse.leftButton);
            Assert.IsTrue(controls.Player.FireSecondary.IsPressed());
        }

        [Test]
        public void QTriggersNextGroundWeapon()
        {
            Press(keyboard.qKey);
            Assert.IsTrue(controls.Player.NextGroundWeapon.WasPressedThisFrame());
        }

        [Test]
        public void ETriggersNextAirWeapon()
        {
            Press(keyboard.eKey);
            Assert.IsTrue(controls.Player.NextAirWeapon.WasPressedThisFrame());
        }
    }

    /// <summary>U.HARE-T42: PlayerShipController's lazily-created HareControls must tear down safely outside Play
    /// mode too (EditMode tests destroy a ship with DestroyImmediate, which still runs OnDestroy synchronously).</summary>
    public class PlayerShipControllerInputDisposalTests
    {
        [Test]
        public void DestroyingAShipThatTouchedControlsDoesNotLogAnErrorInEditMode()
        {
            var go = new GameObject("Player");
            var ship = go.AddComponent<PlayerShipController>();
            ship.ResetState();
            _ = ship.Controls; // force the lazy HareControls into existence, as Update() would in Play mode

            // Object.Destroy (what HareControls.Dispose() calls) logs an error outside Play mode instead of
            // throwing; the test framework fails the test on that log unless OnDestroy avoids it (U.HARE-T42).
            Object.DestroyImmediate(go);

            // See HareControlsTests.TearDown: force the generated wrapper's finalizer to run now, attributed to
            // this test, rather than letting it land on some later, unrelated one.
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
        }
    }
}
