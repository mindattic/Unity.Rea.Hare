using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Unity.Rea.Hare.Input;

namespace Unity.Rea.Hare.Tests.EditMode
{
    /// <summary>
    /// U.HARE-T129: <see cref="HareBindings"/>' file handling and its fan-out to live <see cref="HareControls"/>, against
    /// a temp <see cref="HareBindings.PathOverride"/> so no profile is touched.
    /// </summary>
    public class HareBindingsTests
    {
        const string Rebound = "<Keyboard>/f12";

        string folder;
        string path;
        readonly System.Collections.Generic.List<HareControls> made = new System.Collections.Generic.List<HareControls>();

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "UnityHareBindingsTest_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            path = Path.Combine(folder, HareBindings.FileName);
            HareBindings.PathOverride = path;
            Reload();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var c in made) UnityEngine.Object.DestroyImmediate(c.asset);
            made.Clear();
            HareBindings.PathOverride = null;
            Reload();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }

        static void Reload() =>
            typeof(HareBindings).GetMethod("Boot", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);

        HareControls NewControls()
        {
            var c = HareBindings.Create();
            made.Add(c);
            return c;
        }

        static int FirstBinding(InputAction action)
        {
            for (int i = 0; i < action.bindings.Count; i++)
                if (!action.bindings[i].isComposite && !action.bindings[i].isPartOfComposite) return i;
            Assert.Fail("no plain binding on " + action.name);
            return -1;
        }

        static void Rebind(HareControls c)
        {
            var action = c.Player.NextAirWeapon;
            action.ApplyBindingOverride(FirstBinding(action), Rebound);
        }

        static string PathOf(HareControls c)
        {
            var action = c.Player.NextAirWeapon;
            return action.bindings[FirstBinding(action)].effectivePath;
        }

        [Test]
        public void WithNoFileEveryBindingIsTheDefault()
        {
            var c = NewControls();
            Assert.AreEqual("", HareBindings.OverridesJson);
            Assert.AreNotEqual(Rebound, PathOf(c));
        }

        [Test]
        public void SaveWritesTheFileAndANewReadReturnsTheSameOverrides()
        {
            var c = NewControls();
            Rebind(c);
            Assert.IsTrue(HareBindings.Save(c));
            Assert.IsTrue(File.Exists(path));
            string saved = HareBindings.OverridesJson;
            StringAssert.Contains(Rebound, saved);
            Assert.AreEqual(saved, File.ReadAllText(path));

            Reload();
            Assert.AreEqual(saved, HareBindings.OverridesJson);
            Assert.AreEqual(Rebound, PathOf(NewControls()));
        }

        [Test]
        public void SaveLeavesNoTemporaryFileBehind()
        {
            var c = NewControls();
            Rebind(c);
            HareBindings.Save(c);
            HareBindings.Save(c);
            Assert.IsFalse(File.Exists(path + ".tmp"));
        }

        [Test]
        public void SaveReappliesTheOverridesToEveryOtherLiveControls()
        {
            var editing = NewControls();
            var other = NewControls();
            Assert.AreNotEqual(Rebound, PathOf(other));

            Rebind(editing);
            HareBindings.Save(editing);

            Assert.AreEqual(Rebound, PathOf(other));
        }

        [Test]
        public void SaveCreatesTheFolderWhenItIsMissing()
        {
            string nested = Path.Combine(folder, "a", "b", HareBindings.FileName);
            HareBindings.PathOverride = nested;
            var c = NewControls();
            Rebind(c);
            Assert.IsTrue(HareBindings.Save(c));
            Assert.IsTrue(File.Exists(nested));
        }

        [Test]
        public void SaveOfNothingIsFalse()
        {
            Assert.IsFalse(HareBindings.Save(null));
            Assert.IsFalse(HareBindings.ResetToDefaults(null));
        }

        [Test]
        public void AnUnreadableFileIsSetAsideAsBadAndTheDefaultsStand()
        {
            File.WriteAllText(path, "this is { not json");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("The saved controls were not read"));
            Reload();

            Assert.AreEqual("", HareBindings.OverridesJson);
            Assert.IsFalse(File.Exists(path));
            Assert.IsTrue(File.Exists(path + ".bad"));
            Assert.AreEqual("this is { not json", File.ReadAllText(path + ".bad"));
            Assert.AreNotEqual(Rebound, PathOf(NewControls()));
        }

        [Test]
        public void ASecondUnreadableFileReplacesTheEarlierBadOne()
        {
            File.WriteAllText(path + ".bad", "old");
            File.WriteAllText(path, "{ nope");
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("The saved controls were not read"));
            Reload();
            Assert.AreEqual("{ nope", File.ReadAllText(path + ".bad"));
        }

        [Test]
        public void ResetToDefaultsDropsTheOverridesEverywhereAndSavesThat()
        {
            var editing = NewControls();
            var other = NewControls();
            Rebind(editing);
            HareBindings.Save(editing);
            Assert.AreEqual(Rebound, PathOf(other));

            Assert.IsTrue(HareBindings.ResetToDefaults(editing));

            Assert.AreNotEqual(Rebound, PathOf(editing));
            Assert.AreNotEqual(Rebound, PathOf(other));
            StringAssert.DoesNotContain(Rebound, File.ReadAllText(path));

            Reload();
            Assert.AreNotEqual(Rebound, PathOf(NewControls()));
        }

        [Test]
        public void ApplyPutsAControlsBackOnTheSavedOverrides()
        {
            var editing = NewControls();
            Rebind(editing);
            HareBindings.Save(editing);

            var c = NewControls();
            var action = c.Player.NextAirWeapon;
            action.ApplyBindingOverride(FirstBinding(action), "<Keyboard>/f11");
            HareBindings.Apply(c);

            Assert.AreEqual(Rebound, PathOf(c));
        }
    }
}
