using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Unity.Rea.Hare.Effects;
using Unity.Rea.Hare.Enemies;
using Unity.Rea.Hare.Helper;
using Unity.Rea.Hare.Pickups;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Tests.EditMode
{
    /// <summary>
    /// U.HARE-T66: the helper hawk in Unity: create_helper at the ship with sfx_hawk, spr_helper drawn where Core has it
    /// and turned to its image_angle, its first ring and gold smoke, a carrot or coin its mask touches taken for its ship,
    /// and Delete's obj_rocket_explosion with the black ef_smokeup and orange ef_firework.
    /// </summary>
    public class HelperViewTests
    {
        const float Step = 1f / 120f;

        readonly List<HelperEvent> played = new List<HelperEvent>();
        readonly List<PickupEvent> pickups = new List<PickupEvent>();
        readonly List<string> sounds = new List<string>();
        readonly List<GmlEffectBurst> effects = new List<GmlEffectBurst>();

        void OnEvent(HelperEvent e) => played.Add(e);
        void OnPickup(PickupEvent e) => pickups.Add(e);
        void OnSound(string s) => sounds.Add(s);
        void OnEffect(GmlEffectBurst b) => effects.Add(b);

        [SetUp]
        public void SetUp()
        {
            HelperDirector.Reset(66);
            PickupDirector.Reset(2323);
            played.Clear();
            pickups.Clear();
            sounds.Clear();
            effects.Clear();
            HelperDirector.EventPlayed += OnEvent;
            HelperDirector.SoundPlayed += OnSound;
            PickupDirector.EventPlayed += OnPickup;
            GmlEffects.Created += OnEffect;
        }

        [TearDown]
        public void TearDown()
        {
            HelperDirector.EventPlayed -= OnEvent;
            HelperDirector.SoundPlayed -= OnSound;
            PickupDirector.EventPlayed -= OnPickup;
            GmlEffects.Created -= OnEffect;
            EnemyTestKit.Cleanup(); // ships, effects, explosions, pickups, helpers and their directors
        }

        static PlayerShipController BuildShip(Vector2 at) =>
            PlayerShipBuilder.Build(at).GetComponent<PlayerShipController>();

        static void Steps(int n)
        {
            for (int i = 0; i < n; i++) HelperDirector.Step(Step);
        }

        [Test]
        public void AShipCallsOneHelperAtItselfWithSfxHawk()
        {
            var ship = BuildShip(new Vector2(1f, 1f));
            Assert.IsTrue(ship.CallHelper());
            Assert.IsFalse(ship.CallHelper(), "only one at a time");

            var helper = played.OfType<HelperCalled>().Single().Helper;
            Assert.AreEqual(PlayerSeat.Of(ship.gameObject).Id, helper.Owner);
            Assert.AreEqual(ship.Facing, helper.Direction, 1e-4f, "direction = player.image_angle");
            CollectionAssert.AreEqual(new[] { "sfx_hawk" }, sounds);

            var view = HelperDirector.ViewOf(helper.Id);
            Assert.IsNotNull(view);
            Assert.AreEqual("obj_helper", view.name);
            Assert.AreEqual(HareArt.SortingOrderForDepth(0), view.Renderer.sortingOrder);
            Assert.IsNull(view.GetComponent<Collider2D>(), "nothing collides with obj_helper");
            Assert.AreEqual(0.16f, view.MaskRadius, 1e-5f, "spr_helper's 32 px ellipse");
        }

        [Test]
        public void ItFliesWhereCoreHasItRingingThenSmokingGold()
        {
            var ship = BuildShip(Vector2.zero);
            ship.CallHelper();
            var helper = played.OfType<HelperCalled>().Single().Helper;
            Steps(3);

            var view = HelperDirector.ViewOf(helper.Id);
            Assert.That(Vector2.Distance(view.transform.position, helper.Position.ToVector2()), Is.LessThan(1e-5f));
            Assert.AreEqual(Gml.Px(12f), helper.Position.ToVector2().magnitude, 1e-4f, "4 px a step");
            Assert.AreEqual(helper.ImageAngle, view.transform.eulerAngles.z, 1e-3f);
            var ring = effects.Single(e => e.Kind == GmlEffectKind.Ring);
            Assert.AreEqual(1, ring.Size);
            Assert.AreEqual(Color.white, ring.Color);
            Assert.AreEqual(3, effects.Count(e => e.Kind == GmlEffectKind.Smoke && e.Color == PickupDirector.Gold));
        }

        [Test]
        public void ACoinTheHelperTouchesGoesToItsShip()
        {
            var ship = BuildShip(new Vector2(-10f, 0f)); // only the helper steps here, so only it can take the coin
            ship.CallHelper();
            var helper = played.OfType<HelperCalled>().Single().Helper;
            Vector2 ahead = helper.Position.ToVector2() + Gml.Dir(helper.Direction) * Gml.Px(8f);
            int id = ((PickupDropped)PickupDirector.Drop(PickupKind.Coin, ahead).Events.Single()).Pickup.Id;

            Steps(1);

            var taken = pickups.OfType<PickupCollected>().Single();
            Assert.AreEqual(id, taken.Pickup.Id);
            Assert.AreEqual(PlayerSeat.Of(ship.gameObject).Id, taken.By, "COINS for the helper's ship");
            Assert.IsNull(PickupDirector.ViewOf<PickupView>(id));
        }

        [Test]
        public void DeleteSetsItOffAsARocketExplosion()
        {
            var ship = BuildShip(Vector2.zero);
            ship.CallHelper();
            var helper = played.OfType<HelperCalled>().Single().Helper;
            Steps(2);
            int exploded = 0;
            void Count(RocketExplosion _) => exploded++;
            RocketExplosion.Exploded += Count;
            try
            {
                Assert.IsTrue(HelperDirector.Execute(new DetonateHelper(helper.Owner)).Accepted);
            }
            finally
            {
                RocketExplosion.Exploded -= Count;
            }

            Assert.AreEqual(1, exploded, "instance_create(other.x, other.y, obj_rocket_explosion)");
            Assert.IsTrue(effects.Any(e => e.Kind == GmlEffectKind.SmokeUp && e.Above && e.Color == Color.black));
            Assert.IsTrue(effects.Any(e => e.Kind == GmlEffectKind.Firework && e.Above && e.Color == DeathEffects.Orange));
            Assert.IsNull(HelperDirector.ViewOf(helper.Id), "instance_destroy()");
            Assert.IsTrue(ship.CallHelper(), "a new one can be called");
        }
    }
}
