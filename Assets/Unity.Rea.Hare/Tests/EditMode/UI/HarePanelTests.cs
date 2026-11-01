using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Rea.Hare.Rendering;
using Unity.Rea.Hare.UI;

namespace Unity.Rea.Hare.Tests.EditMode
{
    /// <summary>U.HARE-T45: the PanelSettings every screen is built on matches the original's 1920x1080 reference.</summary>
    public class HarePanelTests
    {
        GameObject host;
        readonly List<Object> created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.DestroyImmediate(host);
            foreach (var o in created) if (o != null) Object.DestroyImmediate(o);
            created.Clear();
        }

        PanelSettings NewSettings()
        {
            var s = HarePanel.CreateSettings();
            created.Add(s);
            return s;
        }

        [Test]
        public void CreateSettingsUsesTheOriginalReferenceResolution()
        {
            var settings = NewSettings();
            Assert.AreEqual(new Vector2Int((int)LetterboxCamera.ReferenceWidth, (int)LetterboxCamera.ReferenceHeight), settings.referenceResolution);
            Assert.AreEqual(1920, settings.referenceResolution.x);
            Assert.AreEqual(1080, settings.referenceResolution.y);
        }

        [Test]
        public void CreateSettingsScalesWithScreenSizeByMatchWidthOrHeight()
        {
            var settings = NewSettings();
            Assert.AreEqual(PanelScaleMode.ScaleWithScreenSize, settings.scaleMode);
            Assert.AreEqual(PanelScreenMatchMode.MatchWidthOrHeight, settings.screenMatchMode);
        }

        [Test]
        public void CreateSettingsLeavesTheBackbufferClearToTheCameraRig()
        {
            var settings = NewSettings();
            Assert.IsFalse(settings.clearColor);
        }

        [Test]
        public void CreateSettingsLoadsTheSharedThemeStyleSheet()
        {
            // Without one, native controls (Button, ScrollView) ship unstyled and the panel logs a runtime
            // warning; Assets/UI Toolkit/'s Editor-auto-generated default theme isn't under Resources and
            // would be stripped from a player build, so HarePanel carries its own (HareTheme.tss).
            var settings = NewSettings();
            Assert.IsNotNull(settings.themeStyleSheet, "Resources/Hare/UI/HareTheme.tss failed to load");
        }

        [Test]
        public void AttachAddsAUIDocumentWiredToTheGivenSettings()
        {
            host = new GameObject("UI");
            var settings = NewSettings();
            var document = HarePanel.Attach(host, settings);
            Assert.IsNotNull(document);
            Assert.AreSame(settings, document.panelSettings);
            Assert.AreSame(document, host.GetComponent<UIDocument>());
        }

        [Test]
        public void AttachBuildsItsOwnSettingsWhenNoneAreGiven()
        {
            host = new GameObject("UI");
            var document = HarePanel.Attach(host);
            Assert.IsNotNull(document.panelSettings);
            created.Add(document.panelSettings);
        }

        [Test]
        public void AttachReusesAnExistingUIDocumentInsteadOfAddingASecondOne()
        {
            host = new GameObject("UI");
            var first = HarePanel.Attach(host, NewSettings());
            var second = HarePanel.Attach(host, NewSettings());
            Assert.AreSame(first, second);
            Assert.AreEqual(1, host.GetComponents<UIDocument>().Length);
        }
    }
}
