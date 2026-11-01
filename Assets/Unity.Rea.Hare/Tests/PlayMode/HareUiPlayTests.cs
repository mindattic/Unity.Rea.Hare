using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Unity.Rea.Hare.UI;

namespace Unity.Rea.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T45: proves the UI foundation actually works together once Unity's own UI Toolkit loop is running —
    /// a live PanelSettings/UIDocument pair, a themed stage confined to the 1920x1080 reference resolution, and a
    /// widget tree (window, bar, button, outline text) laid out with real geometry — not just that each piece is
    /// correct in isolation (HareUiTests, EditMode).
    /// </summary>
    public class HareUiPlayTests
    {
        GameObject host;
        PanelSettings settings;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.Destroy(host);
            if (settings != null) Object.Destroy(settings); // HarePanel.Attach's own PanelSettings isn't a child of host
        }

        [UnityTest]
        public IEnumerator TheWidgetKitLaysOutOnALivePanel()
        {
            host = new GameObject("Hare UI");
            var document = HarePanel.Attach(host);
            settings = document.panelSettings;
            var stage = HarePanel.BuildRoot(document);
            host.AddComponent<HareUiFit>(); // after BuildRoot, as documented — the test below covers the other order

            var window = HareUi.Window("Garage", parent: stage);
            var bar = HareUi.Bar(0.5f, parent: window);
            var button = HareUi.Button("Launch", null, parent: window);
            var outline = HareUi.OutlineText("HP 100", parent: window);

            yield return null;
            yield return null; // let the panel's layout pass settle before reading geometry

            Assert.IsNotNull(document.rootVisualElement.panel, "the document has a live panel, not just a detached tree");
            Assert.IsNotNull(settings.themeStyleSheet, "native controls need a ThemeStyleSheet or they ship unstyled");

            Assert.IsTrue(stage.Contains(window));
            Assert.IsTrue(window.Contains(bar));
            Assert.IsTrue(window.Contains(button));
            Assert.IsTrue(window.Contains(outline));

            Assert.Greater(window.layout.width, 0f, "the window was actually laid out, not left at a zero size");
            Assert.Greater(bar.layout.width, 0f);
            Assert.Greater(button.layout.width, 0f);
            Assert.Greater(outline.layout.width, 0f, "a stretched outline-text box (the window title) still got a real width");

            // The stage is always exactly the reference resolution, confined and centred the same way
            // HareUiFit.ComputeStageRect (pure) says it should be for this run's actual screen size.
            var expected = HareUiFit.ComputeStageRect(Screen.width, Screen.height);
            Assert.AreEqual(expected.width, stage.layout.width, 0.5f);
            Assert.AreEqual(expected.height, stage.layout.height, 0.5f);
            Assert.AreEqual(expected.x, stage.layout.x, 0.5f);
            Assert.AreEqual(expected.y, stage.layout.y, 0.5f);
        }

        [UnityTest]
        public IEnumerator TheFitStillFindsTheStageWhenAttachedBeforeBuildRoot()
        {
            // HareUiFit.Update's first pass used to cache "no stage yet" as if it were a steady state, so a
            // HareUiFit added before BuildRoot (an easy ordering mistake, the opposite of what the class doc
            // asks for) never positioned the stage at all, even once it existed. Caught by this test; fixed by
            // retrying the lookup every frame until the stage is found.
            host = new GameObject("Hare UI");
            var document = HarePanel.Attach(host);
            settings = document.panelSettings;
            host.AddComponent<HareUiFit>(); // deliberately before BuildRoot
            var stage = HarePanel.BuildRoot(document);

            yield return null;
            yield return null;

            var expected = HareUiFit.ComputeStageRect(Screen.width, Screen.height);
            Assert.AreEqual(expected.x, stage.layout.x, 0.5f);
            Assert.AreEqual(expected.y, stage.layout.y, 0.5f);
        }
    }
}
