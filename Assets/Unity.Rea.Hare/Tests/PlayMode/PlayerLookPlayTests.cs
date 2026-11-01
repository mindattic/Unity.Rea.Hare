using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Unity.Rea.Hare.Art;
using Unity.Rea.Hare.Effects;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Rocks;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T55, played: under Unity's own loop the ship flies in drawn as spr_rabbit_leap trailing its orange spawn
    /// jet and white smoke, sparks amber, pink and cornflower while a real D press turns it, and once perched on a rock
    /// is drawn as spr_rabbit_idle holding the vulcan at gunX/gunY.
    /// </summary>
    public class PlayerLookPlayTests : InputTestFixture
    {
        Keyboard keyboard;
        GameObject shipObject;
        Rock rock;
        readonly List<GmlEffectBurst> effects = new List<GmlEffectBurst>();

        void OnEffect(GmlEffectBurst burst) => effects.Add(burst);

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            Gml.Paused = false;
            effects.Clear();
            GmlEffects.Created += OnEffect;
        }

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            GmlEffects.Created -= OnEffect;
            if (shipObject != null) UnityEngine.Object.Destroy(shipObject);
            if (rock != null) UnityEngine.Object.Destroy(rock.gameObject);
            foreach (var burst in UnityEngine.Object.FindObjectsByType<GmlEffectBurst>(FindObjectsSortMode.None)) UnityEngine.Object.Destroy(burst.gameObject);
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

        static Sprite Frame(string name) => HareArtManifest.Load().Frame(name);

        [UnityTest]
        public IEnumerator TheShipFliesInAsTheRabbitTrailingSmokeAndSparksWhenTurned()
        {
            shipObject = PlayerShipBuilder.Build(new Vector3(0f, 2f, 0f));
            var look = shipObject.GetComponent<PlayerLook>();
            yield return null;

            Assert.IsTrue(look.Body.enabled);
            Assert.AreSame(Frame(ShipPose.Leap), look.Body.sprite, "flying in as spr_rabbit_leap");
            Assert.IsFalse(look.Gun.enabled);

            yield return WaitFor(() => effects.Any(e => e != null && e.Kind == GmlEffectKind.Smoke), "the justspawned jet");
            Assert.AreEqual(PlayerLook.Orange, effects.First(e => e != null && e.Kind == GmlEffectKind.Smoke).Color);
            yield return WaitFor(() => effects.Any(e => e != null && e.Kind == GmlEffectKind.SmokeUp), "the smoke trail");

            yield return null;
            Press(keyboard.dKey, queueEventOnly: true);
            yield return WaitFor(() => effects.Any(e => e != null && e.Kind == GmlEffectKind.Spark), "a spark while turning");
            var palette = new[] { ShipColor.Amber, ShipColor.Pink, ShipColor.CornflowerBlue }.Select(PlayerHealth.ColorOf).ToList();
            Assert.That(palette, Does.Contain(effects.First(e => e != null && e.Kind == GmlEffectKind.Spark).Color));
            yield return null;
            Release(keyboard.dKey, queueEventOnly: true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PerchedOnARockItHoldsTheVulcanAtTheGun()
        {
            rock = Rock.Create(Vector2.zero, 100, null, 0);
            shipObject = PlayerShipBuilder.Build(new Vector3(0f, 2f, 0f));
            var ship = shipObject.GetComponent<PlayerShipController>();
            var look = shipObject.GetComponent<PlayerLook>();

            yield return WaitFor(() => ship.State == PlayerFlightState.Idle, "the ship to land on the rock below it");
            yield return null;
            look.Draw(); // a coroutine resumes before LateUpdate: draw this frame's state now
            Assert.AreSame(Frame(ShipPose.Idle), look.Body.sprite, "perched: spr_rabbit_idle");
            Assert.IsTrue(look.Gun.enabled);
            Assert.AreSame(Frame("spr_weapon_vulcan"), look.Gun.sprite);
            Assert.That(Vector2.Distance(look.GunPosition, look.Gun.transform.position), Is.LessThan(1e-3f));
            Assert.AreEqual(Mathf.Repeat(ship.AimDirection, 360f), Mathf.Repeat(look.Gun.transform.eulerAngles.z, 360f), 0.01f,
                "turned to mouse_angle");
        }
    }
}
