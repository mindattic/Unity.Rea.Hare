using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Rendering;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>
    /// U.HARE-T44: the screen-wide fade/flash layer the room transitions and the flashbomb will drive later
    /// (UH-T50, UH-T67). <see cref="ScreenOverlayLayer.Create"/> initializes synchronously (Awake does not run
    /// in Edit Mode, and <c>ProjectSetup</c> builds the main scene in Edit Mode), so these tests exercise that
    /// same path a running game never takes differently.
    /// </summary>
    public class ScreenOverlayLayerTests
    {
        GameObject cameraGo;

        [TearDown]
        public void TearDown()
        {
            if (cameraGo != null) Object.DestroyImmediate(cameraGo);
        }

        ScreenOverlayLayer NewOverlay(float orthographicSize = 5f)
        {
            cameraGo = new GameObject("Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = orthographicSize;
            return ScreenOverlayLayer.Create(cameraGo.transform);
        }

        [Test]
        public void StartsFullyTransparentOnTheOverlayLayer()
        {
            var overlay = NewOverlay();
            var renderer = overlay.GetComponent<SpriteRenderer>();
            Assert.AreEqual(HareSortingLayers.Overlay, renderer.sortingLayerName);
            Assert.AreEqual(short.MaxValue, renderer.sortingOrder);
            Assert.AreEqual(0f, overlay.Color.a);
            Assert.IsNotNull(renderer.sprite);
        }

        [Test]
        public void ColorPropertySetsTheRendererColor()
        {
            var overlay = NewOverlay();
            overlay.Color = new Color(1f, 1f, 1f, 0.5f);
            Assert.AreEqual(new Color(1f, 1f, 1f, 0.5f), overlay.GetComponent<SpriteRenderer>().color);
        }

        [Test]
        public void FitsItselfToTheParentCameraOnCreate()
        {
            var overlay = NewOverlay(orthographicSize: 5.4f);
            var camera = cameraGo.GetComponent<Camera>();
            float expectedHeight = camera.orthographicSize * 2f;
            float expectedWidth = expectedHeight * camera.aspect;
            Assert.AreEqual(expectedWidth, overlay.transform.localScale.x, 1e-3f);
            Assert.AreEqual(expectedHeight, overlay.transform.localScale.y, 1e-3f);
        }
    }
}
