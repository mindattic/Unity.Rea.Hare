using NUnit.Framework;
using UnityEngine.UIElements;
using Unity.Hare.UI;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>U.HARE-T45: the font seam is a safe no-op before U.HARE-T48 drops the licensed TTFs in.</summary>
    public class HareFontsTests
    {
        [TearDown]
        public void TearDown() => HareFonts.ClearCache();

        [Test]
        public void EveryOriginalFaceNameIsListed()
        {
            CollectionAssert.AreEquivalent(
                new[] { "fnt_basic", "fnt_medium", "fnt_point", "fnt_score", "fnt_small", "font4", "font7", "font8" },
                HareFonts.All);
        }

        [Test]
        public void EveryListedFaceResolvesToNullBeforeItsTtfIsImported()
        {
            foreach (var name in HareFonts.All) Assert.IsNull(HareFonts.Get(name), name);
        }

        [Test]
        public void AnUnknownFaceNameResolvesToNullRatherThanThrowing()
        {
            Assert.IsNull(HareFonts.Get("not_a_real_font_face"));
            Assert.IsNull(HareFonts.Get(null));
            Assert.IsNull(HareFonts.Get(string.Empty));
        }

        [Test]
        public void ApplyingAMissingFaceLeavesTheElementsFontUntouched()
        {
            var label = new Label("hi");
            var before = label.style.unityFontDefinition;
            HareFonts.Apply(label, HareFonts.Point);
            Assert.AreEqual(before, label.style.unityFontDefinition);
        }

        [Test]
        public void ClearCacheLetsAFaceBeResolvedAgain()
        {
            Assert.IsNull(HareFonts.Get(HareFonts.Basic));
            HareFonts.ClearCache();
            Assert.IsNull(HareFonts.Get(HareFonts.Basic)); // still no TTF on disk; just proves the cache reset didn't throw
        }
    }
}
