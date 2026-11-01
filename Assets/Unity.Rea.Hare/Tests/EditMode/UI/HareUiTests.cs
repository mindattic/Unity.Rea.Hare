using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Rea.Hare.UI;

namespace Unity.Rea.Hare.Tests.EditMode
{
    /// <summary>
    /// U.HARE-T45: the widget kit, in particular <see cref="HareUi.OutlineText"/> reproducing gmlHARE's
    /// draw_text_outline family exactly (a (2*size+1)^2 square of outline draws plus one text draw on top), not an
    /// approximation of it.
    /// </summary>
    public class HareUiTests
    {
        [Test]
        public void TextReturnsAnUnclickableLabelWithItsClasses()
        {
            var label = HareUi.Text("Score", "hud small");
            Assert.AreEqual("Score", label.text);
            Assert.IsTrue(label.ClassListContains("hud"));
            Assert.IsTrue(label.ClassListContains("small"));
            Assert.AreEqual(PickingMode.Ignore, label.pickingMode);
        }

        [Test]
        public void TextAddsItselfToTheGivenParent()
        {
            var parent = new VisualElement();
            var label = HareUi.Text("Score", parent: parent);
            Assert.AreSame(parent, label.parent);
        }

        [TestCase(0, 2)]   // one in-flow centre outline layer, no offsets besides it, plus the main layer: 1 + 1
        [TestCase(1, 9 + 1)]
        [TestCase(2, 25 + 1)]
        public void OutlineTextDrawsOneLayerPerGmlOffsetPlusOneTextLayerOnTop(int size, int expectedChildren)
        {
            var box = HareUi.OutlineText("HP", size);
            Assert.AreEqual(expectedChildren, box.childCount);
        }

        [Test]
        public void OutlineTextDefaultsToBlackOutlineAndWhiteText()
        {
            var box = HareUi.OutlineText("HP"); // default size 1 -> 9 outline layers + 1 text layer
            var layers = box.Children().Cast<Label>().ToList();

            Assert.AreEqual(Color.white, layers[layers.Count - 1].style.color.value); // added last: the text layer
            Assert.AreEqual(Color.black, layers[0].style.color.value); // an outline layer
        }

        [Test]
        public void EveryStackedLayerCarriesTheSameText()
        {
            var box = HareUi.OutlineText("Lives", 1);
            foreach (var layer in box.Children().Cast<Label>())
                Assert.AreEqual("Lives", layer.text);
        }

        [Test]
        public void TheCentreLayerIsInFlowAndEveryOtherLayerIsAbsolutelyOffset()
        {
            var box = HareUi.OutlineText("HP", 1);
            var layers = box.Children().ToList();
            int centreCount = 0, offsetCount = 0;
            for (int i = 0; i < layers.Count - 1; i++) // exclude the final main-colour layer, always absolute at (0,0)
            {
                var layer = layers[i];
                if (layer.style.position.value == Position.Relative) centreCount++;
                else
                {
                    offsetCount++;
                    Assert.IsTrue(layer.style.left.value.value != 0 || layer.style.top.value.value != 0);
                }
            }
            Assert.AreEqual(1, centreCount); // exactly the (0,0) outline draw
            Assert.AreEqual(8, offsetCount);  // the other eight offsets of a size-1 square
        }

        [Test]
        public void EveryAbsoluteLayerIsPinnedToAllFourEdgesSoItMatchesTheBoxsOwnSize()
        {
            // Opus review (U.HARE-T45): a layer that only sets left/top shrinks to its own text size instead of
            // the box's size, so an outline box stretched wider than its text by an ancestor (a window title, a
            // HUD column) visibly misaligns the outline from the centred/right-aligned main text. Symmetric
            // left/right and top/bottom insets keep every layer exactly the box's size, whatever that size is.
            var box = HareUi.OutlineText("HP", 1);
            foreach (var layer in box.Children())
            {
                if (layer.style.position.value != Position.Absolute) continue; // the one in-flow centre layer
                float left = layer.style.left.value.value, right = layer.style.right.value.value;
                float top = layer.style.top.value.value, bottom = layer.style.bottom.value.value;
                Assert.AreEqual(-left, right, 0.001f);
                Assert.AreEqual(-top, bottom, 0.001f);
            }
        }

        [Test]
        public void OutlineTextBoxCarriesTheSharedOutlineTextClass()
        {
            // Theme.uss styles every outline-text box through this class; HareUi must actually add it, not just
            // name the element "outline-text" (a name is not a class and the stylesheet rule would never match).
            var box = HareUi.OutlineText("HP");
            Assert.IsTrue(box.ClassListContains("outline-text"));
        }

        [Test]
        public void OutlineTextColorUsesTheCallersColoursInsteadOfTheDefaults()
        {
            var box = HareUi.OutlineTextColor("Score", 1, Color.cyan, Color.magenta);
            var layers = box.Children().Cast<Label>().ToList();
            Assert.AreEqual(Color.cyan, layers[layers.Count - 1].style.color.value);
            Assert.AreEqual(Color.magenta, layers[0].style.color.value);
        }

        [Test]
        public void OutlineTextAlphaScalesBothColoursAlpha()
        {
            var box = HareUi.OutlineTextAlpha("Score", 1, 0.5f);
            var layers = box.Children().Cast<Label>().ToList();
            Assert.AreEqual(0.5f, layers[layers.Count - 1].style.color.value.a, 0.001f);
            Assert.AreEqual(0.5f, layers[0].style.color.value.a, 0.001f);
        }

