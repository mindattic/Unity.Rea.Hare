using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Unity.Rea.Hare.Arena;
using Unity.Rea.Hare.Effects;
using Unity.Rea.Hare.Weapons;
using Unity.Rea.Hare.Weather;

namespace Unity.Rea.Hare.Tests.EditMode
{
    /// <summary>
    /// U.HARE-T18: the weather drawn from Core's <see cref="WeatherField"/> with the original's sprites, depths, alphas and
    /// angles, stepped with GML's paused, and taken away by setup_level's Clear Objects (<see cref="LevelReset"/>).
    /// </summary>
    public class WeatherViewTests
    {
        WeatherView view;

        [SetUp]
        public void SetUp()
        {
            Gml.Paused = false;
            view = WeatherView.Create(3840, 2160, (x, y) => new Vector2(Gml.Px(x - 1920f), Gml.Px(1080f - y)), new SeededRandom(18));
        }

        [TearDown]
        public void TearDown()
        {
            Gml.Paused = false;
            foreach (var v in Object.FindObjectsByType<WeatherView>()) Object.DestroyImmediate(v.gameObject);
        }

        [Test]
        public void TheCloudEmitterDrawsTwelveCloudsWithTheOriginalsArtAtDepthZero()
        {
            view.Begin(new[] { WeatherKind.Cloud });
            Assert.AreEqual(12, view.Drawn);
            for (int i = 0; i < view.Drawn; i++)
            {
                var renderer = view.Renderers[i];
                var cloud = view.Field.Particles[i];
                Assert.IsTrue(renderer.enabled);
                Assert.IsNotNull(renderer.sprite, cloud.Sprite + " is imported");
                Assert.AreEqual(HareArt.SortingOrderForDepth(0), renderer.sortingOrder);
                Assert.AreEqual(cloud.Alpha, renderer.color.a, 1e-5f);
                Vector2 expected = new Vector2(Gml.Px(cloud.X - 1920f), Gml.Px(1080f - cloud.Y));
                Assert.AreEqual(expected.x, renderer.transform.position.x, 1e-4f);
                Assert.AreEqual(expected.y, renderer.transform.position.y, 1e-4f);
            }
        }

        [Test]
        public void SnowSandRainAndFogDrawAboveAtDepthMinusOne()
        {
            view.Begin(new[] { WeatherKind.Rain, WeatherKind.Snow, WeatherKind.Sand, WeatherKind.Fog });
            for (int i = 0; i < 3; i++) view.Step();
            view.Present();
            Assert.AreEqual(view.Field.Particles.Count, view.Drawn);
            Assert.AreEqual(32 + 3 * (1 + 1 + 2), view.Field.Particles.Count);
            for (int i = 0; i < view.Drawn; i++)
            {
                Assert.AreEqual(HareArt.SortingOrderForDepth(-1), view.Renderers[i].sortingOrder);
                Assert.IsNotNull(view.Renderers[i].sprite, view.Field.Particles[i].Sprite + " is imported");
            }
            var flake = view.Field.Particles.First(p => p.Kind == WeatherKind.Snow);
            int index = view.Field.Particles.ToList().IndexOf(flake);
            Assert.AreEqual(flake.Angle, Mathf.DeltaAngle(0f, view.Renderers[index].transform.eulerAngles.z), 1e-3f);
        }

        [Test]
        public void PausedWeatherHoldsStill()
        {
            view.Begin(new[] { WeatherKind.Fog });
            var bank = view.Field.Particles.First(p => p.X > 0f && p.X < 3840f); // well clear of coming round
            float x = bank.X;
            Gml.Paused = true;
            view.Step();
            Assert.AreEqual(x, bank.X);
            Gml.Paused = false;
            view.Step();
            Assert.AreEqual(x + bank.DX, bank.X);
        }

        [Test]
        public void TheLevelResetTakesTheWeatherAway()
        {
            view.Begin(new[] { WeatherKind.Cloud, WeatherKind.Rain });
            view.Step();
            Assert.Greater(view.Drawn, 0);

            LevelReset.ClearObjects();

            Assert.AreEqual(0, view.Field.Particles.Count);
            Assert.AreEqual(0, view.Field.Emitters.Count);
            Assert.AreEqual(0, view.Drawn);
            Assert.IsTrue(view.Renderers.All(r => !r.enabled), "every renderer idle");
        }
    }
}
