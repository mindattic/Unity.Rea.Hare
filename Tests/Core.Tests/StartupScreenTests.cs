using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using StringAssert = NUnit.Framework.Legacy.StringAssert;
using Unity.Hare.Content;
using Unity.Hare.Flow;
using Unity.Hare.Rooms;

namespace Unity.Hare.Tests.Core
{
    /// <summary>
    /// The startup room (U.HARE-T51): obj_startup's Create, its three alarms re-rolling the circuit boards' speeds, the
    /// backgrounds' scroll, its any-key and mouse events, and the startup section as data.
    /// </summary>
    public class StartupScreenTests
    {
        static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>Deals the queued numbers in order and records every irandom_range it was asked for.</summary>
        sealed class ScriptedRandom : IRandomSource
        {
            readonly Queue<int> next = new Queue<int>();
            public readonly List<(int Min, int Max)> Asked = new List<(int, int)>();

            public ScriptedRandom(params int[] values) { foreach (var v in values) next.Enqueue(v); }

            public void Queue(params int[] values) { foreach (var v in values) next.Enqueue(v); }

            public int IRandom(int min, int max)
            {
                Asked.Add((min, max));
                return next.Count > 0 ? next.Dequeue() : min;
            }

            public float Range(float min, float max) => min;
        }

        static StartupScreen Screen(IRandomSource random) => new StartupScreen(ContentRegistry.Default.Startup, 60, random);

        // ---- The data ----

        [Test]
        public void TheRoomStartsStartupRoomGmxsBackgroundsBackToFront()
        {
            var screen = Screen(new ScriptedRandom());
            CollectionAssert.AreEqual(
                new[] { "bg_circuitboard3", "bg_circuitboard2", "bg_circuitboard", "bg_semiopaque80", "bg_rabbit_schematic_transparent" },
                screen.Layers.Select(l => l.Name));
            CollectionAssert.AreEqual(new[] { (1, -1), (1, 1), (-2, 1), (0, 0), (0, 0) }, screen.Layers.Select(l => (l.HSpeed, l.VSpeed)));
            CollectionAssert.AreEqual(new[] { true, true, true, false, false }, screen.Layers.Select(l => l.Drifts),
                "alarm[0], [1] and [2] drift backgrounds 0, 1 and 2");
            Assert.IsTrue(screen.Layers.All(l => l.X == 0 && l.Y == 0));
        }

        [Test]
        public void TheNoteIsObjStartupsStrTextInRedOutlinedBlack()
        {
            var tuning = ContentRegistry.Default.Startup;
            StringAssert.StartsWith("Note: If application is running slowly and you have a wireless Xbox 360 controller plugged in, try turning the controller on.\n", tuning.Note);
            StringAssert.EndsWith("\nThis is a known issue with the development software.", tuning.Note);
            Assert.AreEqual("#FF0000", tuning.NoteColor, "c_red");
            Assert.AreEqual("#000000", tuning.NoteOutlineColor, "c_black");
            Assert.AreEqual(2, tuning.NoteOutline);
            Assert.AreEqual(60, tuning.NoteBottomMargin, "drawn at room_height - 60");
            Assert.AreEqual(RoomId.Title, tuning.ContinueRoom);
        }

        [Test]
        public void TheShippedDataFileMatchesTheCompiledInDefaults()
        {
            var path = System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory,
                "../../../../../Assets/Unity.Rea.Hare/Resources/Hare/Content/GameContent.json");
            var loaded = ContentRegistry.FromJson(System.IO.File.ReadAllText(path)).Startup;
            var defaults = ContentRegistry.Default.Startup;
            CollectionAssert.AreEqual(defaults.Backgrounds.Select(b => (b.Name, b.HSpeed, b.VSpeed, b.Drifts)),
                loaded.Backgrounds.Select(b => (b.Name, b.HSpeed, b.VSpeed, b.Drifts)));
            Assert.AreEqual((defaults.ShiftMinSeconds, defaults.ShiftMaxSeconds, defaults.DriftSpeedMin, defaults.DriftSpeedMax),
                (loaded.ShiftMinSeconds, loaded.ShiftMaxSeconds, loaded.DriftSpeedMin, loaded.DriftSpeedMax));
            Assert.AreEqual(defaults.Note, loaded.Note);
            Assert.AreEqual((defaults.NoteBottomMargin, defaults.NoteOutline, defaults.NoteColor, defaults.NoteOutlineColor),
                (loaded.NoteBottomMargin, loaded.NoteOutline, loaded.NoteColor, loaded.NoteOutlineColor));
            Assert.AreEqual((defaults.FullscreenOnEntry, defaults.ContinueRoom), (loaded.FullscreenOnEntry, loaded.ContinueRoom));
        }

