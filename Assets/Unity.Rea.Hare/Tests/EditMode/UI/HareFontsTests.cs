using NUnit.Framework;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using Unity.Rea.Hare.UI;

namespace Unity.Rea.Hare.Tests.EditMode
{
    /// <summary>
    /// The font seam (U.HARE-T45) now resolves every one of the original's eight faces to the bitmap FontAsset the font
    /// pipeline built from GameMaker's own bake (U.HARE-T48), and applies it at the size that draws that bake 1:1.
    /// </summary>
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
        public void EveryFaceResolvesToItsBuiltFontAsset()
        {
            foreach (var name in HareFonts.All)
            {
                var asset = HareFonts.Get(name);
                Assert.IsNotNull(asset, name + ": Resources/" + HareFonts.ResourceFolder + name + " is missing; run tools/import-gml-fonts.ps1");
                Assert.AreEqual(name, asset.name);
                Assert.AreSame(asset, HareFonts.Get(name), name + " is cached");
            }
        }

        [TestCase("fnt_basic", 12, "Arial")]
        [TestCase("fnt_medium", 16, "Berlin Sans FB")]
        [TestCase("fnt_point", 12, "Berlin Sans FB")]
        [TestCase("fnt_score", 36, "Berlin Sans FB")]
        [TestCase("fnt_small", 11, "Berlin Sans FB")]
        [TestCase("font4", 12, "Arial")]
        [TestCase("font7", 12, "Arial")]
        [TestCase("font8", 12, "Arial")]
        public void EachFaceKeepsItsFontGmxFaceAndSize(string name, int size, string family)
        {
            var asset = HareFonts.Get(name);
            Assert.AreEqual(family, asset.faceInfo.familyName);
            Assert.AreEqual("Regular", asset.faceInfo.styleName, "no GML font is bold or italic");
            Assert.AreEqual(size, HareFonts.NativeSize(name));
        }

        [Test]
        public void AnUnknownFaceNameResolvesToNullRatherThanThrowing()
        {
            Assert.IsNull(HareFonts.Get("not_a_real_font_face"));
            Assert.IsNull(HareFonts.Get(null));
            Assert.IsNull(HareFonts.Get(string.Empty));
            Assert.AreEqual(0f, HareFonts.NativeSize("not_a_real_font_face"));
        }

        [Test]
        public void ApplySetsTheFaceAtItsNativeSize()
        {
            var label = new Label("hi");
            HareFonts.Apply(label, HareFonts.Score);
            Assert.AreSame(HareFonts.Get(HareFonts.Score), label.style.unityFontDefinition.value.fontAsset);
            Assert.AreEqual(36f, label.style.fontSize.value.value);
        }

        [Test]
        public void ApplyingAnUnknownFaceLeavesTheElementsFontUntouched()
        {
            var label = new Label("hi");
            var before = label.style.unityFontDefinition;
            HareFonts.Apply(label, "not_a_real_font_face");
            Assert.AreEqual(before, label.style.unityFontDefinition);
        }

        [Test]
        public void ClearCacheLetsAFaceBeResolvedAgain()
        {
            var first = HareFonts.Get(HareFonts.Basic);
            HareFonts.ClearCache();
            Assert.AreSame(first, HareFonts.Get(HareFonts.Basic), "the same asset reloads from Resources");
        }
    }
}
