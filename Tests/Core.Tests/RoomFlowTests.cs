using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using StringAssert = NUnit.Framework.Legacy.StringAssert;
using Unity.Hare.Content;
using Unity.Hare.Flow;

namespace Unity.Hare.Tests.Core
{
    /// <summary>
    /// The room flow (U.HARE-T50): next_room, the obj_fade_* objects' Create and Step, room_goto, the room
    /// controllers' Esc and F1 key-press events, and the rooms table as data.
    /// </summary>
    public class RoomFlowTests
    {
        static readonly PlayerId P1 = new PlayerId(1);

        RoomFlow flow;

        [SetUp]
        public void SetUp() => flow = new RoomFlow(ContentRegistry.Default);

        FlowResult Do(FlowCommand command) => flow.Execute(command);

        /// <summary>Steps until the flow raises something or <paramref name="limit"/> steps pass; returns the steps taken.</summary>
        int StepUntilEvent(out FlowResult result, int limit = 1000)
        {
            for (int i = 1; i <= limit; i++)
            {
                result = Do(new StepRoom(P1));
                if (result.Events.Count > 0) return i;
            }
            result = default;
            return -1;
        }

        // ---- The table ----

        [Test]
        public void TheTableHoldsEveryRoomWithItsRoomGmxSizeAndSpeed()
        {
            var table = ContentRegistry.Default.Rooms;
            CollectionAssert.AreEquivalent(RoomList.All, table.Rooms.Select(r => r.Room));
            void Check(RoomId room, int w, int h, int speed)
            {
                var d = table.Get(room);
                Assert.AreEqual(w, d.Width, room + " width");
                Assert.AreEqual(h, d.Height, room + " height");
                Assert.AreEqual(speed, d.StepsPerSecond, room + " room_speed");
            }
            Check(RoomId.Startup, 1920, 1080, 60);
            Check(RoomId.Title, 1920, 1080, 60);
            Check(RoomId.Intro, 1920, 1080, 60);
            Check(RoomId.GameScreen, 3840, 2160, 120);
            Check(RoomId.SideScroll, 12000, 1080, 120);
            Check(RoomId.Shaft, 1920, 12000, 120);
            Check(RoomId.Map, 1920, 1080, 60);
            Check(RoomId.Story, 1920, 1080, 60);
            Check(RoomId.Upgrade, 1920, 1080, 60);
        }

        [Test]
        public void TheRoomsAreNamedAsInHareGmx_AndRoomBoss1IsNotOne()
        {
            CollectionAssert.AreEqual(
                new[] { "startup", "room_title", "room_intro", "room_gamescreen", "room_sidescroll", "room_shaft", "room_map", "room_story", "room_upgrade" },
                RoomList.All.Select(RoomList.GmlName));
            foreach (var room in RoomList.All)
            {
                Assert.IsTrue(RoomList.TryParse(RoomList.GmlName(room), out var parsed));
                Assert.AreEqual(room, parsed);
            }
            Assert.IsFalse(RoomList.TryParse("room_boss1", out _), "room_boss1 is unreferenced in the original");
        }

        [Test]
        public void FadesEscAndF1BelongToTheRoomsWhoseControllersHaveThem()
        {
            var table = ContentRegistry.Default.Rooms;
            CollectionAssert.AreEquivalent(new[] { RoomId.Startup, RoomId.Intro },
                RoomList.All.Where(r => table.Get(r).Fade == FadeKind.None), "obj_startup has no fade; obj_intro's is commented out");
            CollectionAssert.AreEquivalent(new[] { RoomId.GameScreen, RoomId.SideScroll, RoomId.Shaft },
                RoomList.All.Where(r => table.Get(r).Fade == FadeKind.NextLevel), "obj_game's obj_fade_game");
            CollectionAssert.AreEquivalent(new[] { RoomId.Intro, RoomId.GameScreen, RoomId.SideScroll, RoomId.Shaft, RoomId.Map, RoomId.Story },
                RoomList.All.Where(r => table.Get(r).EscapeToTitle), "obj_intro, obj_game, obj_map and obj_story have Esc events");
            CollectionAssert.AreEquivalent(new[] { RoomId.Title, RoomId.GameScreen, RoomId.SideScroll, RoomId.Shaft },
                RoomList.All.Where(r => table.Get(r).FullscreenKey), "obj_title and obj_game have F1 events");
            Assert.AreEqual(0.03, table.FadeSpeed);
            Assert.AreEqual(RoomId.Startup, table.FirstRoom);
            Assert.AreEqual(RoomId.Title, table.EscapeRoom);
            Assert.AreEqual(RoomId.GameScreen, table.StartingNextRoom, "globals.gml: next_room = room_gamescreen");
        }

