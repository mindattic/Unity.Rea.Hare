using System;
using NUnit.Framework;
using Unity.Rea.Hare;
using Unity.Rea.Hare.Art;

namespace MindAttic.Hare.Tests
{
    /// <summary>The GML sprite geometry the art pipeline (U.HARE-T46) imports with: origin, mask outline, frame rate.</summary>
    public class GmlSpriteGeometryTests
    {
        const float Eps = 1e-4f;

        [Test]
        public void Pivot_FlipsTheTopLeftOriginToBottomLeftFractions()
        {
            // spr_worm_head: 128x128, origin (32, 64).
            var p = GmlSpriteGeometry.Pivot(32, 64, 128, 128);
            Assert.That(p.X, Is.EqualTo(0.25f).Within(Eps));
            Assert.That(p.Y, Is.EqualTo(0.5f).Within(Eps));
            // spr_alertmarker: 32x64, origin (16, 32) is the centre.
            var c = GmlSpriteGeometry.Pivot(16, 32, 32, 64);
            Assert.That(c.X, Is.EqualTo(0.5f).Within(Eps));
            Assert.That(c.Y, Is.EqualTo(0.5f).Within(Eps));
            // spr_tip_box: origin (0, 0) is the top left.
            var tl = GmlSpriteGeometry.Pivot(0, 0, 548, 250);
            Assert.That(tl.X, Is.EqualTo(0f).Within(Eps));
            Assert.That(tl.Y, Is.EqualTo(1f).Within(Eps));
        }

        [Test]
        public void FramesPerSecond_IsImageSpeedTimesRoomSpeed()
        {
            Assert.That(GmlSpriteGeometry.FramesPerSecond(0.07f, 120f), Is.EqualTo(8.4f).Within(Eps)); // spr_mine
            Assert.That(GmlSpriteGeometry.FramesPerSecond(0.1f, 60f), Is.EqualTo(6f).Within(Eps));    // spr_textdone
            Assert.That(GmlSpriteGeometry.FramesPerSecond(1f, 120f), Is.EqualTo(120f).Within(Eps));   // GameMaker default
        }

        [Test]
        public void RectangleMask_CoversTheInclusiveBoxRelativeToTheOrigin()
        {
            // spr_alertmarker: 32x64, origin (16, 32), manual box (0, 23)-(31, 40).
            var outline = GmlSpriteGeometry.MaskOutline(GmlMaskKind.Rectangle, new GmlBoundingBox(0, 23, 31, 40), 16, 32);
            Assert.That(outline, Has.Length.EqualTo(4));
            AssertPoint(outline[0], -16f, -9f);  // bottom left: x 0-16, y 32-41
            AssertPoint(outline[1], 16f, -9f);   // bottom right: x 32-16
            AssertPoint(outline[2], 16f, 9f);    // top right: y 32-23
            AssertPoint(outline[3], -16f, 9f);
        }

        [Test]
        public void DiamondMask_PassesThroughTheBoxEdgeMidpoints()
        {
            var outline = GmlSpriteGeometry.MaskOutline(GmlMaskKind.Diamond, new GmlBoundingBox(0, 0, 9, 19), 0, 0);
            AssertPoint(outline[0], 5f, -20f);
            AssertPoint(outline[1], 10f, -10f);
            AssertPoint(outline[2], 5f, 0f);
            AssertPoint(outline[3], 0f, -10f);
        }

        [Test]
        public void EllipseMask_IsInscribedInTheBox()
        {
            // spr_bigexplosion: 96x96, origin (48, 48), box (0, 1)-(95, 95).
            var box = new GmlBoundingBox(0, 1, 95, 95);
            var outline = GmlSpriteGeometry.MaskOutline(GmlMaskKind.Ellipse, box, 48, 48, 16);
            Assert.That(outline, Has.Length.EqualTo(16));
            float cx = 0f, cy = (47f + -48f) / 2f, rx = 48f, ry = 47.5f;
            foreach (var p in outline)
            {
                float u = (p.X - cx) / rx, v = (p.Y - cy) / ry;
                Assert.That(u * u + v * v, Is.EqualTo(1f).Within(1e-3f));
            }
            AssertPoint(outline[0], cx, cy - ry); // starts at the bottom
        }

        [Test]
        public void PreciseMask_HasNoClosedFormOutline()
        {
            Assert.That(GmlSpriteGeometry.MaskOutline(GmlMaskKind.Precise, new GmlBoundingBox(0, 0, 1, 1), 0, 0), Is.Null);
        }

        [Test]
        public void BoundingBox_RejectsAnInvertedBox()
        {
            Assert.Throws<ArgumentException>(() => new GmlBoundingBox(5, 0, 4, 0));
            Assert.That(new GmlBoundingBox(0, 23, 31, 40).Width, Is.EqualTo(32));
            Assert.That(new GmlBoundingBox(0, 23, 31, 40).Height, Is.EqualTo(18));
        }

        static void AssertPoint(Vec2 p, float x, float y)
        {
            Assert.That(p.X, Is.EqualTo(x).Within(Eps), "x");
            Assert.That(p.Y, Is.EqualTo(y).Within(Eps), "y");
        }
    }
}
