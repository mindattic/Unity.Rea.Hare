using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Hare.Art;
using Unity.Hare.Content;
using Unity.Hare.Effects;
using Unity.Hare.Player;
using Unity.Hare.Rocks;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>
    /// U.HARE-T55: obj_player drawn from the imported art (PlayerLook): the spr_rabbit_* pose, the held ground weapon at
    /// gunX/gunY turned to mouse_angle, Alarm 5's colour, nothing while dead, and the smoke, spark and spawn-jet trails.
    /// </summary>
    public class PlayerLookTests
    {
        const float Step = 1f / GameClock.StepsPerSecond;

        HashSet<GameObject> before;
        readonly List<GmlEffectBurst> effects = new List<GmlEffectBurst>();
        GameObject go;
        PlayerShipController ship;
        PlayerHealth health;
        PlayerLook look;

        void OnEffect(GmlEffectBurst burst) => effects.Add(burst);

        [SetUp]
        public void SetUp()
        {
            before = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
            effects.Clear();
            GmlEffects.Created += OnEffect;
            go = PlayerShipBuilder.Build(Vector3.zero);
            ship = go.GetComponent<PlayerShipController>();
            health = go.GetComponent<PlayerHealth>();
            look = go.GetComponent<PlayerLook>();
        }

        [TearDown]
        public void TearDown()
        {
            GmlEffects.Created -= OnEffect;
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                if (root != null && !before.Contains(root)) Object.DestroyImmediate(root);
        }

        static Sprite Frame(string name) => HareArtManifest.Load().Frame(name);

        void Run(int steps)
        {
            for (int i = 0; i < steps; i++) ship.Step(Step);
        }

        [Test]
        public void TheBuiltShipCarriesItsLook()
        {
            Assert.IsNotNull(look, "PlayerShipBuilder adds the look");
            Assert.IsNotNull(look.Body);
            Assert.IsNotNull(look.Gun);
            Assert.AreSame(go.transform, look.Body.transform.parent);
        }

        [Test]
        public void InFlightItDrawsTheLeapPoseAndNoGun()
        {
            look.Draw();
            Assert.IsTrue(look.Body.enabled);
            Assert.IsNotNull(look.Body.sprite);
            Assert.AreSame(Frame(ShipPose.Leap), look.Body.sprite, "reset_player: sprite_index = asset_leap, frame 0");
            Assert.IsFalse(look.Gun.enabled, "Draw's air weapon line is commented out");
            Assert.AreEqual(Color.white, look.Body.color);
            Assert.AreEqual(Vector3.one, look.Body.transform.localScale, "scale = 1");
        }

        [Test]
        public void PerchedItDrawsTheIdlePoseAndTheVulcanAtTheGunTurnedToTheAim()
        {
            ship.Land();
            ship.SetAimDirection(45f);
            look.Draw();

            Assert.AreSame(Frame(ShipPose.Idle), look.Body.sprite);
            Assert.IsTrue(look.Gun.enabled);
            Assert.IsNotNull(look.Gun.sprite);
            Assert.AreSame(Frame("spr_weapon_vulcan"), look.Gun.sprite, "Create: ground_weapon_asset = spr_weapon_vulcan");

            Vector2 expected = (Vector2)go.transform.position + Gml.Dir(ship.Facing) * Gml.Px(-13f);
            Assert.That(Vector2.Distance(expected, look.Gun.transform.position), Is.LessThan(1e-4f), "gunX/gunY = lengthdir(-13, image_angle)");
            Assert.AreEqual(45f, look.Gun.transform.eulerAngles.z, 1e-3f, "turned to mouse_angle");
            Assert.Less(look.Gun.sortingOrder, look.Body.sortingOrder, "Draw draws the gun first, the ship over it");
        }

        [Test]
        public void TheHurtPoseAndTheFlashColourAreDrawn()
        {
            ship.Land();
            Assert.IsTrue(health.TryTakeHit(ignoresSpawnProtection: true, reaction: HitReaction.None));
            look.Draw();
            Assert.AreSame(Frame(ShipPose.IdleHurt), look.Body.sprite, "player_hurt: asset_idle_hurt");

            health.Tick(Step); // alarm[alarm_flashing] = 1: the first colour a step after the hit
            look.Draw();
            Assert.AreEqual(PlayerHealth.ColorOf(ShipColor.Pink), look.Body.color);
            Assert.AreEqual(look.Body.color, look.Gun.color, "the gun is drawn in the same colour");
        }

        [Test]
        public void ADeadShipIsNotDrawn()
        {
            ship.Die();
            look.Draw();
            Assert.IsFalse(look.Body.enabled, "Draw: if(state != state_dead)");
            Assert.IsFalse(look.Gun.enabled);
        }

        [Test]
        public void ShootingTheSecondaryShotgunPutsItInHand()
        {
            ship.Land();
            var shotgun = go.GetComponents<Shotgun>().First(s => s.Slot == WeaponSlot.Secondary);
            Assert.IsTrue(shotgun.TryFire(), "the perched secondary fires");
            look.Draw();
            Assert.AreSame(Frame("spr_weapon_shotgun"), look.Gun.sprite, "shoot_shotgun: ground_weapon_asset = spr_weapon_shotgun");
        }

        [Test]
        public void FlyingItLeavesAWhiteSmokeTrailEveryFortySteps()
        {
            health.Tick(2f); // past justspawned, so only the trail puffs
            effects.Clear();
            Run(40);
            var smoke = effects.Where(e => e != null && e.Kind == GmlEffectKind.SmokeUp).ToList();
            Assert.AreEqual(1, smoke.Count, "Alarm 2: smoke_delay = 40");
            Assert.AreEqual(0, smoke[0].Size);
            Assert.AreEqual(Color.white, smoke[0].Color);
            Assert.IsFalse(smoke[0].Above, "effect_create_below");
        }

        [Test]
        public void TurningLeavesAmberPinkOrCornflowerSparks()
        {
            health.Tick(2f);
            ship.SetTurnInput(1f);
            effects.Clear();
            Run(40);
            var sparks = effects.Where(e => e != null && e.Kind == GmlEffectKind.Spark).ToList();
            Assert.AreEqual(4, sparks.Count, "Alarm 3: one spark every spark_delay (8) steps while turning");
            var palette = new[] { ShipColor.Amber, ShipColor.Pink, ShipColor.CornflowerBlue }.Select(PlayerHealth.ColorOf).ToList();
            foreach (var spark in sparks)
            {
                Assert.That(palette, Does.Contain(spark.Color));
                Assert.IsFalse(spark.Above);
            }

            ship.SetTurnInput(0f);
            effects.Clear();
            Run(40);
            Assert.IsFalse(effects.Any(e => e != null && e.Kind == GmlEffectKind.Spark), "turning = false stops the trail");
        }

        [Test]
        public void AJustSpawnedShipJetsOrangeSmokeBehindItEveryStep()
        {
            effects.Clear();
            Run(5);
            var jet = effects.Where(e => e != null && e.Kind == GmlEffectKind.Smoke).ToList();
            Assert.AreEqual(5, jet.Count, "every step while justspawned");
            Assert.AreEqual(PlayerLook.Orange, jet[0].Color, "c_orange");
            Vector2 behind = (Vector2)go.transform.position + Gml.Dir(ship.Facing - 180f) * Gml.Px(36f);
            Assert.That(Vector2.Distance(behind, jet.Last().Origin), Is.LessThan(0.05f), "36 px behind the ship");
        }

        [Test]
        public void SmokeAndSparkAreTheRunnersEffects()
        {
            var smoke = GmlEffects.Recipe(GmlEffectKind.Smoke, 0);
            Assert.AreEqual(6, smoke.Count);
            Assert.AreEqual(GmlParticleShape.Explosion, smoke.Type.Shape);
            Assert.AreEqual(0f, smoke.Type.SpeedMax, "ef_smoke never moves");
            Assert.AreEqual(100f, smoke.Type.LifeMin);

            var spark = GmlEffects.Recipe(GmlEffectKind.Spark, 0);
            Assert.AreEqual(1, spark.Count);
            Assert.AreEqual(GmlParticleShape.Spark, spark.Type.Shape);
            Assert.AreEqual(0.4f, spark.Type.SizeMin, 1e-6f);
            Assert.AreEqual(-0.005f, spark.Type.SizeIncrPerStep, 1e-6f);
            Assert.AreEqual(80f, spark.Type.LifeMin);
            Assert.AreEqual(1f, spark.Type.AlphaEnd);
            Assert.AreEqual(360f, spark.Type.OrientationMax);
            Assert.IsNotNull(HareArt.ParticleShape(GmlEffectBurst.ShapeTextureName(GmlParticleShape.Spark)), "pt_shape_spark is imported");
        }

        [Test]
        public void PlayerSizeIsTheIdleRabbitsWidth()
        {
            var idle = HareArtManifest.Load().FindSprite(ShipPose.Idle);
            Assert.IsNotNull(idle);
            Assert.AreEqual(idle.width, GameContent.Current.Arena.PlayerSize, "player_size = sprite_get_width(spr_rabbit_idle)");
            Assert.AreEqual(idle.width, RockField.PlayerSizePx);
            foreach (var pose in new[] { ShipPose.Leap, ShipPose.LeapHurt, ShipPose.IdleHurt, ShipPose.Crouch, ShipPose.CrouchHurt })
                Assert.IsNotNull(Frame(pose), pose + " is imported");
        }
    }
}