        [Test]
        public void TheShippedDataFileMatchesTheCompiledInDefaults()
        {
            var path = System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory,
                "../../../../../Assets/Unity.Rea.Hare/Resources/Hare/Content/GameContent.json");
            var loaded = ContentRegistry.FromJson(System.IO.File.ReadAllText(path)).Rooms;
            var defaults = ContentRegistry.Default.Rooms;
            foreach (var room in RoomList.All)
            {
                var a = loaded.Get(room);
                var b = defaults.Get(room);
                Assert.AreEqual((b.Width, b.Height, b.StepsPerSecond, b.Fade, b.EscapeToTitle, b.FullscreenKey),
                    (a.Width, a.Height, a.StepsPerSecond, a.Fade, a.EscapeToTitle, a.FullscreenKey), room.ToString());
            }
            Assert.AreEqual(defaults.FadeSpeed, loaded.FadeSpeed);
            Assert.AreEqual(defaults.FirstRoom, loaded.FirstRoom);
            Assert.AreEqual(defaults.EscapeRoom, loaded.EscapeRoom);
            Assert.AreEqual(defaults.StartingNextRoom, loaded.StartingNextRoom);
            Assert.AreEqual(defaults.MaxStepsPerFrame, loaded.MaxStepsPerFrame);
        }

        [Test]
        public void ABrokenRoomsSectionIsRefusedListingEveryProblem()
        {
            var e = Assert.Throws<ContentValidationException>(() => ContentRegistry.FromJson(
                "{ \"rooms\": { \"Rooms\": [ { \"Room\": \"Title\", \"Width\": 0, \"Height\": 1080, \"StepsPerSecond\": 60 }," +
                " { \"Room\": \"Title\", \"Width\": 1920, \"Height\": 1080, \"StepsPerSecond\": 60 } ], \"FadeSpeed\": 0 } }"));
            string all = string.Join("\n", e.Errors);
            StringAssert.Contains("rooms.Rooms[0].Width", all);
            StringAssert.Contains("listed more than once", all);
            StringAssert.Contains("Startup (startup) is missing", all);
            StringAssert.Contains("rooms.FadeSpeed", all);
        }

        // ---- room_goto and next_room ----

        [Test]
        public void ANewFlowIsInNoRoomWithGlobalsGmlsNextRoom()
        {
            Assert.IsNull(flow.Current);
            Assert.IsNull(flow.Fade);
            Assert.AreEqual(RoomId.GameScreen, flow.NextRoom);
            Assert.AreEqual(0f, flow.FadeOpacity);
            Assert.IsFalse(Do(new StepRoom(P1)).Accepted, "nothing steps before the first room");
            Assert.IsFalse(Do(new PressEscape(P1)).Accepted);
        }

        [Test]
        public void GoToRoomEntersTheRoomAtOnceAndRaisesRoomChanged()
        {
            var r = Do(new GoToRoom(P1, RoomId.Startup));
            Assert.IsTrue(r.Accepted);
            var changed = r.Events.OfType<RoomChanged>().Single();
            Assert.IsNull(changed.From);
            Assert.AreEqual(RoomId.Startup, changed.To);
            Assert.AreEqual(P1, changed.Id);
            Assert.AreEqual(RoomId.Startup, flow.Current);
            Assert.AreEqual(60, flow.StepsPerSecond);
            Assert.IsNull(flow.Fade, "startup has no obj_fade_*");

            changed = Do(new GoToRoom(P1, RoomId.Title)).Events.OfType<RoomChanged>().Single();
            Assert.AreEqual(RoomId.Startup, changed.From);
            Assert.AreEqual(RoomId.Title, changed.To);
            Assert.AreEqual(FadeKind.NextRoom, flow.Fade.Kind);
        }

