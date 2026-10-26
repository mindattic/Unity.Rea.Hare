using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using Unity.Hare.Content;
using Unity.Hare.Weather;

namespace Unity.Hare.Tests.Core
{
    /// <summary>
    /// gmlHARE's weather emitters run outside Unity (U.HARE-T18): obj_cloud_emitter/obj_cloud, obj_rain_emitter/obj_raindrop,
    /// obj_snow_emitter/obj_snow, obj_sand_emitter/obj_sand and obj_fog_emitter/obj_fog, with the BIBLE's D16-D18 fixes.
    /// </summary>
    public class WeatherTests
    {
        const int Width = 3840, Height = 2160; // room_gamescreen

        sealed class ScriptedRandom : IRandomSource
        {
            readonly Queue<int> next = new Queue<int>();
            public readonly List<(int Min, int Max)> Asked = new List<(int, int)>();
            public void Queue(params int[] values) { foreach (var v in values) next.Enqueue(v); }
            public int IRandom(int min, int max)
            {
                Asked.Add((min, max));
                return next.Count > 0 ? next.Dequeue() : min;
            }
            public float Range(float min, float max) => min;
        }

        ScriptedRandom rng;
        WeatherField field;

        [SetUp]
        public void SetUp()
        {
            rng = new ScriptedRandom();
            field = new WeatherField(rng, Width, Height, ContentRegistry.Default.Weather);
        }

        void Steps(int n)
        {
            for (int i = 0; i < n; i++) field.Execute(new StepWeather());
        }

        [Test]
        public void TheCloudEmitterMakesTwelveCloudsAnywhereThenOneToFourEveryTwoHundredSteps()
        {
            field.Execute(new StartWeather(WeatherKind.Cloud));
            Assert.AreEqual(12, field.Count(WeatherKind.Cloud));
            CollectionAssert.AreEqual(new[] { WeatherKind.Cloud }, field.Emitters.ToArray());

            // alarm[0] = 1: the first batch on the very next step (irandom_range(1, 4) = 3 here).
            rng.Queue(3);
            Steps(1);
            Assert.AreEqual(15, field.Count(WeatherKind.Cloud));
            Assert.Contains((1, 4), rng.Asked);

            rng.Asked.Clear();
            Steps(199);
            Assert.False(rng.Asked.Contains((1, 4)), "nothing before the 200-step rate");
            Steps(1);
            Assert.True(rng.Asked.Contains((1, 4)), "Alarm 0 again after rate = 200 steps");
        }

        [Test]
        public void ACloudStartsAtTheRightEdgeAndDriftsLeftUntilItIsPastItsOwnWidth()
        {
            field.Execute(new StartWeather(WeatherKind.Cloud));
            // Alarm 0's batch: 1 cloud; hspeed -irandom_range(1, 2) = -2, alpha 0.1 * 3, y 500, spr_cloud4.
            rng.Queue(1, 2, 3, 500, 4);
            var result = field.Execute(new StepWeather());
            var cloud = result.Events.OfType<WeatherParticleSpawned>().Single().Particle;
            Assert.AreEqual(WeatherKind.Cloud, cloud.Kind);
            Assert.AreEqual("spr_cloud4", cloud.Sprite);
            Assert.AreEqual(0.3f, cloud.Alpha, 1e-5f);
            Assert.AreEqual(500f, cloud.Y);
            Assert.AreEqual(-2f, cloud.DX);
            Assert.AreEqual(Width - 2f, cloud.X, "made at room_width, then hspeed moved it");
            Assert.AreEqual(0, cloud.Depth);
            Assert.True(rng.Asked.Contains((-156, Height + 156)), "y = irandom_range(-sprite_height/2, room_height + sprite_height/2)");

            // Gone once x < -sprite_width (851).
            int stepsToLeave = (int)((cloud.X + 851) / 2f) + 2;
            Steps(stepsToLeave);
            Assert.False(field.Particles.Contains(cloud));
        }

        [Test]
        public void PausedCloudsStopAndGoOnAfter()
        {
            field.Execute(new StartWeather(WeatherKind.Cloud));
            var cloud = field.Particles[0];
            float x = cloud.X;
            Assert.True(field.Execute(new SetWeatherPaused(true)).Accepted);
            Steps(10);
            Assert.AreEqual(x, cloud.X);
            field.Execute(new SetWeatherPaused(false));
            Steps(1);
            Assert.AreEqual(x + cloud.DX, cloud.X);
        }

