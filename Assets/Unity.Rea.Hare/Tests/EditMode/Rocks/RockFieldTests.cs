using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Rocks;
using Unity.Hare.Weapons;
using static Unity.Hare.Tests.EditMode.EnemyTestKit;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>UH-T17: par_rock, create_new_rock, move_rocks, rock_destroyed and obj_flotsum.</summary>
    public class RockFieldTests
    {
        readonly List<RockGone> gone = new List<RockGone>();
        readonly List<GmlEffectBurst> effects = new List<GmlEffectBurst>();
        readonly List<TinyExplosion> puffs = new List<TinyExplosion>();

        void OnGone(Rock r, RockGone g) => gone.Add(g);
        void OnEffect(GmlEffectBurst b) => effects.Add(b);
        void OnPuff(TinyExplosion t) => puffs.Add(t);

        [SetUp]
        public void SetUp()
        {
            Random.InitState(1717);
            Gml.Paused = false;
            gone.Clear();
            effects.Clear();
            puffs.Clear();
            Rock.Removed += OnGone;
            GmlEffects.Created += OnEffect;
            TinyExplosion.Spawned += OnPuff;
        }

        [TearDown]
        public void TearDown()
        {
            Rock.Removed -= OnGone;
            GmlEffects.Created -= OnEffect;
            TinyExplosion.Spawned -= OnPuff;
            Gml.Paused = false;
            DestroyAll<RockField>();
            DestroyAll<Rock>();
            DestroyAll<Flotsum>();
            Cleanup();
        }

        static Rock StillRock(Vector2 at, int kind = 0)
        {
            var rock = Rock.Create(at, 100, null, kind);
            return rock;
        }

        // ---- par_rock Create, alarms, Step ----

        [Test]
        public void CreateRollsTheOriginalsRanges()
        {
            for (int i = 0; i < 60; i++)
            {
                var rock = Rock.Create(Vector2.zero, 0);
                Assert.AreEqual(255, rock.Rgb);
                Assert.That(rock.Kind, Is.InRange(0, 5));
                Assert.That(Mathf.Abs(rock.SpeedPxPerStep), Is.LessThanOrEqualTo(1.5f + 1e-5f));
                Assert.AreEqual(Mathf.Round(rock.SpeedPxPerStep * 10f), rock.SpeedPxPerStep * 10f, 1e-4f, "tenths of a px");
                Assert.That(Mathf.Abs(rock.Spin), Is.LessThanOrEqualTo(0.3f + 1e-5f));
                Assert.IsFalse(rock.Spin != 0f && Mathf.Abs(rock.Spin) <= 0.03f + 1e-5f, "a spin within 0.03 is none");
                Assert.AreEqual(rock.Spin, rock.OriginalSpin);
                Assert.AreEqual(rock.SpeedPxPerStep, rock.StoredSpeedPxPerStep);
                Object.DestroyImmediate(rock.gameObject);
            }
            var still = Rock.Create(Vector2.zero, 100);
            Assert.AreEqual(0f, still.SpeedPxPerStep, "stationary_percent 100");
        }

        [Test]
        public void RocksShowTheOriginalSpritesAtDepthThreeWithAPreciseMask()
        {
            for (int kind = 0; kind < Rock.SpriteNames.Length; kind++)
            {
                var rock = StillRock(Vector2.zero, kind);
                var renderer = rock.GetComponent<SpriteRenderer>();
                Assert.IsNotNull(renderer.sprite, Rock.SpriteNames[kind] + " is imported");
                Assert.AreEqual(HareArt.Frame(Rock.SpriteNames[kind]), renderer.sprite);
                Assert.AreEqual(-3, renderer.sortingOrder, "depth 3");
                Assert.AreEqual(Rock.SpriteWidthsPx[kind], renderer.sprite.rect.width, "size = sprite_get_width");
                Assert.IsNotNull(rock.GetComponent<PolygonCollider2D>(), "the sprite's shape");
                Assert.IsTrue(rock.GetComponent<Collider2D>().isTrigger);
            }
        }

        [Test]
        public void DriftsAtSpeedAndSpinsEveryStepAndStopsWhilePaused()
        {
            var rock = StillRock(Vector2.zero);
            rock.SetMotion(0f, 1.5f);
            float angle = rock.ImageAngle;
            for (int i = 0; i < 10; i++) rock.Tick(Step);
            Assert.AreEqual(Gml.Px(15f), rock.Position.x, 1e-4f);
            Assert.AreEqual(Mathf.Repeat(angle + rock.Spin * 10f, 360f), rock.ImageAngle, 1e-3f);

            Gml.Paused = true;
            Vector2 at = rock.Position;
            rock.Tick(Step);
            Assert.AreEqual(at, rock.Position, "speed = 0 while paused");
            Assert.AreEqual(1.5f, rock.StoredSpeedPxPerStep, "Speed survives the pause");
            Gml.Paused = false;
            rock.Tick(Step);
            Assert.AreEqual(at.x + Gml.Px(1.5f), rock.Position.x, 1e-4f, "speed = Speed again");
        }

        [Test]
        public void ColourHealsOneEveryQuarterSecond()
        {
            var rock = StillRock(Vector2.zero);
            var ship = Ship(new Vector2(-5f, 0f));
            for (int i = 0; i < 3; i++) ShipRound(ship, rock.Position).TryHit(rock.GetComponent<Collider2D>());
            Assert.AreEqual(243, rock.Rgb);
            for (int i = 0; i < 29; i++) rock.Tick(Step);
            Assert.AreEqual(243, rock.Rgb);
            rock.Tick(Step);
            Assert.AreEqual(244, rock.Rgb, "alarm[0] = room_speed / 4");
            for (int i = 0; i < 30; i++) rock.Tick(Step);
            Assert.AreEqual(245, rock.Rgb);
            Assert.AreEqual(245f / 255f, rock.GetComponent<SpriteRenderer>().color.r, 1e-4f, "image_blend");
        }

        // ---- add_spin and Alarm 1 ----

        [Test]
        public void AddSpinWindsEitherWayUpToSpinMax()
        {
            var rock = StillRock(Vector2.zero);
            SetSpin(rock, 1f);
            rock.AddSpin(0.25f);
            Assert.AreEqual(1.5f, rock.Spin, 1e-5f, "1 + 1 * 0.25 + 0.25");
            SetSpin(rock, -1f);
            rock.AddSpin(0.25f);
            Assert.AreEqual(-1.5f, rock.Spin, 1e-5f, "mirrored (GML snapped it to -spin_max)");
            SetSpin(rock, -3.9f);
            rock.AddSpin(0.25f);
            Assert.AreEqual(-Rock.SpinMax, rock.Spin);
            SetSpin(rock, 3.9f);
            rock.AddSpin(0.25f);
            Assert.AreEqual(Rock.SpinMax, rock.Spin);
            SetSpin(rock, 0f);
            rock.AddSpin(0.25f);
            Assert.That(rock.Spin, Is.InRange(-0.15f, 0.15f), "a still rock gets irandom_range(-15, 15) * 0.01");
        }

        [Test]
        public void FrictionSlowsAWoundUpSpinBackTowardItsOriginalWithoutFlippingIt()
        {
            var rock = StillRock(Vector2.zero);
            SetSpin(rock, -0.2f);
            rock.AddSpin(0.25f); // -0.2 - 0.05 - 0.25 = -0.5
            Assert.AreEqual(-0.5f, rock.Spin, 1e-5f);
            for (int i = 0; i < 30; i++) rock.Tick(Step);
            Assert.AreEqual(-0.48f, rock.Spin, 1e-5f, "* 0.96 at alarm[1], still negative");
            for (int i = 0; i < 30 * 40; i++) rock.Tick(Step);
            Assert.That(rock.Spin, Is.InRange(-0.2f, -0.19f), "stops slowing at original_spin");
        }

        static void SetSpin(Rock rock, float spin)
        {
            // spin and original_spin pinned for the test (both have private setters).
            typeof(Rock).GetProperty(nameof(Rock.Spin)).SetValue(rock, spin);
            typeof(Rock).GetProperty(nameof(Rock.OriginalSpin)).SetValue(rock, spin);
        }

        // ---- Collisions ----

        [Test]
        public void ABulletDarkensPushesAndSpinsTheRockAndIsSpentInATinyExplosion()
        {
            var rock = StillRock(Vector2.zero);
            var ship = Ship(new Vector2(-5f, 0f));
            var bullet = ShipRound(ship, new Vector2(-0.5f, 0f), 0f);
            Assert.IsTrue(bullet.TryHit(rock.GetComponent<Collider2D>()));
            Assert.IsTrue(bullet == null, "with(other) instance_destroy()");
            Assert.AreEqual(251, rock.Rgb);
            Assert.AreEqual(0.1f, rock.SpeedPxPerStep, 1e-5f, "motion_add(other.direction, 0.1)");
            Assert.AreEqual(0f, rock.Direction, 1e-3f);
            Assert.AreEqual(0.1f, rock.StoredSpeedPxPerStep, 1e-5f);
            Assert.AreEqual(1, puffs.Count);
            Assert.AreEqual(new Vector2(-0.5f + Gml.Px(0.1f), 0f), (Vector2)puffs[0].transform.position, "other.x + hspeed");
            Assert.AreEqual(0, gone.Count);
        }

        [Test]
        public void PushesCapSpeedAtThree()
        {
            var rock = StillRock(Vector2.zero);
            rock.SetMotion(0f, 2.8f);
            var rocket = FireRocket(new Vector2(-0.5f, 0f), 0f);
            rocket.Touch(rock.GetComponent<Collider2D>());
            Assert.AreEqual(3f, rock.SpeedPxPerStep, 1e-5f);
        }

        [Test]
        public void ARocketDarkensTheRockTwentyAndGoesOffThere()
        {
            var rock = StillRock(Vector2.zero);
            var rocket = FireRocket(new Vector2(-0.5f, 0f), 0f);
            rocket.Touch(rock.GetComponent<Collider2D>());
            Assert.IsTrue(rocket == null);
            Assert.AreEqual(235, rock.Rgb);
            Assert.AreEqual(0.5f, rock.SpeedPxPerStep, 1e-5f);
            var explosion = Object.FindAnyObjectByType<RocketExplosion>();
            Assert.IsNotNull(explosion);
            Assert.AreEqual(-0.5f + Gml.Px(0.5f), explosion.transform.position.x, 1e-5f, "other.x + hspeed");
        }

        [Test]
        public void BelowHalfColourTheShipsBulletBreaksItForTenPoints()
        {
            var field = RockField.CreateField();
            var rock = AddStill(field);
            gone.Clear();
            var ship = Ship(new Vector2(-5f, 0f));
            int hits = 0;
            while (rock.Rgb > Rock.BreakRgb) { ShipRound(ship, rock.Position).TryHit(rock.GetComponent<Collider2D>()); hits++; }
            Assert.AreEqual(32, hits, "255 - 4 * 32 = 127");
            puffs.Clear();

            Vector2 at = rock.Position;
            var bullet = ShipRound(ship, at + new Vector2(0.3f, 0f));
            Assert.IsTrue(bullet.TryHit(rock.GetComponent<Collider2D>()));
            Assert.IsTrue(bullet == null, "obj_bullet's par_rock event spends it");
            Assert.IsTrue(rock == null);
            Assert.AreEqual(1, gone.Count);
            Assert.AreEqual(RockRemoval.Bullet, gone[0].Cause);
            Assert.IsTrue(gone[0].AwardsScore);
            Assert.AreEqual(10, gone[0].Points);
            Assert.AreEqual(1, puffs.Count);
            AssertBreakEffects(at);
            Assert.AreEqual(1, field.Rocks.Count, "create_new_rock replaced it");
        }

        [Test]
        public void AnEnemyRoundChipsAndBreaksRocksButScoresNothing()
        {
            var rock = StillRock(Vector2.zero);
            while (rock.Rgb > Rock.BreakRgb) EnemyRound(rock.Position).TryHit(rock.GetComponent<Collider2D>());
            EnemyRound(rock.Position).TryHit(rock.GetComponent<Collider2D>());
            Assert.AreEqual(1, gone.Count);
            Assert.IsFalse(gone[0].AwardsScore);
        }

        [Test]
        public void ARocketThatBreaksARockFliesOn()
        {
            var rock = StillRock(Vector2.zero);
            for (int i = 0; i < 7; i++) FireRocket(new Vector2(-0.5f, 0f)).Touch(rock.GetComponent<Collider2D>());
            Assert.AreEqual(115, rock.Rgb);
            DestroyAll<RocketExplosion>();
            var rocket = FireRocket(new Vector2(-0.5f, 0f));
            rocket.Touch(rock.GetComponent<Collider2D>());
            Assert.IsTrue(rock == null);
            Assert.IsFalse(rocket == null, "obj_rocket has no par_rock event");
            Assert.AreEqual(0, Count<RocketExplosion>());
            Assert.AreEqual(RockRemoval.Rocket, gone[0].Cause);
            Assert.IsTrue(gone[0].AwardsScore, "rocket kills always score");
        }

        void AssertBreakEffects(Vector2 at)
        {
            var grenade = Object.FindAnyObjectByType<GrenadeExplosion>();
            Assert.IsNotNull(grenade, "obj_grenade_explosion");
            Assert.AreEqual(at, (Vector2)grenade.transform.position);
            Assert.AreEqual(2, effects.Count);
            Assert.IsTrue(effects.All(e => e.Size == DeathEffects.Large), "size 2");
            Assert.AreEqual(1, effects.Count(e => e.Kind == GmlEffectKind.SmokeUp));
            Assert.AreEqual(1, effects.Count(e => e.Kind == GmlEffectKind.Firework));
            var shards = Object.FindObjectsByType<Flotsum>(FindObjectsSortMode.None);
            Assert.That(shards.Length, Is.InRange(RockField.FlotsumMin, RockField.FlotsumMax));
            foreach (var s in shards)
            {
                Vector2 d = (s.Position - at) * Gml.PixelsPerUnit;
                Assert.That(Mathf.Abs(d.x), Is.LessThanOrEqualTo(40.001f));
                Assert.That(Mathf.Abs(d.y), Is.LessThanOrEqualTo(40.001f));
            }
        }

        [Test]
        public void TheLaserIsDrawnOnlyUpToARock()
        {
            var rock = StillRock(Vector2.zero);
            var blocker = Gml.FirstAlong<IBeamBlocker>(new Vector2(-5f, 0f), new Vector2(5f, 0f), out var at);
            Assert.AreSame(rock, blocker);
            Assert.Less(at.x, 0f);
            Assert.Greater(at.x, -1.5f);
        }

        // ---- The field ----

        Rock AddStill(RockField field)
        {
            field.Setup(1, 1, 100);
            if (field.Rocks.Count == 0) return AddStill(field); // move_rocks cleared it
            return field.Rocks[0];
        }

        [Test]
        public void SetupPlacesTheLevelsRocksInsideTheRoomAndClearsTheSpawnPoint()
        {
            var field = RockField.CreateField();
            for (int run = 0; run < 10; run++)
            {
                gone.Clear();
                field.Setup(15, 30, 50);
                int cleared = gone.Count(g => g.Cause == RockRemoval.Cleared && g.Position.y > field.Room.yMax - 8f
                    && Mathf.Abs(g.Position.x - field.Room.center.x) <= 4f + 1e-4f);
                Assert.That(field.Rocks.Count + cleared, Is.InRange(15, 30));
                foreach (var rock in field.Rocks)
                {
                    Vector2 gml = (rock.Position - new Vector2(field.Room.xMin, field.Room.yMax)) * Gml.PixelsPerUnit;
                    gml.y = -gml.y;
                    Assert.That(gml.x, Is.InRange(95.99f, 3840f - 95.99f));
                    Assert.That(gml.y, Is.InRange(95.99f, 2160f - 95.99f));
                    Assert.IsFalse(gml.y < 800f && Mathf.Abs(gml.x - 1920f) <= 400f, "move_rocks");
                    Assert.AreEqual(rock.Spin, rock.OriginalSpin);
                }
            }
        }

        [Test]
        public void StationaryPercentDecidesHowManyStartStill()
        {
            var field = RockField.CreateField();
            field.Setup(30, 30, 100);
            Assert.IsTrue(field.Rocks.All(r => r.SpeedPxPerStep == 0f));
            field.Setup(30, 30, 0);
            Assert.Greater(field.Rocks.Count(r => r.SpeedPxPerStep != 0f), field.Rocks.Count / 2);
        }

        [Test]
        public void NewRocksEnterFromJustPastASideEdgeHeadingIn()
        {
            var field = RockField.CreateField();
            for (int i = 0; i < 60; i++)
            {
                var rock = field.CreateNewRock();
                float size = Gml.Px(rock.SizePx);
                bool left = Mathf.Abs(rock.Position.x - (field.Room.xMin - size)) < 1e-4f;
                bool right = Mathf.Abs(rock.Position.x - (field.Room.xMax + size)) < 1e-4f;
                Assert.IsTrue(left || right, "x = -size or room_width + size");
                Assert.That(rock.Position.y, Is.InRange(field.Room.yMin - 1e-4f, field.Room.yMax + 1e-4f));
                Assert.That(rock.SpeedPxPerStep, Is.InRange(0.1f - 1e-5f, 1.5f + 1e-5f));
                float vx = rock.VelocityPxPerStep.x;
                Assert.IsTrue(left ? vx >= -1e-5f : vx <= 1e-5f, "heads into the room");
                Assert.IsFalse(rock.OutsideRoom(), "on the edge, not past it");
            }
        }

        [Test]
        public void ARockThatDriftsOutIsReplacedWithoutFlotsum()
        {
            var field = RockField.CreateField();
            var rock = field.CreateNewRock();
            rock.SetMotion(0f, 3f);
            float startX = rock.Position.x;
            bool movingOut = startX > field.Room.center.x;
            if (!movingOut) rock.SetMotion(180f, 3f);
            for (int i = 0; i < 5 && rock != null; i++) rock.Tick(Step);
            Assert.IsTrue(rock == null);
            Assert.AreEqual(RockRemoval.LeftRoom, gone[0].Cause);
            Assert.AreEqual(1, field.Rocks.Count);
            Assert.AreEqual(0, Count<Flotsum>());
        }

        // ---- obj_flotsum ----

        [Test]
        public void FlotsumDriftsSpinsAndFadesAndKeepsDriftingAfterAPause()
        {
            var shard = Flotsum.Spawn(Vector2.zero);
            Assert.IsNotNull(shard.GetComponent<SpriteRenderer>().sprite);
            Assert.That(shard.SpeedPxPerStep, Is.InRange(1f, 3f));
            Assert.That(shard.SpinSpeed, Is.InRange(-5f, 5f));
            Assert.That(shard.FadeSpeed, Is.InRange(0.002f - 1e-6f, 0.01f + 1e-6f));

            shard.Tick(Step);
            Assert.AreEqual(Gml.Px(shard.SpeedPxPerStep), shard.Position.magnitude, 1e-5f);
            Gml.Paused = true;
            Vector2 at = shard.Position;
            float alpha = shard.ImageAlpha;
            shard.Tick(Step);
            Assert.AreEqual(at, shard.Position);
            Assert.AreEqual(alpha, shard.ImageAlpha);
            Gml.Paused = false;
            shard.Tick(Step);
            Assert.Greater((shard.Position - at).magnitude, Gml.Px(0.99f), "speed = Speed after the pause");

            int steps = 0;
            while (shard != null && steps < 1000) { shard.Tick(Step); steps++; }
            Assert.IsTrue(shard == null, "gone once image_alpha reaches 0");
        }
    }
}
