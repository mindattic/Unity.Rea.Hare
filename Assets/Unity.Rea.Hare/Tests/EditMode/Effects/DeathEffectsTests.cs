using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Enemies;
using Unity.Hare.Player;
using Unity.Hare.Weapons;
using static Unity.Hare.Tests.EditMode.EnemyTestKit;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>UH-T16: the enemies' death effects, the GameMaker effects behind them, tiny explosions and death particles.</summary>
    public class DeathEffectsTests
    {
        readonly List<GmlEffectBurst> effects = new List<GmlEffectBurst>();
        readonly List<TinyExplosion> puffs = new List<TinyExplosion>();

        void OnEffect(GmlEffectBurst b) => effects.Add(b);
        void OnPuff(TinyExplosion t) => puffs.Add(t);

        [SetUp]
        public void SetUp()
        {
            Random.InitState(1616);
            EnemyDirector.Reset(1616);
            Gml.Paused = false;
            effects.Clear();
            puffs.Clear();
            GmlEffects.Created += OnEffect;
            TinyExplosion.Spawned += OnPuff;
        }

        [TearDown]
        public void TearDown()
        {
            GmlEffects.Created -= OnEffect;
            TinyExplosion.Spawned -= OnPuff;
            Gml.Paused = false;
            Cleanup();
        }

        // ---- The GameMaker effects ----

        [Test]
        public void FireworkRecipesAreTheRunnersAtSpeedFactorAQuarter()
        {
            Assert.AreEqual(0.25f, GmlEffects.SpeedFactor);
            var small = GmlEffects.Recipe(GmlEffectKind.Firework, 0);
            Assert.AreEqual(75, small.Count);
            Assert.AreEqual(GmlParticleShape.Flare, small.Type.Shape);
            Assert.AreEqual(0.125f, small.Type.SpeedMin, 1e-6f);
            Assert.AreEqual(0.75f, small.Type.SpeedMax, 1e-6f);
            Assert.AreEqual(60f, small.Type.LifeMin);
            Assert.AreEqual(100f, small.Type.LifeMax);
            Assert.AreEqual(0.025f, small.Type.GravityPerStep, 1e-6f);
            Assert.AreEqual(270f, small.Type.GravityDirection);
            Assert.AreEqual(1f, small.Type.AlphaStart);
            Assert.AreEqual(0.4f, small.Type.AlphaEnd, 1e-6f);

            Assert.AreEqual(150, GmlEffects.Recipe(GmlEffectKind.Firework, 1).Count);
            var large = GmlEffects.Recipe(GmlEffectKind.Firework, 2);
            Assert.AreEqual(250, large.Count);
            Assert.AreEqual(2f, large.Type.SpeedMax, 1e-6f);
            Assert.AreEqual(160f, large.Type.LifeMax);
            Assert.AreEqual(150, GmlEffects.Recipe(GmlEffectKind.Firework, 7).Count, "any size but 0 and 2 is medium");
        }

        [Test]
        public void SmokeUpRecipesRiseShrinkAndFade()
        {
            var small = GmlEffects.Recipe(GmlEffectKind.SmokeUp, 0);
            Assert.AreEqual(6, small.Count, "i = 0..5");
            Assert.AreEqual(5f, small.SpreadPx);
            Assert.AreEqual(90f, small.Type.Direction);
            Assert.AreEqual(0f, small.Type.DirectionRange);
            Assert.AreEqual(100f, small.Type.LifeMin);
            Assert.AreEqual(-0.0025f, small.Type.SizeIncrPerStep, 1e-7f);
            Assert.AreEqual(0.4f, small.Type.AlphaStart, 1e-6f);
            Assert.AreEqual(0f, small.Type.AlphaEnd);

            var medium = GmlEffects.Recipe(GmlEffectKind.SmokeUp, 1);
            Assert.AreEqual(11, medium.Count);
            Assert.AreEqual(15f, medium.SpreadPx);
            Assert.AreEqual(120f, medium.Type.LifeMin);

            var large = GmlEffects.Recipe(GmlEffectKind.SmokeUp, 2);
            Assert.AreEqual(16, large.Count);
            Assert.AreEqual(30f, large.SpreadPx);
            Assert.AreEqual(200f, large.Type.LifeMax);
            Assert.AreEqual(1.75f, large.Type.SpeedMax, 1e-6f);
        }

        [Test]
        public void SmokeRisesWithinItsSpreadAndIsGoneAfterItsLife()
        {
            var burst = GmlEffects.CreateAbove(GmlEffectKind.SmokeUp, Vector2.zero, 0, Color.black);
            Assert.AreEqual(6, burst.Count);
            foreach (var p in burst.Particles)
            {
                Assert.That(Mathf.Abs(p.Position.x), Is.LessThanOrEqualTo(Gml.Px(5f) + 1e-6f));
                Assert.That(Mathf.Abs(p.Position.y), Is.LessThanOrEqualTo(Gml.Px(5f) + 1e-6f));
            }
            float y0 = burst.Particles[0].Position.y;
            for (int i = 0; i < 99; i++) Assert.IsTrue(burst.Tick(Step), $"alive at step {i + 1}");
            Assert.Greater(burst.Particles[0].Position.y, y0 + Gml.Px(0.75f * 98f), "rises 0.75-1 px/step");
            Assert.Less(burst.Particles[0].Alpha, 0.01f, "0.4 fading to 0");
            Assert.IsFalse(burst.Tick(Step), "a 100-step life");
            Assert.IsTrue(burst == null);
        }

        [Test]
        public void FireworkFlaresFallUnderGravityAndAreGoneWithin100Steps()
        {
            var burst = GmlEffects.CreateAbove(GmlEffectKind.Firework, Vector2.zero, 0, DeathEffects.Orange);
            Assert.AreEqual(75, burst.Count);
            Assert.IsTrue(burst.Particles.All(p => p.Position == Vector2.zero), "all from the point");
            for (int i = 0; i < 59; i++) burst.Tick(Step);
            Assert.AreEqual(75, burst.Count, "nothing goes before 60 steps");
            float meanY = burst.Particles.Average(p => p.Position.y);
            Assert.Less(meanY, 0f, "gravity 270 pulls them down");
            for (int i = 59; i < 100 && burst != null; i++) burst.Tick(Step);
            Assert.IsTrue(burst == null, "every flare lives 60-100 steps");
        }

        // ---- obj_tinyexplosion ----

        [Test]
        public void TinyExplosionPlaysSevenFramesAtAFifthAndGoesAfter35Steps()
        {
            var puff = TinyExplosion.Spawn(new Vector2(1f, 1f));
            var renderer = puff.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(renderer.sprite, "spr_explosion is imported");
            Assert.AreEqual(0.5f, renderer.color.a, "draw_sprite_ext(..., 0.5)");
            Assert.AreEqual(4, renderer.sortingOrder, "depth -4");
            for (int i = 0; i < 34; i++) Assert.IsTrue(puff.Tick(Step));
            Assert.AreEqual(HareArt.Frame("spr_explosion", 6), renderer.sprite, "the last frame");
            Assert.IsFalse(puff.Tick(Step));
            Assert.IsTrue(puff == null);
        }

        // ---- obj_death_particle / spawn_death_particles ----

        [Test]
        public void SpawnDeathParticlesScattersLargeMediumAndSmallWithin32Px()
        {
            int total = DeathParticle.SpawnDeathParticles(new Vector2(2f, 3f));
            var all = Object.FindObjectsByType<DeathParticle>(FindObjectsSortMode.None);
            Assert.AreEqual(total, all.Length);
            Assert.That(all.Count(p => p.Size == DeathParticleSize.Large), Is.InRange(3, 6));
            Assert.That(all.Count(p => p.Size == DeathParticleSize.Medium), Is.InRange(12, 18));
            Assert.That(all.Count(p => p.Size == DeathParticleSize.Small), Is.InRange(32, 48));
            foreach (var p in all)
            {
                Vector2 d = ((Vector2)p.transform.position - new Vector2(2f, 3f)) * Gml.PixelsPerUnit;
                Assert.That(Mathf.Abs(d.x), Is.LessThanOrEqualTo(32.001f));
                Assert.That(Mathf.Abs(d.y), Is.LessThanOrEqualTo(32.001f));
                Assert.That(p.SpeedPxPerStep, Is.InRange(1f, 8f));
                Assert.That(p.SpinPerStep, Is.InRange(-9f, 9f));
                Assert.AreEqual(0.9f, p.Alpha);
                Assert.IsNotNull(p.GetComponent<SpriteRenderer>().sprite);
            }
        }

        [Test]
        public void DeathParticleFadesOverFourHundredFiftyStepsAndHoldsWhilePaused()
        {
            var p = DeathParticle.Spawn(Vector2.zero, DeathParticleSize.Medium);
            Gml.Paused = true;
            for (int i = 0; i < 10; i++) p.Tick(Step);
            Assert.AreEqual(Vector2.zero, (Vector2)p.transform.position);
            Assert.AreEqual(0.9f, p.Alpha);
            Gml.Paused = false;

            p.Tick(Step);
            Assert.AreEqual(Gml.Px(p.SpeedPxPerStep), ((Vector2)p.transform.position).magnitude, 1e-5f);
            for (int i = 1; i < 449; i++) Assert.IsTrue(p.Tick(Step), $"alive at step {i + 1}");
            Assert.IsFalse(p.Tick(Step), "0.9 / 0.002 = 450 steps");
        }

        // ---- Gibs ----

        [Test]
        public void GibsShowTheirOriginalSpriteAndFadeTheirRenderer()
        {
            var gib = FadingDebris.Spawn(Gibs.Gib(new SeededRandom(7), DebrisKind.WormGib, Vec2.Zero));
            var renderer = gib.GetComponent<SpriteRenderer>();
            Assert.AreEqual(HareArt.Frame("spr_worm_gib" + gib.Variant), renderer.sprite);
            Assert.IsNotNull(renderer.sprite);
            gib.Tick(Step);
            Assert.AreEqual(gib.Alpha, renderer.color.a, 1e-6f);

            Gml.Paused = true;
            Vector2 at = gib.transform.position;
            gib.Tick(Step);
            Assert.AreEqual(at, (Vector2)gib.transform.position, "gibs stand still while paused");
        }

        [Test]
        public void SpritesPivotOnTheGmlOrigin()
        {
            var head = HareArt.Frame("spr_worm_head");
            Assert.IsNotNull(head);
            Assert.AreEqual(new Vector2(32f, 64f), head.pivot, "origin 32, 64 of 128x128");
            Assert.AreEqual(Gml.PixelsPerUnit, head.pixelsPerUnit);
            var blast = HareArt.Frame("spr_grenade_explode", 8);
            Assert.IsNotNull(blast);
            Assert.AreEqual(new Vector2(100f, 182f - 91f), blast.pivot);
            Assert.IsNull(HareArt.Frame("spr_grenade_explode", 9), "9 frames");
            Assert.IsNotNull(HareArt.Clip(DeathParticle.Sound));
            Assert.IsNotNull(HareArt.Clip(GrenadeExplosion.Sound));
            Assert.IsNotNull(HareArt.ParticleShape("pt_shape_flare"));
            Assert.IsNotNull(HareArt.ParticleShape("pt_shape_explosion"));
        }

        // ---- Per-enemy death effects ----

        void AssertBurst(Vector2 at, int size)
        {
            Assert.AreEqual(2, effects.Count, "ef_smokeup and ef_firework");
            var smoke = effects.Single(e => e.Kind == GmlEffectKind.SmokeUp);
            var sparks = effects.Single(e => e.Kind == GmlEffectKind.Firework);
            Assert.AreEqual(size, smoke.Size);
            Assert.AreEqual(size, sparks.Size);
            Assert.AreEqual(Color.black, smoke.Color);
            Assert.AreEqual(DeathEffects.Orange, sparks.Color);
            Assert.IsTrue(smoke.Above && sparks.Above, "effect_create_above");
            Assert.That(Vector2.Distance(at, smoke.Origin), Is.LessThan(1e-4f));
            Assert.That(Vector2.Distance(at, sparks.Origin), Is.LessThan(1e-4f));
        }

        [Test]
        public void ABulletKillPuffsWhereTheBulletWasAndBurstsWhereTheEnemyWas()
        {
            var ship = Ship(new Vector2(20f, 0f));
            var saw = Spawn<Sawblade>();
            saw.Init(new Vector2(1f, 1f), Gml.Room, ship);
            var round = ShipRound(ship, new Vector2(1.1f, 1f));
            Assert.IsTrue(round.TryHit(Hitbox(saw)));
            Assert.AreEqual(1, puffs.Count);
            Assert.That(Vector2.Distance(new Vector2(1.1f, 1f), puffs[0].transform.position), Is.LessThan(1e-4f));
            AssertBurst(new Vector2(1f, 1f), DeathEffects.Small);
        }

        [Test]
        public void EachEnemyBurstsAtItsOwnSize()
        {
            Assert.AreEqual(DeathEffects.Small, EnemyTable.Get(EnemyKind.Dagger).DeathBurstSize);
            Assert.AreEqual(DeathEffects.Small, EnemyTable.Get(EnemyKind.Hydra).DeathBurstSize);
            Assert.AreEqual(DeathEffects.Small, EnemyTable.Get(EnemyKind.Sawblade).DeathBurstSize);
            Assert.AreEqual(DeathEffects.Small, EnemyTable.Get(EnemyKind.Trilobyte).DeathBurstSize);
            Assert.AreEqual(DeathEffects.Small, EnemyTable.Get(EnemyKind.Wraith).DeathBurstSize);
            Assert.AreEqual(DeathEffects.Small, EnemyTable.Get(EnemyKind.Trilobyte).DeathBurstSize);
            Assert.AreEqual(DeathEffects.Medium, EnemyTable.Get(EnemyKind.Phalanx).DeathBurstSize);
            Assert.AreEqual(DeathEffects.Medium, EnemyTable.Get(EnemyKind.Mimic).DeathBurstSize);
            Assert.AreEqual(DeathEffects.Large, EnemyTable.Get(EnemyKind.Meatball).DeathBurstSize);
            Assert.AreEqual(DeathEffects.Large, EnemyTable.Get(EnemyKind.Worm).DeathBurstSize);
            Assert.AreEqual(DeathEffects.Large, EnemyTable.Get(EnemyKind.Worm).DeathBurstSize);

            var phalanx = Spawn<Phalanx>();
            phalanx.Init(Vector2.zero, Gml.Room, null);
            Assert.IsTrue(phalanx.OnRocketHit(null));
            AssertBurst(Vector2.zero, DeathEffects.Medium);
            Assert.AreEqual(0, puffs.Count, "a rocket kill has no tiny explosion: the rocket explodes");
        }

        [Test]
        public void HydraPuffsOnEveryHitButBurstsOnlyWhenItDies()
        {
            var ship = Ship(new Vector2(20f, 0f));
            var hydra = Spawn<Hydra>();
            hydra.Init(Vector2.zero, Gml.Room, ship);
            int arms = hydra.Arms;
            for (int i = 0; i < arms; i++) ShipRound(ship, Vector2.zero).TryHit(Hitbox(hydra));
            Assert.AreEqual(arms, puffs.Count);
            Assert.AreEqual(0, effects.Count);
            ShipRound(ship, Vector2.zero).TryHit(Hitbox(hydra));
            Assert.IsTrue(hydra == null, "the hit after the last turret kills it");
            Assert.AreEqual(arms + 1, puffs.Count);
            AssertBurst(Vector2.zero, DeathEffects.Small);
        }

        [Test]
        public void KillsFromTheOtherSideShowNothing()
        {
            var saw = Spawn<Sawblade>();
            saw.Init(Vector2.zero, Gml.Room, null);
            saw.OnWeaponHit(new WeaponHit(WeaponHitKind.Laser, Vector2.zero, Vector2.right));
            var hydra = Spawn<Hydra>();
            hydra.Init(Vector2.zero, Gml.Room, null);
            hydra.OnWeaponHit(new WeaponHit(WeaponHitKind.RocketExplosion, Vector2.zero, Vector2.right));
            Assert.AreEqual(0, effects.Count, "laser and rocket-explosion kills have no smoke or sparks");
            Assert.AreEqual(0, puffs.Count);

            var shielded = Spawn<Sawblade>();
            shielded.Init(new Vector2(3f, 0f), Gml.Room, null);
            shielded.OnShieldHit(null, Vector2.right);
            Assert.AreEqual(0, effects.Count, "the shield wall's kill has only the tiny explosion");
            Assert.AreEqual(1, puffs.Count);
            Assert.That(Vector2.Distance(new Vector2(3f, 0f), puffs[0].transform.position), Is.LessThan(1e-4f));
        }

        [Test]
        public void MeatballBurstAddsTheLargeBurstOverItsGrenadeAndGibs()
        {
            var ship = Ship(new Vector2(20f, 0f));
            var meatball = Spawn<Meatball>();
            meatball.Init(Vector2.zero, Gml.Room, ship);
            int hits = 0;
            while (meatball != null && !meatball.IsDead && hits < 100)
            {
                ShipRound(ship, meatball.Position).TryHit(Hitbox(meatball));
                hits++;
            }
            Assert.AreEqual(hits, puffs.Count, "one puff on every hit, the burst included (unlike the worm, it has no second)");
            AssertBurst(effects[0].Origin, DeathEffects.Large);
            Assert.AreEqual(1, Count<GrenadeExplosion>());
            Assert.That(DebrisCount(DebrisKind.MeatballGib), Is.InRange(8, 12));
        }

        [Test]
        public void EnemyMineBurstsWhenShotButNotOnItsFuse()
        {
            var shot = new GameObject("Mine").AddComponent<EnemyMine>();
            shot.Init(Vector2.zero);
            Assert.IsTrue(shot.OnProjectileHit(EnemyRound(new Vector2(0.05f, 0f))));
            AssertBurst(Vector2.zero, DeathEffects.Small);

            effects.Clear();
            var fused = new GameObject("Mine").AddComponent<EnemyMine>();
            fused.Init(new Vector2(5f, 0f), null, null, null, 20);
            for (int i = 0; i < 120 * 20 + 2 && fused != null; i++) fused.Tick(Step);
            Assert.IsTrue(fused == null);
            Assert.AreEqual(0, effects.Count, "alarm[1] is only the obj_rocket_explosion");
        }

        [Test]
        public void ExposedWormSegmentDiesWithTwoPuffsGrenadeBurstAndGibs()
        {
            var ship = Ship(new Vector2(40f, 0f));
            var worm = Spawn<Worm>();
            worm.Init(Vector2.zero, Gml.Room, ship);
            var tail = Object.FindObjectsByType<WormSegment>(FindObjectsSortMode.None).Single(s => s.IsExposed);
            Vector2 at = tail.Position;
            int hits = 0;
            while (tail != null && !tail.IsDead && hits < 100)
            {
                ShipRound(ship, tail.Position).TryHit(Hitbox(tail));
                hits++;
            }
            Assert.AreEqual(hits + 1, puffs.Count);
            AssertBurst(effects[0].Origin, DeathEffects.Large);
            Assert.That(DebrisCount(DebrisKind.WormGib), Is.InRange(6, 8));
        }
    }
}