        [Test]
        public void RainFallsFromAboveTheRoomOneDropAStepAndLeavesBelowIt()
        {
            field.Execute(new StartWeather(WeatherKind.Rain));
            Assert.AreEqual(0, field.Particles.Count, "the rain emitter makes nothing in Create");
            // x 100, y -40, dY 20, dX -1.
            rng.Queue(100, -40, 20, -1);
            Steps(1);
            var drop = field.Particles.Single();
            Assert.AreEqual("spr_rain", drop.Sprite);
            Assert.AreEqual(1f, drop.Alpha);
            Assert.AreEqual(-1, drop.Depth);
            Assert.AreEqual(99f, drop.X);
            Assert.AreEqual(-20f, drop.Y);
            Assert.True(rng.Asked.Contains((-4, -Height)), "y = irandom_range(-sprite_height, -room_height)");
            Assert.True(rng.Asked.Contains((15, 20)));

            int spawned = 0;
            for (int i = 0; i < 200; i++) spawned += field.Execute(new StepWeather()).Events.OfType<WeatherParticleSpawned>().Count();
            Assert.AreEqual(200, spawned, "one drop a step");
            Assert.False(field.Particles.Contains(drop), "gone once y > room_height");
        }

        [Test]
        public void SnowThatRollsNoFallStillFalls()
        {
            field.Execute(new StartWeather(WeatherKind.Snow));
            // x 10, y -9, choose -> spr_snow2, dY irandom_range(0, 2) = 0, dX 0.
            rng.Queue(10, -9, 1, 0, 0);
            Steps(1);
            var flake = field.Particles.Single();
            Assert.AreEqual("spr_snow2", flake.Sprite);
            Assert.AreEqual(0.8f, flake.Alpha, 1e-5f);
            Assert.AreEqual(0.5f, flake.DY, "BIBLE D16: irandom_range(0.5, 2)'s 0 falls at 0.5");
            Assert.AreEqual(-8.5f, flake.Y);
            Assert.AreEqual(-1f, flake.Angle);
            Assert.True(rng.Asked.Contains((0, 2)));
            Assert.True(rng.Asked.Contains((-2, 2)));
        }

        [Test]
        public void SandBlowsInFromTheLeftTwoGrainsAStepAndARisingGrainLeavesPastTheRightEdge()
        {
            field.Execute(new StartWeather(WeatherKind.Sand));
            // Grain 1: x -100, y 0, spr_sand1, dY 0 -> 2, dX 3. Grain 2: x 3840, y 100, spr_sand2, dY -1, dX 6.
            rng.Queue(-100, 0, 0, 0, 3, Width, 100, 1, -1, 6);
            var spawned = field.Execute(new StepWeather()).Events.OfType<WeatherParticleSpawned>().Select(e => e.Particle).ToArray();
            Assert.AreEqual(2, spawned.Length);
            Assert.AreEqual(2f, spawned[0].DY, "if (dY == 0) dY = 2");
            Assert.AreEqual(-97f, spawned[0].X);
            Assert.AreEqual("spr_sand2", spawned[1].Sprite);
            Assert.True(rng.Asked.Contains((-Width / 2, 0)));
            Assert.True(rng.Asked.Contains((-Height / 2, Height)));
            // BIBLE D17: grain 2 rises (dY -1) and would never have been destroyed; it is gone past the right edge.
            Assert.False(field.Particles.Contains(spawned[1]));
            Assert.True(field.Particles.Contains(spawned[0]));
        }

