using System.IO;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using Unity.Hare.Content;
using Unity.Hare.Popups;

namespace Unity.Hare.Tests.Core
{
    /// <summary>U.HARE-T27: obj_float_text, obj_tip and obj_alert, step by step as HARE.gmx runs them.</summary>
    public class PopupTests
    {
        static FloatTextDefinition FloatDef => ContentRegistry.Default.Popups.FloatText;

        [Test]
        public void AScorePopupRisesAPixelAStepFadesAHundredthAndGoesOnThe101stStep()
        {
            var field = new FloatTextField(FloatDef);
            var text = field.ShowScore(500, 300, 10);
            ClassicAssert.AreEqual("10", text.Text, "string(n)");
            ClassicAssert.AreEqual(1.0, text.Alpha);
            ClassicAssert.AreEqual(290, text.DrawY, "drawn at y - 10");

            field.Step();
            ClassicAssert.AreEqual(299, text.Y, 1e-9, "y -= floatspeed");
            ClassicAssert.AreEqual(0.99, text.Alpha, 1e-9, "alpha -= 0.01");

            // 0.01 off a double 1 a hundred times leaves a hair below 0 (-7.5e-16), so the 101st step destroys it.
            for (int i = 1; i < 100; i++) field.Step();
            Assert.That(field.Texts, Has.Count.EqualTo(1));
            ClassicAssert.AreEqual(200, text.Y, 1e-9);
            Assert.That(text.Alpha, Is.LessThan(0.0));
            field.Step();
            Assert.That(text.Gone, Is.True);
            ClassicAssert.AreEqual(0.0, text.Alpha);
            Assert.That(field.Texts, Is.Empty);
            ClassicAssert.AreEqual(101, text.Steps);
        }

        [Test]
        public void ItFlashesWhiteWhiteRedYellowBlueBlueAndRoundAgain()
        {
            var field = new FloatTextField(FloatDef);
            var text = field.Show(0, 0, "hi");
            ClassicAssert.AreEqual("#FFFFFF", text.Color, "Create's c_white");
            string[] expected = { "#FFFFFF", "#FF0000", "#FFFF00", "#0000FF", "#0000FF", "#FFFFFF", "#FFFFFF", "#FF0000" };
            //                  cycle  1          2          3          4      5 (no case)      0          1          2
            foreach (var color in expected)
            {
                text.Step();
                ClassicAssert.AreEqual(color, text.Color);
            }
        }

        [Test]
        public void ShowFloatTextUsesCreatesDefaultsAndShowFloatTextColorSetsEverything()
        {
            var field = new FloatTextField(FloatDef);
            var plain = field.Show(0, 0, "x");
            ClassicAssert.AreEqual(0.008, plain.FadeSpeed);
            ClassicAssert.AreEqual(1.0, plain.FloatSpeed);
            Assert.That(plain.Flash, Is.True);

            var still = field.ShowColor(10, 20, "y", false, 0.5, 0, "#00FF00", "#FF00FF");
            still.Step();
            ClassicAssert.AreEqual("#00FF00", still.Color, "no flash: the colour it was given");
            ClassicAssert.AreEqual("#FF00FF", still.OutlineColor);
            ClassicAssert.AreEqual(20, still.Y, "floatspeed 0");
            ClassicAssert.AreEqual(0.5, still.Alpha, 1e-9);
            still.Step();
            still.Step();
            Assert.That(still.Gone, Is.True, "alpha 0 is not above 0: destroyed on the next step");
        }

