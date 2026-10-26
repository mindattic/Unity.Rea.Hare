using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using StringAssert = NUnit.Framework.Legacy.StringAssert;
using Unity.Hare.Arena;
using Unity.Hare.Content;
using Unity.Hare.Flow;
using Unity.Hare.Story;

namespace Unity.Hare.Tests.Core
{
    /// <summary>
    /// The story and cutscene system (U.HARE-T28): room_story's obj_story, obj_typewriter_story and obj_scroll_world,
    /// obj_conversation with obj_textdone and next_line, the plain obj_typewriter, load_scenario, and the text as data.
    /// </summary>
    public class StoryTests
    {
        static readonly PlayerId P1 = new PlayerId(1);

        static ContentRegistry Content => ContentRegistry.Default;

        static StoryScreen Story()
        {
            var content = Content;
            var screen = new StoryScreen(content.Story, content.Dialogue.Script(content.Story.Script), 1920, 1080);
            Assert.IsTrue(screen.Execute(new CreateStory(P1)).Accepted);
            return screen;
        }

        static List<StoryEvent> Step(StoryScreen screen, int steps)
        {
            var all = new List<StoryEvent>();
            for (int i = 0; i < steps; i++) all.AddRange(screen.Execute(new StepStory(P1)).Events);
            return all;
        }

        // ---- The text as data ----

        [Test]
        public void TheStoryTextIsObjStorysTypewriterTextInOrder()
        {
            string text = Content.Dialogue.Script("story");
            Assert.AreEqual(1346, text.Length);
            StringAssert.StartsWith("^^Introduction##^          It was the dawn of what many believed would be a golden age.#^", text);
            StringAssert.Contains("High Altitude Reconnaissance & Engagement##^", text, "&amp; in the .gmx is a plain &");
            StringAssert.Contains("across the the depths of space for ten millenia.#^^^", text, "the original's words, typos and all");
            StringAssert.EndsWith("#^^^Good luck ^...you'll need it!", text);
            Assert.AreEqual(30, text.Count(c => c == '^'));
            Assert.AreEqual(26, Content.Dialogue.ScriptLines("story").Count, "one line per typewriter.text += ...");
        }

        [Test]
        public void TheMapConversationIsAdmiralZeesThreeLines()
        {
            Assert.AreEqual("We need your help, Hutch!#^The planet is in peril!#^Alien invaders have broken through the blockade!",
                Content.Dialogue.Script(Content.Story.Conversation.MapScript));
            Assert.AreEqual("Admiral Zee", Content.Dialogue.Speaker("conversation.map"));
            Assert.AreEqual("", Content.Dialogue.Script("nothing"));
        }

        [Test]
        public void DialogueRejectsADuplicateIdAndAMissingText()
        {
            var ex = Assert.Throws<ContentValidationException>(() => ContentRegistry.FromJson(
                "{\"dialogue\": {\"Lines\": [{\"Id\": \"a\", \"Text\": \"x\"}, {\"Id\": \"a\", \"Text\": \"\"}]}}"));
            Assert.IsTrue(ex.Errors.Any(e => e.Contains("used by more than one line")));
            Assert.IsTrue(ex.Errors.Any(e => e.Contains("dialogue.Lines[1].Text")));
        }

        [Test]
        public void TheStorySectionValidates()
        {
            var ex = Assert.Throws<ContentValidationException>(() => ContentRegistry.FromJson(
                "{\"story\": {\"ScrollDelaySteps\": 0, \"Typewriter\": {\"TypeSpeedSteps\": 0}, \"Scenarios\": [{\"Scenario\": 1}, {\"Scenario\": 1}]}}"));
            Assert.IsTrue(ex.Errors.Any(e => e.StartsWith("story.ScrollDelaySteps")));
            Assert.IsTrue(ex.Errors.Any(e => e.StartsWith("story.Typewriter.TypeSpeedSteps")));
            Assert.IsTrue(ex.Errors.Any(e => e.Contains("listed more than once")));
        }

        // ---- obj_typewriter_story ----

        [Test]
        public void TheStoryComesUpWithTheEarthBelowTheRoomAndNothingTyped()
        {
            var screen = Story();
            Assert.AreEqual((960d, 2142d, 0d, 0), (screen.EarthX, screen.EarthY, screen.EarthAngle, screen.BackgroundY),
                "obj_scroll_world: x = room_width/2; y = room_height + sprite_get_height(spr_earthball)");
            Assert.AreEqual("", screen.Typewriter.Output);
            Assert.AreEqual(1, screen.Typewriter.Alarm, "alarm[0] = type_speed");
            Assert.IsFalse(screen.Execute(new CreateStory(P1)).Accepted, "once only");
        }

