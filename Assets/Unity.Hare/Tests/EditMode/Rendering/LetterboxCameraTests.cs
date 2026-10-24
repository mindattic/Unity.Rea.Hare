using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Rendering;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>U.HARE-T44: the 1920x1080 reference resolution's pillarbox/letterbox viewport math.</summary>
    public class LetterboxCameraTests
    {
        const float Reference = LetterboxCamera.ReferenceAspect; // 1920/1080

        [Test]
        public void ExactReferenceAspectFillsTheScreen()
        {
            var rect = LetterboxCamera.ComputeViewportRect(1920f, 1080f, Reference);
            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), rect);
        }

        [Test]
        public void WiderWindowPillarboxes()
        {
            // 2560x1080 is 0.75 as wide, in reference units, as the 1920x1080 composition: bars left and right.
            var rect = LetterboxCamera.ComputeViewportRect(2560f, 1080f, Reference);
            Assert.AreEqual(0f, rect.y, 1e-4f);
            Assert.AreEqual(1f, rect.height, 1e-4f);
            Assert.AreEqual(0.75f, rect.width, 1e-4f);
            Assert.AreEqual(0.125f, rect.x, 1e-4f);
        }

        [Test]
        public void NarrowerWindowLetterboxes()
        {
            // 1080x1920 (the same 16:9 panel, turned on its side): bars top and bottom.
            var rect = LetterboxCamera.ComputeViewportRect(1080f, 1920f, Reference);
            Assert.AreEqual(0f, rect.x, 1e-4f);
            Assert.AreEqual(1f, rect.width, 1e-4f);
            Assert.AreEqual(0.3164f, rect.height, 1e-3f);
            Assert.AreEqual(0.3418f, rect.y, 1e-3f);
        }

        [Test]
        public void DegenerateInputFillsTheScreenRatherThanThrowing()
        {
            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), LetterboxCamera.ComputeViewportRect(0f, 1080f, Reference));
            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), LetterboxCamera.ComputeViewportRect(1920f, 0f, Reference));
            Assert.AreEqual(new Rect(0f, 0f, 1f, 1f), LetterboxCamera.ComputeViewportRect(1920f, 1080f, 0f));
        }
    }
}