        [Test]
        public void ATipSlidesInWithSfxFlickIdlesAndGoesOnAnyKey()
        {
            var tip = new TipBox(ContentRegistry.Default.Popups.Tip, "Controls#  a#", 1920, 1080);
            ClassicAssert.AreEqual(1920, tip.X, "x = view_wview");
            ClassicAssert.AreEqual(1080 - 250 - 30, tip.Y);
            ClassicAssert.AreEqual(1920 - 548 - 30, tip.EndX);
            ClassicAssert.AreEqual("Controls\n  a\n", tip.DrawText);

            Assert.That(tip.Step(out bool sound), Is.True);
            Assert.That(sound, Is.True, "the slide's first pass plays sfx_flick");
            ClassicAssert.AreEqual(1920 - (1 + 578 * 0.1), tip.X, 1e-9, "x -= 1 + (x - endX) * 0.1");
            tip.Step(out sound);
            Assert.That(sound, Is.False);

            int steps = 2;
            while (tip.Mode == TipMode.SlideIn && steps < 1000) { tip.Step(out sound); Assert.That(sound, Is.False); steps++; }
            ClassicAssert.AreEqual(TipMode.Idle, tip.Mode);
            ClassicAssert.AreEqual(tip.EndX, tip.X);
            Assert.That(steps, Is.LessThan(100));

            Assert.That(tip.Contains(tip.X + 1, tip.Y + 1), Is.True);
            Assert.That(tip.Contains(tip.X - 1, tip.Y + 1), Is.False);
            tip.Dismiss();
            Assert.That(tip.Gone, Is.True);
            Assert.That(tip.Step(out _), Is.False);
        }

        [Test]
        public void ATipSlidingOutAfterIdlingReturnsToStartXSilentlyAndGoes()
        {
            var tip = new TipBox(ContentRegistry.Default.Popups.Tip, "", 1920, 1080);
            while (tip.Mode == TipMode.SlideIn) tip.Step(out _);
            tip.Step(out _); // idle's first pass
            tip.SlideOut();
            tip.Step(out bool sound);
            Assert.That(sound, Is.False, "firstPass was spent by the idle");
            ClassicAssert.AreEqual(tip.EndX + 2 + (tip.StartX - tip.EndX) * 0.1, tip.X, 1e-9);
            int guard = 0;
            while (!tip.Gone && guard++ < 1000) tip.Step(out _);
            Assert.That(tip.Gone, Is.True);
            ClassicAssert.AreEqual(tip.StartX, tip.X);
            ClassicAssert.AreEqual(TipMode.Off, tip.Mode);
        }

        [Test]
        public void AnAlertRidesTheEarthsRimAndPingsEvery30StepsOnlyWhileTheMapIsIdle()
        {
            var definition = ContentRegistry.Default.Popups.Alert;
            Assert.That(definition.Missions, Has.Length.EqualTo(10), "one per original level, owner-directed (BIBLE V42)");
            CollectionAssert.AreEqual(new float[] { 16, 150, 270, 310 }, new[] { definition.Missions[0].Degree, definition.Missions[1].Degree,
                definition.Missions[2].Degree, definition.Missions[3].Degree }, "obj_map's original four first");
            for (int i = 0; i < 10; i++)
            {
                ClassicAssert.AreEqual(i + 1, definition.Missions[i].ScenarioId);
                ClassicAssert.AreEqual(i + 1, definition.Missions[i].Level, "mission n launches level_0n");
                ClassicAssert.AreEqual(i == 7, definition.Missions[i].Boss, "level_08, Scrap Mental, is the boss");
            }
            var alert = new AlertMarker(definition, 90, 3);

            alert.Step(960, 1000, 0, true, out _, out _);
            ClassicAssert.AreEqual(90, alert.ImageAngle);
            ClassicAssert.AreEqual(960, alert.X, 1e-9);
            ClassicAssert.AreEqual(1000 - 550, alert.Y, 1e-9, "lengthdir_y: 90 degrees is up");

            int pings = 0;
            for (int step = 2; step <= 60; step++)
            {
                bool idle = step <= 30;
                if (alert.Step(960, 1000, step, idle, out double px, out double py))
                {
                    pings++;
                    ClassicAssert.AreEqual(30, step, "alarm[0] = 30 fires on the 30th step");
                    // the ping uses image_angle from the step before (the alarm runs before the Step)
                    ClassicAssert.AreEqual(960 + AlertMarker.LengthDirX(550, 90 + 29), px, 1e-6);
                    ClassicAssert.AreEqual(1000 + AlertMarker.LengthDirY(550, 90 + 29), py, 1e-6);
                }
            }
            ClassicAssert.AreEqual(1, pings, "the 60th step's alarm finds obj_map moving: no ring, but it re-arms");
            ClassicAssert.AreEqual(30, alert.Alarm);
        }

