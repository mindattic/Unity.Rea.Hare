using System;
using System.IO;
using NUnit.Framework;
using Unity.Shared.Core;

namespace Unity.Rea.Hare.Tests.EditMode
{
    /// <summary>
    /// Unity.Rea.Hare's own use of the shared Unity.Shared profile store (U.HARE-T43): <see cref="ActiveProfile"/>
    /// reaches the current profile and its Hare block, and <see cref="HareSettings"/> is typed and persisted the
    /// same way Unity.Shared's own <c>CommonSettings</c> is. Unity.Shared.Core's own format, defaults and
    /// round-trip rules are tested in its own repo; this only checks Hare's wiring into them.
    /// </summary>
    public class ActiveProfileTests
    {
        string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "UnityHareProfilesTest_" + Guid.NewGuid().ToString("N"));
            Profiles.UseRoot(root);
        }

        [TearDown]
        public void TearDown()
        {
            Profiles.Reset();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }

        [Test]
        public void ANewProfileGetsHaresDifficultyMetricAndFullscreenDefaults()
        {
            var hare = ActiveProfile.Hare;
            Assert.AreEqual(HareSettings.DefaultDifficulty, hare.Difficulty);
            Assert.AreEqual(HareSettings.DefaultUseMetric, hare.UseMetric);
            Assert.AreEqual(HareSettings.DefaultFullscreen, hare.Fullscreen);
        }

        [Test]
        public void HaresBlockRoundTripsThroughSettingsJson()
        {
            var hare = ActiveProfile.Hare;
            hare.Difficulty = 75;
            hare.UseMetric = true;
            hare.Fullscreen = false;
            ActiveProfile.Current.SaveSettings();

            // A fresh store over the same root reads the file back rather than reusing anything in memory.
            var reloaded = new ProfileStore(root).Current;
            var reloadedHare = new HareSettings(reloaded.Settings.Game);
            Assert.AreEqual(75, reloadedHare.Difficulty);
            Assert.IsTrue(reloadedHare.UseMetric);
            Assert.IsFalse(reloadedHare.Fullscreen);

            // The common block Unity.Shared defines is there too, on the one settings.json.
            Assert.AreEqual(ActiveProfile.Current.Name, reloaded.Settings.Common.DisplayName);
        }

        [TestCase(-10, 0)]
        [TestCase(0, 0)]
        [TestCase(100, 100)]
        [TestCase(500, 100)]
        public void DifficultyClampsToItsRange(int set, int expected)
        {
            var hare = ActiveProfile.Hare;
            hare.Difficulty = set;
            Assert.AreEqual(expected, hare.Difficulty);
        }

        [Test]
        public void SavesFolderIsTheProfilesSavesFolder()
        {
            string saves = ActiveProfile.SavesFolder;
            Assert.AreEqual(Path.Combine(ActiveProfile.Current.Folder, "Saves"), saves);
            Assert.IsTrue(Directory.Exists(saves), "the campaign save ticket (U.HARE-T65) expects this folder to exist");
        }
    }
}
