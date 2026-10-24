using NUnit.Framework;
using Unity.Hare.UI;

namespace Unity.Hare.Tests.EditMode
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
    }
}