        [Test]
        public void EveryRoomWithAFadeComesUpFromBlack()
        {
            Do(new GoToRoom(P1, RoomId.Title));
            Assert.AreEqual(1.0, flow.Fade.Alpha, "obj_fade_*'s Create: image_alpha = 1");
            Assert.AreEqual(FadeMode.FadeOut, flow.Fade.Mode);
            Assert.AreEqual(1f, flow.FadeOpacity);

            // Step one keeps the first pass's image_alpha = 1 and takes the first 0.03 off; the fade-out ends on
            // the step after image_alpha drops to zero or below (1 - 34 * 0.03 = -0.02), turning the fade off.
            int steps = 0;
            while (flow.Fade.Mode == FadeMode.FadeOut)
            {
                Assert.IsFalse(Do(new StepRoom(P1)).Events.Any(), "the fade-out raises nothing");
                steps++;
            }
            Assert.AreEqual(35, steps);
            Assert.AreEqual(FadeMode.Off, flow.Fade.Mode);
            Assert.AreEqual(0f, flow.FadeOpacity, "drawn alpha is clamped at zero");

            Do(new StepRoom(P1));
            Assert.AreEqual(0.0, flow.Fade.Alpha, "mode off's first pass: image_alpha = 0");
        }

        [Test]
        public void FadeToRoomGoesToBlackThenToNextRoom()
        {
            Do(new GoToRoom(P1, RoomId.Title));
            var r = Do(new FadeToRoom(P1, RoomId.Story));
            Assert.IsTrue(r.Accepted, "a button may start the fade-in even mid fade-out (obj_btn_quickstart checks nothing)");
            Assert.AreEqual(RoomId.Story, r.Events.OfType<FadeInStarted>().Single().NextRoom);
            Assert.AreEqual(RoomId.Story, flow.NextRoom, "next_room = room_story");
            Assert.AreEqual(FadeMode.FadeIn, flow.Fade.Mode);

            // First step: image_alpha = 0, then += 0.03. Opaque once image_alpha >= 1 (34 steps, 1.02), and the
            // step after that runs room_goto(next_room).
            int steps = StepUntilEvent(out var result);
            Assert.AreEqual(35, steps);
            var changed = result.Events.OfType<RoomChanged>().Single();
            Assert.AreEqual(RoomId.Title, changed.From);
            Assert.AreEqual(RoomId.Story, changed.To);
            Assert.AreEqual(RoomId.Story, flow.Current);
            Assert.AreEqual(FadeMode.FadeOut, flow.Fade.Mode, "the new room's own obj_fade_story, fading out");
            Assert.AreEqual(1f, flow.FadeOpacity);
        }

        [Test]
        public void TheFadeInActsOnlyOnce()
        {
            Do(new GoToRoom(P1, RoomId.GameScreen));
            Do(new StartFadeIn(P1));
            StepUntilEvent(out var result);
            Assert.IsInstanceOf<LevelAdvanceRequested>(result.Events.Single(), "obj_fade_game: current_level += 1; setup_level();");
            Assert.AreEqual(RoomId.GameScreen, flow.Current, "obj_fade_game does not leave the room itself");
            Assert.IsTrue(flow.Fade.ExecuteOnce);
            for (int i = 0; i < 100; i++)
                Assert.IsFalse(Do(new StepRoom(P1)).Events.Any(), "executeOnce: never again");
            Assert.AreEqual(1f, flow.FadeOpacity, "the screen stays black until the level setup reloads the room");
        }

        [Test]
        public void AnIdleOnlyButtonIsRefusedWhileAFadeRuns()
        {
            Do(new GoToRoom(P1, RoomId.Title));
            Assert.IsFalse(Do(new FadeToRoom(P1, RoomId.Story, onlyWhenIdle: true)).Accepted,
                "obj_btn_campaign checks obj_fade_title.mode == \"off\" first");
            Assert.AreEqual(RoomId.GameScreen, flow.NextRoom, "next_room untouched");
            while (flow.Fade.Mode != FadeMode.Off) Do(new StepRoom(P1));
            Assert.IsTrue(Do(new FadeToRoom(P1, RoomId.Story, onlyWhenIdle: true)).Accepted);
            Assert.IsFalse(Do(new FadeToRoom(P1, RoomId.Map, onlyWhenIdle: true)).Accepted, "already fading in");
            Assert.AreEqual(RoomId.Story, flow.NextRoom);
        }

        [Test]
        public void ARoomWithNoFadeRefusesToFade()
        {
            Do(new GoToRoom(P1, RoomId.Intro));
            Assert.IsFalse(Do(new FadeToRoom(P1, RoomId.GameScreen)).Accepted);
            Assert.IsFalse(Do(new StartFadeIn(P1)).Accepted);
            Assert.IsTrue(Do(new StepRoom(P1)).Accepted, "the room still steps");
            Assert.AreEqual(0f, flow.FadeOpacity);
        }