        [Test]
        public void ABrokenStartupSectionIsRefusedListingEveryProblem()
        {
            var e = Assert.Throws<ContentValidationException>(() => ContentRegistry.FromJson(
                "{ \"startup\": { \"Backgrounds\": [ { \"Name\": \"\" } ], \"ShiftMinSeconds\": 5, \"ShiftMaxSeconds\": 2," +
                " \"DriftSpeedMin\": 2, \"DriftSpeedMax\": -2, \"NoteColor\": \"red\" } }"));
            string all = string.Join("\n", e.Errors);
            StringAssert.Contains("startup.Backgrounds[0].Name", all);
            StringAssert.Contains("startup.ShiftMaxSeconds", all);
            StringAssert.Contains("startup.DriftSpeedMax", all);
            StringAssert.Contains("startup.NoteColor", all);
        }

        // ---- Create ----

        [Test]
        public void CreateGoesFullscreenAndArmsTheThreeAlarmsForOneToTwelveSeconds()
        {
            var random = new ScriptedRandom(100, 200, 300);
            var screen = Screen(random);
            var result = screen.Execute(new CreateStartup(P1));

            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(1, result.Events.Count);
            Assert.IsInstanceOf<FullscreenRequested>(result.Events[0], "window_set_fullscreen(true)");
            CollectionAssert.AreEqual(new[] { (60, 720), (60, 720), (60, 720) }, random.Asked,
                "irandom_range(room_speed * shift_min, room_speed * shift_max), alarm[0] then [1] then [2]");
            CollectionAssert.AreEqual(new[] { 100, 200, 300, -1, -1 }, screen.Layers.Select(l => l.Alarm));
            Assert.IsFalse(screen.Execute(new CreateStartup(P1)).Accepted, "Create runs once");
        }

        [Test]
        public void WithFullscreenOnEntryOffCreateLeavesTheWindowAlone()
        {
            var tuning = ContentRegistry.FromJson("{ \"startup\": { \"FullscreenOnEntry\": false } }").Startup;
            var screen = new StartupScreen(tuning, 60, new ScriptedRandom());
            Assert.IsEmpty(screen.Execute(new CreateStartup(P1)).Events);
        }

        [Test]
        public void NothingStepsBeforeCreate()
        {
            var screen = Screen(new ScriptedRandom());
            Assert.IsFalse(screen.Execute(new StepStartup(P1)).Accepted);
            Assert.IsTrue(screen.Layers.All(l => l.X == 0 && l.Y == 0));
        }

        // ---- Step ----

        [Test]
        public void EachStepMovesEveryBackgroundByItsSpeeds()
        {
            var screen = Screen(new ScriptedRandom(500, 500, 500));
            screen.Execute(new CreateStartup(P1));
            for (int i = 0; i < 10; i++) screen.Execute(new StepStartup(P1));

            CollectionAssert.AreEqual(new[] { (10, -10), (10, 10), (-20, 10), (0, 0), (0, 0) }, screen.Layers.Select(l => (l.X, l.Y)));
            CollectionAssert.AreEqual(new[] { 490, 490, 490, -1, -1 }, screen.Layers.Select(l => l.Alarm));
        }