        [Test]
        public void FogMakesThirtyTwoBanksInTheBottomQuarterThatComeRoundAgain()
        {
            // Bank 1: spr_cloud2, dir left (1), x -10, dX -1, then x again 0, y 1700.
            rng.Queue(2, 1, -10, 1, 0, 1700);
            field.Execute(new StartWeather(WeatherKind.Fog));
            Assert.AreEqual(32, field.Count(WeatherKind.Fog));
            var bank = field.Particles[0];
            Assert.AreEqual("spr_cloud2", bank.Sprite);
            Assert.True(bank.DriftsLeft);
            Assert.AreEqual(-1f, bank.DX);
            Assert.AreEqual(0f, bank.X);
            Assert.AreEqual(1700f, bank.Y);
            Assert.AreEqual(0.3f, bank.Alpha, 1e-5f);
            Assert.AreEqual(-1, bank.Depth);
            Assert.True(rng.Asked.Contains((-Width / 2, Width + Width / 2)));
            Assert.True(rng.Asked.Contains((Height - Height / 4, 2003)));

            // Past -sprite_width it comes round from the right: room_width + irandom_range(1, room_width/2).
            Steps(851);
            Assert.AreEqual(-851f, bank.X);
            rng.Queue(500, 2);
            Steps(1);
            Assert.AreEqual(Width + 500f, bank.X);
            Assert.AreEqual(-2f, bank.DX);
            Assert.AreEqual(32, field.Count(WeatherKind.Fog), "fog is never destroyed");
        }

        [Test]
        public void ClearingRemovesEveryEmitterAndParticleAndStartingAgainReplacesThem()
        {
            field.Execute(new StartWeather(WeatherKind.Cloud, WeatherKind.Rain));
            Steps(5);
            int before = field.Particles.Count;
            var cleared = field.Execute(new ClearWeather());
            Assert.True(cleared.Accepted);
            Assert.AreEqual(before, cleared.Events.OfType<WeatherParticleRemoved>().Count());
            Assert.AreEqual(0, field.Particles.Count);
            Assert.AreEqual(0, field.Emitters.Count);
            Assert.False(field.Execute(new ClearWeather()).Accepted, "nothing left to clear");
            Steps(5);
            Assert.AreEqual(0, field.Particles.Count, "no emitter, no weather");

            field.Execute(new StartWeather(WeatherKind.Fog));
            CollectionAssert.AreEqual(new[] { WeatherKind.Fog }, field.Emitters.ToArray());
        }

        [Test]
        public void EveryShippedLevelHasTheRoomsCloudsAndNothingElse()
        {
            var arena = ContentRegistry.Default.Arena;
            foreach (var level in arena.Levels.Concat(new[] { arena.DefaultLevel, arena.GeneratorLevel }))
                CollectionAssert.AreEqual(new[] { WeatherKind.Cloud }, level.Weather, level.Script);
        }

        [Test]
        public void ALevelThatNamesAKindTwiceIsRefused()
        {
            var ex = Assert.Throws<ContentValidationException>(() => ContentRegistry.FromJson(
                "{ \"arena\": { \"DefaultLevel\": { \"Script\": \"level_default\", \"Weather\": [\"Rain\", \"Rain\"] } } }"));
            Assert.True(ex.Message.Contains("Weather"), ex.Message);
        }

        [Test]
        public void TheWeatherSectionLoadsFromData()
        {
            var registry = ContentRegistry.FromJson("{ \"weather\": { \"FogCount\": 4 }, \"arena\": { \"DefaultLevel\": { \"Script\": \"level_default\", \"Weather\": [\"Snow\", \"Fog\"] } } }");
            Assert.AreEqual(4, registry.Weather.FogCount);
            CollectionAssert.AreEqual(new[] { WeatherKind.Snow, WeatherKind.Fog }, registry.Arena.DefaultLevel.Weather);
        }

        [Test]
        public void ASeededRunHasEveryParticleInsideItsLimits()
        {
            var seeded = new WeatherField(new SeededRandom(7), Width, Height, ContentRegistry.Default.Weather);
            seeded.Execute(new StartWeather(WeatherKind.Cloud, WeatherKind.Rain, WeatherKind.Snow, WeatherKind.Sand, WeatherKind.Fog));
            for (int i = 0; i < 600; i++) seeded.Execute(new StepWeather());
            foreach (var p in seeded.Particles)
            {
                if (p.Kind != WeatherKind.Cloud) Assert.LessOrEqual(p.Y, Height, p.Kind.ToString());
                if (p.Kind == WeatherKind.Snow) Assert.Greater(p.DY, 0f, "every flake falls");
                if (p.Kind == WeatherKind.Sand) Assert.LessOrEqual(p.X, Width + 4);
            }
            Assert.AreEqual(32, seeded.Count(WeatherKind.Fog));
        }
    }
}