        [Test]
        public void OutlineTextColorAlphaCombinesCustomColoursWithAlpha()
        {
            var box = HareUi.OutlineTextColorAlpha("Score", 1, Color.cyan, Color.magenta, 0.25f);
            var layers = box.Children().Cast<Label>().ToList();
            var main = layers[layers.Count - 1].style.color.value;
            var outline = layers[0].style.color.value;
            Assert.AreEqual(0.25f, main.a, 0.001f);
            Assert.AreEqual(0.25f, outline.a, 0.001f);
            Assert.AreEqual(Color.cyan.r, main.r, 0.001f);
            Assert.AreEqual(Color.magenta.r, outline.r, 0.001f);
        }

        [Test]
        public void OutlineTextWithNoParentIsNotAttachedToAnything()
        {
            var box = HareUi.OutlineText("Standalone");
            Assert.IsNull(box.parent);
        }

        [Test]
        public void OutlineTextAddsItselfToTheGivenParentAndCarriesItsClasses()
        {
            var parent = new VisualElement();
            var box = HareUi.OutlineText("HUD", 1, "hud-score", parent);
            Assert.AreSame(parent, box.parent);
            Assert.IsTrue(box.ClassListContains("hud-score"));
        }

        [Test]
        public void ButtonIsStyledAndWrapsAnOutlineTextLabelWithTheSameText()
        {
            bool clicked = false;
            var button = HareUi.Button("Start", () => clicked = true);
            Assert.IsTrue(button.ClassListContains("hare-btn"));
            var inner = button.Q(className: "hare-btn-label");
            Assert.IsNotNull(inner);
            Assert.AreEqual("Start", inner.Children().Cast<Label>().Last().text);
            Assert.IsFalse(clicked); // constructing it never fires the callback
        }

        [Test]
        public void ButtonOutlineSizeControlsItsLabelsLayerCount()
        {
            var button = HareUi.Button("Start", null, outlineSize: 2);
            var inner = button.Q(className: "hare-btn-label");
            Assert.AreEqual(25 + 1, inner.childCount);
        }

        [Test]
        public void BarStartsAtTheGivenFractionAndIsPlainAboveTwoThirds()
        {
            // obj_energybar's own thresholds (HARE.gmx): above two thirds it's neither --low nor --mid.
            var bar = HareUi.Bar(0.8f);
            var fill = bar.Q(name: "fill");
            Assert.AreEqual(80f, fill.style.width.value.value, 0.001f);
            Assert.IsFalse(fill.ClassListContains("hare-bar-fill--low"));
            Assert.IsFalse(fill.ClassListContains("hare-bar-fill--mid"));
        }

        [Test]
        public void BarIsMidAtOrBelowTwoThirdsAboveAThird()
        {
            var bar = HareUi.Bar(0.5f);
            var fill = bar.Q(name: "fill");
            Assert.IsTrue(fill.ClassListContains("hare-bar-fill--mid"));
            Assert.IsFalse(fill.ClassListContains("hare-bar-fill--low"));
        }

        [Test]
        public void BarGoesLowAtOrBelowAThird()
        {
            var bar = HareUi.Bar(HareUi.LowThreshold);
            var fill = bar.Q(name: "fill");
            Assert.IsTrue(fill.ClassListContains("hare-bar-fill--low"));
            Assert.IsFalse(fill.ClassListContains("hare-bar-fill--mid"));
        }

        [Test]
        public void BarExactlyAtTwoThirdsIsMidNotPlain()
        {
            var bar = HareUi.Bar(HareUi.MidThreshold);
            Assert.IsTrue(bar.Q(name: "fill").ClassListContains("hare-bar-fill--mid"));
        }

        [Test]
        public void SetBarClampsOutOfRangeFractions()
        {
            var bar = HareUi.Bar(0f);
            HareUi.SetBar(bar, 1.5f);
            Assert.AreEqual(100f, bar.Q(name: "fill").style.width.value.value, 0.001f);
            HareUi.SetBar(bar, -0.5f);
            Assert.AreEqual(0f, bar.Q(name: "fill").style.width.value.value, 0.001f);
        }

        [Test]
        public void WindowWithATitleAddsAnOutlineTextTitle()
        {
            var window = HareUi.Window("Garage");
            Assert.IsTrue(window.ClassListContains("hare-window"));
            var title = window.Q(className: "hare-window-title");
            Assert.IsNotNull(title);
            Assert.AreEqual("Garage", title.Children().Cast<Label>().Last().text);
        }

        [Test]
        public void WindowWithNoTitleAddsNoTitleElement()
        {
            var window = HareUi.Window();
            Assert.IsNull(window.Q(className: "hare-window-title"));
        }

        [Test]
        public void DialogBuildsAVeiledWindowWithATitleAMessageAndOneButtonPerEntry()
        {
            var root = new VisualElement();
            var veil = HareUi.Dialog("Leave?", "Return to the title screen.", root, ("Yes", null), ("No", null));

            Assert.IsTrue(veil.ClassListContains("hare-veil"));
            Assert.AreSame(root, veil.parent);

            var window = veil.Q(className: "hare-window");
            Assert.IsNotNull(window);
            Assert.AreEqual("Leave?", window.Q(className: "hare-window-title").Children().Cast<Label>().Last().text);
            Assert.IsNotNull(window.Q(className: "hare-dialog-message"));

            var buttons = window.Query<Button>().ToList();
            Assert.AreEqual(2, buttons.Count);
        }

        [Test]
        public void DialogWithNoButtonsStillBuildsTheVeilAndWindow()
        {
            var root = new VisualElement();
            var veil = HareUi.Dialog("Tip", "Press F1 for fullscreen.", root);
            Assert.IsNotNull(veil.Q(className: "hare-window"));
            Assert.AreEqual(0, veil.Query<Button>().ToList().Count);
        }
    }
}