        [Test]
        public void ACaretIsNeverDrawnAndHoldsTheNextCharacterBackASecond()
        {
            var screen = Story();
            Assert.IsEmpty(Step(screen, 1).OfType<StoryTypeSounded>(), "step 1 types the first ^: no sound");
            Assert.AreEqual((1, "", "^"), (screen.Typewriter.Position, screen.Typewriter.Output, screen.Typewriter.NextChar));
            Assert.AreEqual(60, screen.Typewriter.Alarm, "alarm[0] = pause_delay (room_speed)");
            Step(screen, 60);
            Assert.AreEqual(2, screen.Typewriter.Position, "the second ^ at step 61");
            Assert.IsEmpty(Step(screen, 59).OfType<StoryTypeSounded>());
            Assert.AreEqual(2, screen.Typewriter.Position);
            var typed = Step(screen, 1).OfType<StoryTypeSounded>().ToList();
            Assert.AreEqual(3, screen.Typewriter.Position, "the I at step 121");
            Assert.AreEqual("I", screen.Typewriter.Output);
            Assert.AreEqual(1, typed.Count);
            Assert.AreEqual("sfx_type2", typed[0].Sound);
        }

        [Test]
        public void SpacesNewLinesStopsAndCommasTypeWithoutASound()
        {
            var t = new Typewriter(TypewriterDefinition.Story(), "a b#c.d,e");
            t.ArmTyping();
            var sounds = new List<string>();
            for (int i = 0; i < 9; i++) sounds.Add(t.Step().Sound);
            CollectionAssert.AreEqual(new[] { "sfx_type2", null, "sfx_type2", null, "sfx_type2", null, "sfx_type2", null, "sfx_type2" }, sounds);
            Assert.IsTrue(t.PrintComplete);
            Assert.AreEqual("a b\nc.d,e", t.DrawText, "GameMaker's # is a new line");
        }

        [Test]
        public void TheWholeIntroductionIsOutOnStep3116AndTheTypingStops()
        {
            var screen = Story();
            Step(screen, 3115);
            Assert.IsFalse(screen.Typewriter.PrintComplete);
            Step(screen, 1);
            Assert.IsTrue(screen.Typewriter.PrintComplete, "1316 characters a step each and 30 ^ a second each, from step 1");
            Assert.AreEqual(Content.Dialogue.Script("story").Replace("^", ""), screen.Typewriter.Output);
            Assert.AreEqual(1, screen.Typewriter.Alarm, "the last character's firing sets alarm[0] before print_complete");
            Step(screen, 1);
            Assert.AreEqual(-1, screen.Typewriter.Alarm, "that firing finds print_complete and does not set it again");
            Assert.IsEmpty(Step(screen, 200).OfType<StoryTypeSounded>());
        }

        // ---- obj_scroll_world ----

        [Test]
        public void TheEarthAndSkyRiseAPixelEveryThreeStepsAndTheEarthTurns()
        {
            var screen = Story();
            Step(screen, 2);
            Assert.AreEqual(2142d, screen.EarthY);
            Step(screen, 1);
            Assert.AreEqual((2141d, -1), (screen.EarthY, screen.BackgroundY), "y -= 1; background_y[0] -= 1");
            Assert.AreEqual(360d - 0.03d, screen.EarthAngle, 1e-9, "image_angle -= auto_speed, normalize_angle");
            Assert.AreEqual(359.99d, StoryScreen.NormalizeAngle(-0.01d), 1e-9);
            Assert.AreEqual(10d, StoryScreen.NormalizeAngle(370d));
        }

        [Test]
        public void TheEarthReachingTheBottomStartsTheCampaignAndFadesToTheMapOnce()
        {
            var screen = Story();
            var before = Step(screen, 3188);
            Assert.IsEmpty(before.OfType<StoryFadeRequested>());
            Assert.AreEqual(1080d, screen.EarthY, "1062 scrolls, the last on step 3186");
            var events = Step(screen, 1);
            Assert.IsTrue(screen.Finished);
            Assert.AreEqual(1, events.OfType<StoryCampaignChosen>().Count(), "gamemode = gm_campaign; current_scenario = -1; current_level = 1");
            Assert.AreEqual(RoomId.Map, events.OfType<StoryFadeRequested>().Single().Room, "next_room = room_map; obj_fade_story.mode = \"fadein\"");
            Assert.IsEmpty(Step(screen, 30).OfType<StoryFadeRequested>(), "finished: once only");
            Assert.AreEqual(-1062, screen.BackgroundY, "the alarm keeps running, but the sky stops with the earth");
        }