        [Test]
        public void TheAlertAnimatesAtAFifthOfAFrameAStep()
        {
            var alert = new AlertMarker(ContentRegistry.Default.Popups.Alert, 0, 1);
            for (int i = 0; i < 5; i++) alert.Step(0, 0, 0, false, out _, out _);
            ClassicAssert.AreEqual(1, alert.Frame);
            for (int i = 0; i < 25; i++) alert.Step(0, 0, 0, false, out _, out _);
            ClassicAssert.AreEqual(0, alert.Frame, "six frames wrap after 30 steps");
        }

        [Test]
        public void TheShippedPopupsSectionMatchesTheDefaults()
        {
            var dir = TestContext.CurrentContext.TestDirectory;
            string path = null;
            for (var d = new DirectoryInfo(dir); d != null; d = d.Parent)
            {
                var candidate = Path.Combine(d.FullName, "Assets", "Unity.Rea.Hare", "Resources", "Hare", "Content", "GameContent.json");
                if (File.Exists(candidate)) { path = candidate; break; }
            }
            Assert.That(path, Is.Not.Null);
            var fromFile = ContentRegistry.FromJson(File.ReadAllText(path)).Popups;
            var defaults = ContentRegistry.Default.Popups;
            ClassicAssert.AreEqual(defaults.FloatText.FadeSpeed, fromFile.FloatText.FadeSpeed);
            ClassicAssert.AreEqual(defaults.FloatText.ScoreFadeSpeed, fromFile.FloatText.ScoreFadeSpeed);
            CollectionAssert.AreEqual(defaults.FloatText.FlashColors, fromFile.FloatText.FlashColors);
            ClassicAssert.AreEqual(defaults.Tip.MapText, fromFile.Tip.MapText);
            ClassicAssert.AreEqual(defaults.Tip.IntroText, fromFile.Tip.IntroText);
            ClassicAssert.AreEqual(defaults.Tip.Width, fromFile.Tip.Width);
            ClassicAssert.AreEqual(defaults.Alert.Length, fromFile.Alert.Length);
            ClassicAssert.AreEqual(defaults.Alert.Missions.Length, fromFile.Alert.Missions.Length);
            for (int i = 0; i < defaults.Alert.Missions.Length; i++)
            {
                ClassicAssert.AreEqual(defaults.Alert.Missions[i].Degree, fromFile.Alert.Missions[i].Degree);
                ClassicAssert.AreEqual(defaults.Alert.Missions[i].ScenarioId, fromFile.Alert.Missions[i].ScenarioId);
                ClassicAssert.AreEqual(defaults.Alert.Missions[i].Level, fromFile.Alert.Missions[i].Level);
                ClassicAssert.AreEqual(defaults.Alert.Missions[i].Boss, fromFile.Alert.Missions[i].Boss);
            }
        }

        [Test]
        public void ABadPopupsSectionIsReported()
        {
            var e = Assert.Throws<ContentValidationException>(() =>
                ContentRegistry.FromJson("{ \"popups\": { \"FloatText\": { \"FadeSpeed\": 0, \"FlashColors\": [\"red\"] } } }"));
            Assert.That(string.Join("\n", e.Errors), Does.Contain("popups.FloatText.FadeSpeed").And.Contain("popups.FloatText.FlashColors[0]"));
        }
    }
}
