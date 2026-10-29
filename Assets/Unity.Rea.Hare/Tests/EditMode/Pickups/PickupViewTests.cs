using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Enemies;
using Unity.Hare.Pickups;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>
    /// U.HARE-T23: the pickups and scraps in Unity: a carrot or coin drawn where Core has it with its sprite's mask, a ship
    /// whose mask touches one takes it (HP, the ef_ring, sfx_powerup), the magnet, an enemy kill's random_pickup, the
    /// vulcan's cartridge, the shotgun's shell and player_leap's kicked-up dirt.
    /// </summary>
    public class PickupViewTests
    {
        const float Step = 1f / 120f;

        readonly List<PickupEvent> played = new List<PickupEvent>();
        readonly List<string> sounds = new List<string>();
        readonly List<GmlEffectBurst> effects = new List<GmlEffectBurst>();

        void OnEvent(PickupEvent e) => played.Add(e);
        void OnSound(string s) => sounds.Add(s);
        void OnEffect(GmlEffectBurst b) => effects.Add(b);

        [SetUp]
        public void SetUp()
        {
            PickupDirector.Reset(2323);
            played.Clear();
            sounds.Clear();
            effects.Clear();
            PickupDirector.EventPlayed += OnEvent;
            PickupDirector.SoundPlayed += OnSound;
            GmlEffects.Created += OnEffect;
        }

        [TearDown]
        public void TearDown()
        {
            PickupDirector.EventPlayed -= OnEvent;
            PickupDirector.SoundPlayed -= OnSound;
            GmlEffects.Created -= OnEffect;
            EnemyTestKit.Cleanup(); // ships, enemies, effects, the pickups' and scraps' views, both directors
            EnemyTestKit.DestroyAll<Bullet>();
            EnemyTestKit.DestroyAll<MuzzleFlash>();
        }

        static PlayerShipController BuildShip(Vector2 at) =>
            PlayerShipBuilder.Build(at).GetComponent<PlayerShipController>();

        static void Steps(int n)
        {
            for (int i = 0; i < n; i++) PickupDirector.Step(Step);
        }

        [Test]
        public void ACoinIsDrawnWhereCoreHasItWithItsSpritesMask()
        {
            var id = Drop(PickupKind.Coin, new Vector2(5f, 5f));
            var view = PickupDirector.ViewOf<PickupView>(id);
            Assert.IsNotNull(view);
            StringAssert.StartsWith("spr_coin_", view.Pickup.SpriteName);
            var mask = view.GetComponent<CircleCollider2D>();
            Assert.IsTrue(mask.isTrigger);
            Assert.AreEqual(0.16f, mask.radius, 1e-5f, "spr_coin_*'s 32 px ellipse");

            Steps(10);
            Assert.AreEqual(5f - Gml.Px(10f), view.transform.position.y, 1e-4f, "y += 1 a step");
        }

        [Test]
        public void ACarrotsMaskIsItsSpritesBox()
        {
            var view = PickupDirector.ViewOf<PickupView>(Drop(PickupKind.Carrot, Vector2.zero));
            var box = view.GetComponent<BoxCollider2D>();
            Assert.AreEqual(new Vector2(0.14f, 0.24f), box.size);
            Assert.AreEqual(Vector2.zero, box.offset);
            Assert.AreEqual(HareArt.SortingOrderForDepth(0), view.Renderer.sortingOrder);
        }

        [Test]
        public void AShipTouchingACoinTakesItWithARingAndPowerUp()
        {
            var ship = BuildShip(Vector2.zero);
            int id = Drop(PickupKind.Coin, new Vector2(0.1f, 0f));
            var coin = PickupDirector.Field.FindPickup(id);
            Steps(1);

            var taken = played.OfType<PickupCollected>().Single();
            Assert.AreEqual(id, taken.Pickup.Id);
            Assert.AreEqual(PlayerSeat.Of(ship.gameObject).Id, taken.By);
            Assert.IsNull(PickupDirector.ViewOf<PickupView>(id), "instance_destroy()");
            var ring = effects.Single(e => e.Kind == GmlEffectKind.Ring);
            Assert.AreEqual(0, ring.Size);
            Color expected = coin.Grade == CoinGrade.Blue ? PickupDirector.CornflowerBlue
                : coin.Grade == CoinGrade.Silver ? PickupDirector.Silver : PickupDirector.Gold;
            Assert.AreEqual(expected, ring.Color);
            CollectionAssert.AreEqual(new[] { PickupDirector.PowerUpSound }, sounds, "no room flow, so no hundredth coin");
        }

        [Test]
        public void ACarrotGivesTheShipThatTakesItOneHp()
        {
            var ship = BuildShip(Vector2.zero);
            var health = ship.GetComponent<PlayerHealth>();
            Assert.IsTrue(health.TryTakeHit(true));
            var seat = PlayerSeat.Of(ship.gameObject);
            Assert.AreEqual(5, seat.Ship.Health.Health);

            Drop(PickupKind.Carrot, Vector2.zero);
            Steps(1);

            Assert.AreEqual(6, seat.Ship.Health.Health);
            var ring = effects.Single(e => e.Kind == GmlEffectKind.Ring);
            Assert.AreEqual(1, ring.Size);
            Assert.AreEqual(PickupDirector.Red, ring.Color);
            CollectionAssert.AreEqual(new[] { PickupDirector.PowerUpSound }, sounds);
        }

        [Test]
        public void AShipWithin48PxPullsAPickupInAtFourPxAStep()
        {
            BuildShip(Vector2.zero);
            int id = Drop(PickupKind.Carrot, new Vector2(Gml.Px(50f), 0f));
            var view = PickupDirector.ViewOf<PickupView>(id);
            Steps(1);
            Assert.AreEqual(Gml.Px(46f), view.transform.position.x, 1e-4f);
        }

        [Test]
        public void EnemyKillsDropRandomPickups()
        {
            var ship = EnemyTestKit.Ship(new Vector2(10f, 0f));
            for (int i = 0; i < 10; i++)
            {
                var saw = EnemyTestKit.Spawn<Sawblade>();
                saw.Init(new Vector2(-5f, i), Gml.Room, ship);
                saw.OnShieldHit(null, Vector2.right); // 10 points and random_pickup
            }
            var dropped = played.OfType<PickupDropped>().ToList();
            Assert.That(dropped.Count, Is.InRange(1, 10));
            Assert.IsTrue(dropped.All(d => Mathf.Approximately(d.Pickup.Position.X, -5f)), "random_pickup(x, y) at the kill");
            Assert.IsTrue(dropped.All(d => PickupDirector.ViewOf<PickupView>(d.Pickup.Id) != null));
        }

        [Test]
        public void TheVulcanLeavesACartridgeAtTheGun()
        {
            var go = new GameObject("Player");
            var ship = go.AddComponent<PlayerShipController>();
            ship.ResetState();
            var vulcan = go.AddComponent<VulcanWeapon>();
            vulcan.ResetState();
            ship.Land();
            ship.SetAimDirection(30f);
            int fired = 0;
            vulcan.Fired += _ => fired++;

            vulcan.PressTrigger();
            vulcan.Tick(Step);

            Assert.AreEqual(1, fired, "one round");
            Assert.AreEqual(1, played.Count, string.Join(", ", played.Select(p => p.GetType().Name)));
            var casing = played.OfType<ScrapThrown>().Single().Scrap;
            Assert.AreEqual(ScrapKind.Cartridge, casing.Kind);
            Assert.That(Vector2.Distance(casing.Position.ToVector2(), VulcanWeapon.GunPoint(go.transform)), Is.LessThan(1e-5f));
            var view = PickupDirector.ViewOf<ScrapView>(casing.Id);
            Assert.IsNotNull(view);
            Assert.IsNull(view.GetComponent<Collider2D>(), "nothing collides with it");
        }

        [Test]
        public void TheShotgunLeavesAShell()
        {
            var shotgun = PerchedWeaponTestUtil.NewPerchedWeapon<Shotgun>();
            Assert.IsTrue(shotgun.TryFire());
            var shell = played.OfType<ScrapThrown>().Single().Scrap;
            Assert.AreEqual(ScrapKind.Shell, shell.Kind);
            Assert.AreEqual("spr_ammo_shell", shell.SpriteName);
        }

        [Test]
        public void ALeapKicksUpGreyDirtAndChipsAtTheFeetItStoodOn()
        {
            var ship = BuildShip(Vector2.zero);
            ship.Land();
            float facing = ship.Facing;
            Assert.IsTrue(ship.TryLeap(facing));

            var firework = effects.Single(e => e.Kind == GmlEffectKind.Firework);
            Assert.AreEqual(PickupDirector.Gray, firework.Color);
            Vector2 feet = Gml.Dir(facing) * Gml.Px(32f);
            Assert.That(Vector2.Distance(firework.Origin, feet), Is.LessThan(1e-4f));
            var chips = played.OfType<ScrapThrown>().Select(t => t.Scrap).ToList();
            Assert.That(chips.Count, Is.InRange(6, 12));
            Assert.IsTrue(chips.All(c => c.Kind == ScrapKind.Chip && c.Scale == 0.5f));
            var view = PickupDirector.ViewOf<ScrapView>(chips[0].Id);
            Assert.AreEqual(HareArt.SortingOrderForDepth(1), view.Renderer.sortingOrder, "obj_chip's depth 1");

            Steps(3);
            Assert.That(Vector2.Distance(view.transform.position, chips[0].Position.ToVector2()), Is.LessThan(1e-5f));
        }

        [Test]
        public void TheRingIsTheRunnersEffect01()
        {
            var small = GmlEffects.Recipe(GmlEffectKind.Ring, 0);
            Assert.AreEqual(1, small.Count);
            Assert.AreEqual(GmlParticleShape.Ring, small.Type.Shape);
            Assert.AreEqual(0f, small.Type.SizeMin);
            Assert.AreEqual(0.15f * GmlEffects.SpeedFactor, small.Type.SizeIncrPerStep, 1e-7f);
            Assert.AreEqual(40f, small.Type.LifeMin, "Round(10 / 0.25)");
            Assert.AreEqual(48f, small.Type.LifeMax);
            Assert.AreEqual(1f, small.Type.AlphaStart);
            Assert.AreEqual(0f, small.Type.AlphaEnd);
            Assert.AreEqual(0.25f * GmlEffects.SpeedFactor, GmlEffects.Recipe(GmlEffectKind.Ring, 1).Type.SizeIncrPerStep, 1e-7f);
            Assert.AreEqual(0.4f * GmlEffects.SpeedFactor, GmlEffects.Recipe(GmlEffectKind.Ring, 2).Type.SizeIncrPerStep, 1e-7f);
            Assert.IsNotNull(HareArt.ParticleShape("pt_shape_ring"), "the runner's ring texture");
        }

        static int Drop(PickupKind kind, Vector2 at) =>
            ((PickupDropped)PickupDirector.Drop(kind, at).Events.Single()).Pickup.Id;
    }
}