        // ---- obj_story's keys ----

        [Test]
        public void AnyKeyButEscStartsTheCampaignAndFadesToTheMapEveryPress()
        {
            var screen = Story();
            var first = screen.Execute(new PressStoryKey(P1));
            Assert.IsTrue(first.Accepted);
            Assert.AreEqual(1, first.Events.OfType<StoryCampaignChosen>().Count());
            Assert.AreEqual(RoomId.Map, first.Events.OfType<StoryFadeRequested>().Single().Room);
            Assert.IsTrue(screen.Execute(new PressStoryKey(P1)).Accepted, "a second press sets the fade going again");
            Assert.IsFalse(screen.Execute(new PressStoryKey(P1, escapeHeld: true)).Accepted, "if (!keyboard_check(vk_escape))");
            Assert.IsFalse(new StoryScreen(Content.Story, "", 1920, 1080).Execute(new PressStoryKey(P1)).Accepted, "before Create");
        }

        [Test]
        public void TheStoryRoomHasAFadeAndEscGoesToTheTitle()
        {
            var flow = new RoomFlow(Content);
            flow.Execute(new GoToRoom(P1, RoomId.Story));
            Assert.IsNotNull(flow.Fade, "room_story places obj_fade_story");
            Assert.IsTrue(flow.Execute(new FadeToRoom(P1, RoomId.Map)).Accepted);
            Assert.AreEqual(RoomId.Map, flow.NextRoom);
            Assert.IsTrue(flow.Execute(new PressEscape(P1)).Accepted, "obj_story's Esc: room_goto(room_title)");
            Assert.AreEqual(RoomId.Title, flow.Current);
        }

        // ---- obj_conversation ----

        static ConversationBox Conversation(string speaker = "Admiral Zee", string text = null)
        {
            var c = Content;
            return new ConversationBox(c.Story.Conversation, speaker, text ?? c.Dialogue.Script("conversation.map"), 1920, 1080);
        }

        static List<string> Steps(ConversationBox box, int steps)
        {
            var sounds = new List<string>();
            for (int i = 0; i < steps; i++) sounds.AddRange(box.Step());
            return sounds;
        }

        [Test]
        public void TheConversationWaitsOffBelowTheView()
        {
            var box = Conversation();
            Assert.AreEqual((ConversationMode.Off, 0d, 1080d, 1080d, 824d), (box.Mode, box.X, box.Y, box.StartY, box.EndY));
            Assert.IsEmpty(Steps(box, 100));
            Assert.AreEqual("", box.Typewriter.Output, "Create leaves the alarm unset: nothing types until it is idle");
            Assert.AreEqual((30d, 1110d, 127.5d, 1305d, 260d, 1104d), (box.FrameX, box.FrameY, box.NameX, box.NameY, box.TextX, box.TextY));
        }

        [Test]
        public void ItSlidesInWithAFlickEasesToTheBottomAndTypesItsLine()
        {
            var box = Conversation();
            box.SlideIn();
            var first = box.Step();
            CollectionAssert.AreEqual(new[] { "sfx_flick" }, first);
            Assert.AreEqual(1080d - (1d + 256d * 0.1d), box.Y, 1e-9, "y -= 1 + ((y - endY) * 0.1)");
            int steps = 1;
            while (box.Mode == ConversationMode.SlideIn) { box.Step(); steps++; }
            Assert.AreEqual(824d, box.Y);
            Assert.Less(steps, 60);
            Assert.AreEqual(-1, box.Typewriter.Alarm, "idle's first pass is the next step");
            box.Step();
            Assert.AreEqual(2, box.Typewriter.Alarm, "alarm[0] = type_speed");
            box.Step();
            var w = box.Step();
            CollectionAssert.AreEqual(new[] { "sfx_type" }, w, "the W, two steps on");
            Assert.AreEqual("W", box.Typewriter.Output);
        }