        [Test]
        public void AnAlarmReRollsItsBackgroundsSpeedsInMinusTwoToTwoAndArmsAgain()
        {
            var random = new ScriptedRandom(3, 600, 700);
            var screen = Screen(random);
            screen.Execute(new CreateStartup(P1));
            screen.Execute(new StepStartup(P1));
            screen.Execute(new StepStartup(P1));
            random.Asked.Clear();
            random.Queue(-2, 2, 250);

            var result = screen.Execute(new StepStartup(P1)); // alarm[0] counts 3, 2, 1 and fires on this, its third step

            CollectionAssert.AreEqual(new[] { (-2, 2), (-2, 2), (60, 720) }, random.Asked, "hspeed, vspeed, then the next shift");
            var drift = result.Events.OfType<DriftChanged>().Single();
            Assert.AreEqual((0, -2, 2, 250), (drift.Layer, drift.HSpeed, drift.VSpeed, drift.NextShiftSteps));
            var layer = screen.Layers[0];
            Assert.AreEqual((-2, 2, 250), (layer.HSpeed, layer.VSpeed, layer.Alarm));
            Assert.AreEqual((1 + 1 - 2, -1 - 1 + 2), (layer.X, layer.Y), "the re-rolled speed moves the background on the step it fires");
            Assert.AreEqual((1, 1), (screen.Layers[1].HSpeed, screen.Layers[1].VSpeed), "the other boards keep their speeds");
        }

        [Test]
        public void AlarmsFiringOnTheSameStepRollInAlarmOrder()
        {
            var random = new ScriptedRandom(1, 1, 1);
            var screen = Screen(random);
            screen.Execute(new CreateStartup(P1));
            random.Queue(1, 0, 61, 2, -1, 62, -1, -2, 63);

            var result = screen.Execute(new StepStartup(P1));

            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, result.Events.OfType<DriftChanged>().Select(d => d.Layer));
            CollectionAssert.AreEqual(new[] { (1, 0, 61), (2, -1, 62), (-1, -2, 63) }, screen.Layers.Take(3).Select(l => (l.HSpeed, l.VSpeed, l.Alarm)));
        }

        [Test]
        public void OverManyMinutesTheBoardsKeepDriftingWithinTheirRanges()
        {
            var screen = Screen(new SeededRandom(51));
            screen.Execute(new CreateStartup(P1));
            int drifts = 0;
            for (int step = 0; step < 60 * 60 * 5; step++)
            {
                foreach (var drift in screen.Execute(new StepStartup(P1)).Events.OfType<DriftChanged>())
                {
                    drifts++;
                    Assert.That(drift.Layer, Is.InRange(0, 2));
                    Assert.That(drift.HSpeed, Is.InRange(-2, 2));
                    Assert.That(drift.VSpeed, Is.InRange(-2, 2));
                    Assert.That(drift.NextShiftSteps, Is.InRange(60, 720));
                }
            }
            // Five minutes, three boards, a re-roll every 6.5 s on average: about 140.
            Assert.That(drifts, Is.InRange(75, 300));
            Assert.IsTrue(screen.Layers.Skip(3).All(l => l.X == 0 && l.Y == 0 && l.Alarm == -1), "the veil and schematic never move");
        }

        [Test]
        public void WrapPutsATilesStartInsideTheImage()
        {
            Assert.AreEqual(0, StartupLayer.Wrap(0, 1920));
            Assert.AreEqual(5, StartupLayer.Wrap(1925, 1920));
            Assert.AreEqual(1918, StartupLayer.Wrap(-2, 1920));
            Assert.AreEqual(0, StartupLayer.Wrap(-3840, 1920));
        }

        // ---- Any key, the mouse ----

        [Test]
        public void AKeyOrTheMouseGoesToTheTitleOnce()
        {
            var screen = Screen(new ScriptedRandom());
            screen.Execute(new CreateStartup(P1));
            var result = screen.Execute(new ContinueFromStartup(P1));

            Assert.IsTrue(result.Accepted);
            var request = (ContinueRequested)result.Events.Single();
            Assert.AreEqual(RoomId.Title, request.Room, "room_goto(room_title)");
            Assert.AreEqual(P1, request.Id);
            Assert.IsTrue(screen.Continued);
            Assert.IsFalse(screen.Execute(new ContinueFromStartup(P1)).Accepted, "the held button asks once");
        }
    }
}
