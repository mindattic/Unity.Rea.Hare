using NUnit.Framework;
using UnityEngine;
using Unity.Rea.Hare.Arena;

namespace Unity.Rea.Hare.Tests.EditMode
{
    /// <summary>U.HARE-T58: obj_moon_surface as drawn, spr_moon_surface at depth 1, risen into its room once turned on.</summary>
    public class MoonSurfaceViewTests
    {
        MoonSurfaceView moon;
        int cleared;

        void OnCleared() => cleared++;

        [SetUp]
        public void SetUp()
        {
            cleared = 0;
            MoonSurfaceView.DropEmittersCleared += OnCleared;
        }

        [TearDown]
        public void TearDown()
        {
            MoonSurfaceView.DropEmittersCleared -= OnCleared;
            if (moon != null) Object.DestroyImmediate(moon.gameObject);
        }

        [Test]
        public void TheMoon_IsSprMoonSurfaceAtDepth1_AndRisesOnlyOnceTurnedOn()
        {
            moon = MoonSurfaceView.Create(960f, 1200f, 1080, (x, y) => new Vector2(x, -y));
            Assert.That(moon.Sprite.sprite, Is.Not.Null, "spr_moon_surface is imported");
            Assert.That(moon.Sprite.sprite.name, Does.StartWith("spr_moon_surface"));
            Assert.That(moon.Sprite.sortingOrder, Is.EqualTo(-1), "depth 1");
            Assert.That(moon.transform.position.y, Is.EqualTo(-1200f));

            moon.Step();
            Assert.That(moon.transform.position.y, Is.EqualTo(-1200f), "phase = \"off\": it stays");

            moon.TurnOn();
            for (int i = 0; i < 6; i++) moon.Step();
            Assert.That(moon.transform.position.y, Is.EqualTo(-1080f), "40 px a step until it is no longer below the room");
            Assert.That(cleared, Is.EqualTo(1), "with(obj_drop_emitter) instance_destroy(), once");
        }
    }
}