        [Test]
        public void NextLineWaitsForTheLineThenSlidesOutWithAFlickAndAWoof()
        {
            var box = Conversation();
            box.SlideIn();
            while (box.Mode != ConversationMode.Idle) box.Step();
            box.Step();
            Steps(box, 10);
            CollectionAssert.IsEmpty(box.NextLine(), "if(print_complete && mode == \"idle\")");
            Assert.AreEqual(ConversationMode.Idle, box.Mode);
            while (!box.PrintComplete) box.Step();
            Assert.AreEqual("We need your help, Hutch!\nThe planet is in peril!\nAlien invaders have broken through the blockade!", box.Typewriter.DrawText);
            Steps(box, 25);
            Assert.AreEqual(ConversationMode.Idle, box.Mode, "it waits for a key or a click");

            CollectionAssert.AreEqual(new[] { "sfx_flick", "sfx_woof" }, box.NextLine(), "next_line: sfx_flick, then Admiral Zee's sfx_woof");
            Assert.AreEqual(ConversationMode.SlideOut, box.Mode);
            CollectionAssert.AreEqual(new[] { "sfx_flick" }, box.Step(), "the slide out's first pass");
            Assert.AreEqual(824d + 4d + 256d * 0.1d, box.Y, 1e-9, "y += 4 + ((startY - y) * 0.1)");
            while (box.Mode == ConversationMode.SlideOut) box.Step();
            Assert.AreEqual((ConversationMode.Off, 1080d, "", 0, false), (box.Mode, box.Y, box.Typewriter.Output, box.Typewriter.Position, box.PrintComplete));
        }

        [Test]
        public void SomeoneElsesLineEndsWithASecondFlick()
        {
            var box = Conversation("Hutch", "Hi");
            box.SlideIn();
            while (!box.PrintComplete) box.Step();
            CollectionAssert.AreEqual(new[] { "sfx_flick", "sfx_flick" }, box.NextLine());
        }

        [Test]
        public void ItCanBeSentBackInAndTypesTheLineAgainFromTheStart()
        {
            var box = Conversation();
            box.SlideIn();
            Steps(box, 200);
            box.SlideOut();
            Steps(box, 100);
            Assert.AreEqual(ConversationMode.Off, box.Mode);
            box.SlideIn();
            Steps(box, 120);
            Assert.AreEqual(ConversationMode.Idle, box.Mode);
            StringAssert.StartsWith("We need", box.Typewriter.Output);
        }

        // ---- obj_typewriter ----

        [Test]
        public void ThePlainTypewriterTypesEverythingSilentlyAndFadesWhenTold()
        {
            var t = new Typewriter(TypewriterDefinition.Plain(), "a^b");
            t.ArmTyping();
            t.Step();
            Assert.AreEqual("", t.Output, "type_speed = 2");
            var s = t.Step();
            Assert.AreEqual(("a", (string)null), (t.Output, s.Sound));
            t.Step(); t.Step();
            Assert.AreEqual("a^", t.Output, "no pauses: the ^ is drawn");
            t.Step(); t.Step();
            Assert.AreEqual("a^b", t.Output);
            Assert.IsFalse(t.PrintComplete);
            Assert.AreEqual(2, t.Alarm, "its alarm runs on");
            t.FadeOut();
            int firings = 0;
            while (!t.Destroyed) { if (t.TickAlarm()) { t.Fire(); firings++; } }
            Assert.AreEqual(100, firings, "alpha -= 0.01 a firing until it is not above 0");
        }

        // ---- load_scenario ----

        [Test]
        public void LoadScenarioPlaysLevels1To5InScenario1And6To10InTheOthers()
        {
            var story = Content.Story;
            Assert.AreEqual("level_01", story.LoadScenario(1, 1));
            Assert.AreEqual("level_05", story.LoadScenario(1, 5));
            Assert.AreEqual("level_06", story.LoadScenario(2, 1));
            Assert.AreEqual("level_08", story.LoadScenario(3, 3));
            Assert.AreEqual("level_10", story.LoadScenario(4, 5));
            Assert.IsNull(story.LoadScenario(1, 6), "no if matches: no level script runs");
            Assert.AreEqual("level_default", story.LoadScenario(-1, 1), "default: level_default()");
            Assert.AreEqual("level_default", story.LoadScenario(5, 3));
        }

        [Test]
        public void EveryScenarioLevelIsAnArenaLevel()
        {
            var c = Content;
            Assert.AreEqual(8, LevelSetup.LoadScenario(c.Arena, c.Story, 3, 3).Level, "scenario 3's third level is level_08, the boss");
            Assert.AreEqual("level_default", LevelSetup.LoadScenario(c.Arena, c.Story, -1, 1).Script);
            Assert.IsNull(LevelSetup.LoadScenario(c.Arena, c.Story, 2, 9));
            foreach (var s in c.Story.Scenarios)
                for (int level = 1; level <= s.Levels.Length; level++)
                    Assert.AreEqual(s.Levels[level - 1], LevelSetup.LoadScenario(c.Arena, c.Story, s.Scenario, level).Script);
        }
    }
}
