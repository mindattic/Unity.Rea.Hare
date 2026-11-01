using NUnit.Framework;
using UnityEngine;
using Unity.Rea.Hare.Rendering;
using Unity.Rea.Hare.UI;

namespace Unity.Rea.Hare.Tests.EditMode
{
    /// <summary>U.HARE-T45: HareUiFit's pure sizing math mirrors LetterboxCamera's pillarbox/letterbox branch.</summary>
    public class HareUiFitTests
    {
        [Test]
        public void ExactReferenceAspectMatchesWidth()
        {
            Assert.AreEqual(0f, HareUiFit.ComputeMatch(1920f, 1080f));
        }

        [Test]
        public void WiderThanReferenceAspectMatchesHeightForPillarboxing()
        {
            Assert.AreEqual(1f, HareUiFit.ComputeMatch(3440f, 1440f)); // 21:9, wider than 16:9
        }

        [Test]
        public void NarrowerThanReferenceAspectMatchesWidthForLetterboxing()
        {
            Assert.AreEqual(0f, HareUiFit.ComputeMatch(1080f, 1920f)); // portrait, narrower than 16:9
        }

        [Test]
        public void DegenerateScreenSizeFallsBackToMatchWidth()
        {
            Assert.AreEqual(0f, HareUiFit.ComputeMatch(0f, 1080f));
            Assert.AreEqual(0f, HareUiFit.ComputeMatch(1920f, 0f));
            Assert.AreEqual(0f, HareUiFit.ComputeMatch(-100f, 1080f));
        }

        [Test]
        public void StageRectAtExactReferenceAspectFillsIt()
        {
            var rect = HareUiFit.ComputeStageRect(1920f, 1080f);
            Assert.AreEqual(new Rect(0f, 0f, LetterboxCamera.ReferenceWidth, LetterboxCamera.ReferenceHeight), rect);
        }

        [Test]
        public void StageRectIsAlwaysTheReferenceResolutionWhateverTheScreenSize()
        {
            foreach (var (w, h) in new[] { (3440f, 1440f), (1080f, 1920f), (2560f, 1080f), (800f, 600f) })
            {
                var rect = HareUiFit.ComputeStageRect(w, h);
                Assert.AreEqual(LetterboxCamera.ReferenceWidth, rect.width, 0.01f, $"{w}x{h}");
                Assert.AreEqual(LetterboxCamera.ReferenceHeight, rect.height, 0.01f, $"{w}x{h}");
            }
        }

        [Test]
        public void StageRectIsCentredInTheSpareRoomOnAWiderPillarboxedWindow()
        {
            // 21:9, wider than 16:9: the panel's logical area grows wider than 1920 (match height), so the stage
            // sits centred inside it, the same place LetterboxCamera draws the game camera's own pillarbox bars.
            var rect = HareUiFit.ComputeStageRect(3440f, 1440f);
            float expectedLogicalWidth = LetterboxCamera.ReferenceHeight * 3440f / 1440f;
            Assert.AreEqual((expectedLogicalWidth - LetterboxCamera.ReferenceWidth) / 2f, rect.x, 0.01f);
            Assert.AreEqual(0f, rect.y, 0.01f);
        }

        [Test]
        public void StageRectIsCentredInTheSpareRoomOnATallerLetterboxedWindow()
        {
            // Portrait, narrower than 16:9: the panel's logical area grows taller than 1080 (match width).
            var rect = HareUiFit.ComputeStageRect(1080f, 1920f);
            float expectedLogicalHeight = LetterboxCamera.ReferenceWidth * 1920f / 1080f;
            Assert.AreEqual(0f, rect.x, 0.01f);
            Assert.AreEqual((expectedLogicalHeight - LetterboxCamera.ReferenceHeight) / 2f, rect.y, 0.01f);
        }

        [Test]
        public void StageRectFallsBackToTheReferenceResolutionAtTheOriginForADegenerateScreenSize()
        {
            var rect = HareUiFit.ComputeStageRect(0f, 1080f);
            Assert.AreEqual(new Rect(0f, 0f, LetterboxCamera.ReferenceWidth, LetterboxCamera.ReferenceHeight), rect);
        }
    }
}