        [Test]
        public void EnterRoomRestartsTheRoomsFadeWithoutARoomChange()
        {
            Do(new GoToRoom(P1, RoomId.GameScreen));
            while (flow.Fade.Mode != FadeMode.Off) Do(new StepRoom(P1));
            var r = Do(new EnterRoom(P1, RoomId.GameScreen));
            Assert.AreEqual(RoomId.GameScreen, r.Events.OfType<RoomEntered>().Single().Room);
            Assert.IsFalse(r.Events.OfType<RoomChanged>().Any());
            Assert.AreEqual(FadeMode.FadeOut, flow.Fade.Mode, "room_restart re-runs obj_game's Create: a fresh obj_fade_game");
            Assert.AreEqual(1f, flow.FadeOpacity);
        }

        // ---- Esc and F1 ----

        [TestCase(RoomId.Intro)]
        [TestCase(RoomId.GameScreen)]
        [TestCase(RoomId.SideScroll)]
        [TestCase(RoomId.Shaft)]
        [TestCase(RoomId.Map)]
        [TestCase(RoomId.Story)]
        public void EscReturnsToTheTitleAtOnce(RoomId room)
        {
            Do(new GoToRoom(P1, room));
            var r = Do(new PressEscape(P1));
            Assert.IsTrue(r.Accepted);
            var changed = r.Events.OfType<RoomChanged>().Single();
            Assert.AreEqual(room, changed.From);
            Assert.AreEqual(RoomId.Title, changed.To, "room_goto(room_title), no fade");
            Assert.AreEqual(RoomId.Title, flow.Current);
        }

        [TestCase(RoomId.Startup)]
        [TestCase(RoomId.Title)]
        [TestCase(RoomId.Upgrade)]
        public void EscDoesNothingWhereTheControllerHasNoEscEvent(RoomId room)
        {
            Do(new GoToRoom(P1, room));
            var r = Do(new PressEscape(P1));
            Assert.IsFalse(r.Accepted);
            Assert.IsEmpty(r.Events);
            Assert.AreEqual(room, flow.Current);
        }

        [Test]
        public void EscMidFadeLeavesAtOnceAndTheTitleComesUpFromBlack()
        {
            Do(new GoToRoom(P1, RoomId.Map));
            Do(new FadeToRoom(P1, RoomId.GameScreen));
            for (int i = 0; i < 10; i++) Do(new StepRoom(P1));
            Do(new PressEscape(P1));
            Assert.AreEqual(RoomId.Title, flow.Current);
            Assert.AreEqual(FadeMode.FadeOut, flow.Fade.Mode);
            Assert.AreEqual(RoomId.GameScreen, flow.NextRoom, "next_room is a global: Esc leaves it as it was");
        }

        [TestCase(RoomId.Title, true)]
        [TestCase(RoomId.GameScreen, true)]
        [TestCase(RoomId.SideScroll, true)]
        [TestCase(RoomId.Shaft, true)]
        [TestCase(RoomId.Startup, false)]
        [TestCase(RoomId.Intro, false)]
        [TestCase(RoomId.Map, false)]
        [TestCase(RoomId.Story, false)]
        [TestCase(RoomId.Upgrade, false)]
        public void F1TogglesFullscreenWhereTheControllerHasAnF1Event(RoomId room, bool toggles)
        {
            Do(new GoToRoom(P1, room));
            var r = Do(new PressFullscreen(P1));
            Assert.AreEqual(toggles, r.Accepted);
            Assert.AreEqual(toggles ? 1 : 0, r.Events.OfType<FullscreenToggled>().Count());
            Assert.AreEqual(room, flow.Current, "F1 never changes the room");
        }

        [Test]
        public void TheWholeMenuPathRunsTitleToStoryToMapToGame()
        {
            Do(new GoToRoom(P1, RoomId.Startup));
            Do(new GoToRoom(P1, RoomId.Title));             // obj_startup: any key
            Do(new FadeToRoom(P1, RoomId.Story));            // obj_btn_quickstart
            StepUntilEvent(out var r1);
            Assert.AreEqual(RoomId.Story, flow.Current);
            Do(new FadeToRoom(P1, RoomId.Map));              // obj_story / obj_scroll_world
            StepUntilEvent(out _);
            Assert.AreEqual(RoomId.Map, flow.Current);
            Do(new FadeToRoom(P1, RoomId.GameScreen));       // the map's launch
            StepUntilEvent(out _);
            Assert.AreEqual(RoomId.GameScreen, flow.Current);
            Assert.AreEqual(120, flow.StepsPerSecond);
        }
    }
}
