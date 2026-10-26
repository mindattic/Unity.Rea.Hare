using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>
    /// U.HARE-T31: the weapons' and explosions' effects as gmlHARE draws them: obj_muzzleflash from the vulcan and
    /// shotgun, the shotgun's and flamer's smoke, the laser's ef_flare, the railgun's swirls and smoke, and the
    /// rocket and oval explosions' animations.
    /// </summary>
    public class WeaponEffectsTests
    {
        const float Step = 1f / 120f;

        readonly List<GmlEffectBurst> effects = new List<GmlEffectBurst>();
        readonly List<MuzzleFlash> flashes = new List<MuzzleFlash>();
        readonly List<RocketExplosion> rocketBlasts = new List<RocketExplosion>();

        void OnEffect(GmlEffectBurst b) => effects.Add(b);
        void OnFlash(MuzzleFlash f) => flashes.Add(f);
        void OnRocketBlast(RocketExplosion r) => rocketBlasts.Add(r);

        [SetUp]
        public void SetUp()
        {
            Random.InitState(3131);
            effects.Clear();
            flashes.Clear();
            rocketBlasts.Clear();
            GmlEffects.Created += OnEffect;
            MuzzleFlash.Spawned += OnFlash;
            RocketExplosion.Exploded += OnRocketBlast;
        }

        [TearDown]
        public void TearDown()
        {
            GmlEffects.Created -= OnEffect;
            MuzzleFlash.Spawned -= OnFlash;
            RocketExplosion.Exploded -= OnRocketBlast;
            PerchedWeaponTestUtil.DestroyAll();
            DestroyAll<LaserBeam>();
            DestroyAll<RailgunShot>();
            DestroyAll<GmlEffectBurst>();
            DestroyAll<MuzzleFlash>();
            DestroyAll<RocketExplosion>();
            DestroyAll<OvalExplosion>();
        }

        static void DestroyAll<T>() where T : Component
        {
            foreach (var c in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
                if (c != null) Object.DestroyImmediate(c.gameObject);
        }

        static int StepsUntilGone(System.Func<float, bool> tick)
        {
            int steps = 0;
            while (tick(Step)) { steps++; Assert.Less(steps, 10000); }
            return steps + 1;
        }

        // ---- The runner's ef_flare ----

        [Test]
        public void FlareIsTheRunnersEffect08()
        {
            var small = GmlEffects.Recipe(GmlEffectKind.Flare, 0);
            Assert.AreEqual(1, small.Count);
            Assert.AreEqual(GmlParticleShape.Flare, small.Type.Shape);
            Assert.AreEqual(0.4f, small.Type.SizeMin, 1e-6f);
            Assert.AreEqual(-0.02f * GmlEffects.SpeedFactor, small.Type.SizeIncrPerStep, 1e-7f);
            Assert.AreEqual(80f, small.Type.LifeMin, "Round(20 / 0.25)");
            Assert.AreEqual(0f, small.Type.OrientationMin);
            Assert.AreEqual(360f, small.Type.OrientationMax);
            Assert.AreEqual(1f, small.Type.AlphaEnd);
            Assert.AreEqual(1.2f, GmlEffects.Recipe(GmlEffectKind.Flare, 2).Type.SizeMin, 1e-6f);
            Assert.AreEqual(0.75f, GmlEffects.Recipe(GmlEffectKind.Flare, 1).Type.SizeMin, 1e-6f);
        }

        // ---- obj_muzzleflash ----

        [Test]
        public void MuzzleFlashAnimatesAtThreeTenthsAndEndsAfterFourFrames()
        {
            var flash = MuzzleFlash.Spawn(new Vector2(1f, 2f), 45f);
            var renderer = flash.GetComponent<SpriteRenderer>();
            Assert.AreEqual(0.95f, renderer.color.a, 1e-6f, "image_alpha = 0.95");
            Assert.AreEqual(2, renderer.sortingOrder, "depth -2");
            Assert.AreEqual(45f, flash.transform.eulerAngles.z, 1e-3f, "image_angle");
            Assert.IsNotNull(renderer.sprite, "spr_muzzleflash_new is imported");
            Assert.AreEqual(14, StepsUntilGone(flash.Tick), "4 frames / 0.3 = 13.3: Animation End on step 14");
        }

        [Test]
        public void VulcanShotFlashesAtTheMuzzleTurnedToTheBullet()
        {
            var go = new GameObject("Player");
            var ship = go.AddComponent<PlayerShipController>();
            ship.ResetState();
            var vulcan = go.AddComponent<VulcanWeapon>();
            vulcan.ResetState();
            ship.Land();
            ship.SetAimDirection(30f);
            var bullets = new List<Bullet>();
            vulcan.Fired += bullets.Add;

            vulcan.PressTrigger();
            vulcan.Tick(Step);

            Assert.AreEqual(1, bullets.Count);
            Assert.AreEqual(1, flashes.Count, "one obj_muzzleflash per bullet");
            var flash = flashes[0];
            Assert.AreEqual(bullets[0].Direction, flash.Angle, 1e-3f);
            Vector2 at = flash.transform.position;
            Vector2 bulletAt = bullets[0].transform.position;
            Assert.AreEqual(bulletAt.x, at.x, 1e-4f, "the bullet's X");
            Assert.That(Mathf.Abs(bulletAt.y - at.y), Is.LessThanOrEqualTo(0.06f), "the bullet's Y but for its +-5 px jitter");
        }

        [Test]
        public void ShotgunBlastFlashesAlongTheLastPelletAndSmokesBlack()
        {
            var shotgun = PerchedWeaponTestUtil.NewPerchedWeapon<Shotgun>();
            shotgun.GetComponent<PlayerShipController>().SetAimDirection(0f);
            Vector2 muzzle = shotgun.MuzzlePosition;

            Assert.IsTrue(shotgun.TryFire());

            Assert.AreEqual(1, flashes.Count, "one flash per blast");
            Assert.AreEqual(shotgun.LastBlast.Last().Direction, flashes[0].Angle, 1e-3f, "muzzleflash.image_angle = pellet.direction");
            Assert.That(Vector2.Distance(flashes[0].transform.position, muzzle), Is.LessThan(1e-4f));

            var smoke = effects.Single();
            Assert.AreEqual(GmlEffectKind.Smoke, smoke.Kind);
            Assert.AreEqual(1, smoke.Size);
            Assert.AreEqual(Color.black, smoke.Color);
            Assert.IsFalse(smoke.Above, "effect_create_below");
            Assert.That(Vector2.Distance(smoke.Origin, muzzle), Is.LessThan(1e-4f));
        }

        [Test]
        public void FlamerPuffsSmallBlackSmokeUpWithEachFlame()
        {
            var flamer = PerchedWeaponTestUtil.NewPerchedWeapon<Flamethrower>();
            Vector2 muzzle = flamer.MuzzlePosition;
            Assert.IsTrue(flamer.TryFire());

            var smoke = effects.Single();
            Assert.AreEqual(GmlEffectKind.SmokeUp, smoke.Kind);
            Assert.AreEqual(0, smoke.Size);
            Assert.AreEqual(Color.black, smoke.Color);
            Assert.IsFalse(smoke.Above);
            Assert.That(Vector2.Distance(smoke.Origin, muzzle), Is.LessThan(1e-4f));
            Assert.AreEqual(0, flashes.Count, "shoot_flamer makes no muzzle flash");
        }

        // ---- obj_laser's ef_flare ----

        [Test]
        public void LaserFlaresCornflowerAtTheMuzzleEveryStep()
        {
            var laser = PerchedWeaponTestUtil.NewPerchedWeapon<LaserGun>();
            laser.TriggerHeld = true;
            Assert.IsTrue(laser.TryFire());
            var beam = laser.ActiveLaser;

            Assert.AreEqual(1, effects.Count, "the first step's flare");
            Assert.AreEqual(5, beam.Glow(5 * Step));
            Assert.AreEqual(6, effects.Count);
            foreach (var flare in effects)
            {
                Assert.AreEqual(GmlEffectKind.Flare, flare.Kind);
                Assert.AreEqual(0, flare.Size);
                Assert.IsFalse(flare.Above);
                Assert.AreEqual(100f / 255f, flare.Color.r, 1e-4f);
                Assert.AreEqual(149f / 255f, flare.Color.g, 1e-4f);
                Assert.AreEqual(237f / 255f, flare.Color.b, 1e-4f);
                Assert.That(Vector2.Distance(flare.Origin, beam.Start), Is.LessThan(1e-4f));
            }
        }

        // ---- obj_railgun and obj_rail_swirl ----

        [Test]
        public void RailSwirlsSitEvery56PxInsideTheRoomTurnedToTheAim()
        {
            var room = new Rect(-1f, -1f, 10f, 2f); // the rail leaves this room at x = 9
            var rail = new GameObject("Rail").AddComponent<RailgunShot>();
            rail.Init(Vector2.zero, 0f, 20f, room);

            Assert.AreEqual(Mathf.CeilToInt(2000f / 56f), rail.SwirlPoints.Count);
            Assert.AreEqual(Mathf.FloorToInt(900f / 56f) + 1, rail.Swirls.Count, "only the points with 0 <= x <= room_width");
            foreach (var swirl in rail.Swirls)
            {
                Assert.LessOrEqual(swirl.transform.position.x, room.xMax);
                Assert.AreEqual(0f, swirl.transform.eulerAngles.z, 1e-3f);
                Assert.AreEqual(1, swirl.sortingOrder, "depth -1");
                Assert.AreEqual(RailgunShot.RailColor.g, swirl.color.g, 1e-4f, "image_blend = railColor");
                Assert.AreEqual(RailgunShot.DefaultAlpha, swirl.color.a, 1e-4f, "image_alpha = railAlpha");
                Assert.IsNotNull(swirl.sprite, "spr_railgun_swirl is imported");
            }

            rail.Tick(10 * Step);
            Assert.AreEqual(rail.Alpha, rail.Swirls[0].color.a, 1e-4f, "each step image_alpha = railAlpha");
        }

        [Test]
        public void RailSwirlsFollowTheAimAndGoWithTheRail()
        {
            var rail = new GameObject("Rail").AddComponent<RailgunShot>();
            rail.Init(Vector2.zero, 90f, 20f, new Rect(-50f, -50f, 100f, 100f));
            Assert.AreEqual(rail.SwirlPoints.Count, rail.Swirls.Count);
            Assert.AreEqual(90f, rail.Swirls[3].transform.eulerAngles.z, 1e-3f);
            Assert.AreEqual(3f * 0.56f, rail.Swirls[3].transform.position.y, 1e-4f);

            var swirl = rail.Swirls[0];
            while (rail.Tick(Step)) { }
            Assert.IsTrue(swirl == null, "with(obj_rail_swirl) instance_destroy() when the rail fades out");
        }

        [Test]
        public void RailSmokesAtTheMuzzleAndTheRailEndEveryStepWhileVisible()
        {
            var rail = new GameObject("Rail").AddComponent<RailgunShot>();
            rail.Init(Vector2.zero, 0f, 20f, new Rect(-50f, -50f, 100f, 100f));

            rail.Tick(Step);
            Assert.AreEqual(2, effects.Count);
            Assert.That(Vector2.Distance(effects[0].Origin, rail.Origin), Is.LessThan(1e-4f));
            Assert.That(Vector2.Distance(effects[1].Origin, rail.End), Is.LessThan(1e-4f));
            foreach (var smoke in effects)
            {
                Assert.AreEqual(GmlEffectKind.Smoke, smoke.Kind);
                Assert.AreEqual(0, smoke.Size);
                Assert.IsFalse(smoke.Above);
                Assert.AreEqual(RailgunShot.RailColor, smoke.Color);
            }

            int steps = 1;
            while (rail.Tick(Step)) steps++;
            Assert.That(effects.Count, Is.InRange(2 * (steps - 1), 2 * (steps + 1)), "a pair every step until the rail is gone");
        }

        // ---- obj_rocket_explosion and obj_oval_explosion ----

        [Test]
        public void RocketExplosionPlaysSprBigexplosionForFortySteps()
        {
            var blast = new GameObject("RocketExplosion").AddComponent<RocketExplosion>();
            blast.Init(new Vector2(3f, 4f));

            Assert.AreEqual(1, rocketBlasts.Count);
            var renderer = blast.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(renderer.sprite, "spr_bigexplosion is imported");
            Assert.AreEqual(4, renderer.sortingOrder, "depth -4");
            Assert.AreEqual(40f, RocketExplosion.LifetimeSteps, "20 frames at image_speed 0.5");

            for (int i = 0; i < 10; i++) blast.Tick(Step);
            Assert.AreEqual(5f, blast.ImageIndex, 1e-3f);
            Assert.AreEqual(HareArt.Frame(RocketExplosion.SpriteName, 5), renderer.sprite);
            Assert.AreEqual(40, 10 + StepsUntilGone(blast.Tick), "gone at Animation End");
        }

        [Test]
        public void OvalExplosionPlaysTwelveFramesAtThreeTenths()
        {
            var oval = OvalExplosion.Spawn(Vector2.zero);
            var renderer = oval.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(renderer.sprite, "spr_explosion_oval is imported");
            Assert.AreEqual(4, renderer.sortingOrder, "depth -4");
            Assert.AreEqual(40, StepsUntilGone(oval.Tick), "12 / 0.3");
        }
    }
}
