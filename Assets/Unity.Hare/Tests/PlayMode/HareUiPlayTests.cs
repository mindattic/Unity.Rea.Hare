using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Hare.UI;

namespace Unity.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T45: proves the UI foundation actually works together once Unity's own UI Toolkit loop is running —
    /// a live PanelSettings/UIDocument pair, Theme.uss applied, and a widget tree (window, bar, button, outline
    /// text) laid out with real geometry — not just that each piece is correct in isolation (HareUiTests, EditMode).
    /// </summary>
    public class HareUiPlayTests
    {
        GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.Destroy(host);
        }

        [UnityTest]
        public IEnumerator TheWidgetKitLaysOutOnALivePanel()
        {
            host = new GameObject("Hare UI");
            var document = HarePanel.Attach(host);
            host.AddComponent<HareUiFit>();
            var root = HarePanel.BuildRoot(document);

            var window = HareUi.Window("Garage", parent: root);
            var bar = HareUi.Bar(0.5f, parent: window);
            var button = HareUi.Button("Launch", null, parent: window);
            var outline = HareUi.OutlineText("HP 100", parent: window);

            yield return null;
            yield return null; // let the panel's layout pass settle before reading geometry

            Assert.IsNotNull(document.rootVisualElement.panel, "the document has a live panel, not just a detached tree");
            Assert.IsTrue(root.Contains(window));
            Assert.IsTrue(window.Contains(bar));
            Assert.IsTrue(window.Contains(button));
            Assert.IsTrue(window.Contains(outline));

            Assert.Greater(window.layout.width, 0f, "the window was actually laid out, not left at a zero size");
            Assert.Greater(bar.layout.width, 0f);
            Assert.Greater(button.layout.width, 0f);
            Assert.Greater(outline.layout.width, 0f);
        }
    }
}
