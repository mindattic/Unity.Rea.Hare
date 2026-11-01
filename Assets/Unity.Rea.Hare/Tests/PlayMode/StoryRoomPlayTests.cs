using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Unity.Rea.Hare.Content;
using Unity.Rea.Hare.Flow;
using Unity.Rea.Hare.Input;
using Unity.Rea.Hare.Rendering;
using Unity.Rea.Hare.Session;
using Unity.Rea.Hare.Story;

namespace Unity.Rea.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T28, played: room_story comes up with obj_story over bg_nightsky, the typewriter types the introduction
    /// while the earth rises, a key starts the campaign and fades to room_map, Esc goes to room_title; and obj_conversation
    /// slides in on a GUI layer, types Admiral Zee's line, and a key once it is out slides it away with sfx_flick and
    /// sfx_woof. With a graphics device the story and the conversation are rendered to Screenshots/story.png and
    /// Screenshots/conversation.png.
    /// </summary>
    public class StoryRoomPlayTests : InputTestFixture
    {
        Keyboard keyboard;
        Func<bool> getVisible;
        Action<bool> setVisible;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Mouse>();
            getVisible = SystemCursor.GetVisible;
            setVisible = SystemCursor.SetVisible;
            bool cursor = true;
            SystemCursor.GetVisible = () => cursor;
            SystemCursor.SetVisible = value => cursor = value;
        }

        [UnityTearDown]
        public IEnumerator LeaveNoRoomBehind()
        {
            if (RoomFlowDirector.Active != null) UnityEngine.Object.Destroy(RoomFlowDirector.Active.gameObject);
            yield return null;
            SystemCursor.Restore();
            SystemCursor.GetVisible = getVisible;
            SystemCursor.SetVisible = setVisible;
            yield return PlayModeRooms.TearDown();
        }

        static IEnumerator WaitFor(Func<bool> condition, string what, float seconds = 5f)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > until) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        IEnumerator Tap(ButtonControl button)
        {
            Press(button, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(button, queueEventOnly: true);
            yield return null;
        }

        static StoryRoom Story() => UnityEngine.Object.FindAnyObjectByType<StoryRoom>();

        static IEnumerator EnterStory()
        {
            var director = RoomFlowDirector.Create(enterFirstRoom: false);
            director.GetFullscreen = () => false;
            director.SetFullscreen = _ => { };
            director.Execute(new GoToRoom(RoomFlowDirector.Seat, RoomId.Story));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_story" && Story() != null && Story().State.Created,
                "room_story and its Create");
        }

        static string Drawn(VisualElement outline)
        {
            var label = outline.Q<Label>();
            return label != null ? label.text : null;
        }

        [UnityTest]
        public IEnumerator TheIntroductionTypesOutAsTheEarthRises()
        {
            yield return EnterStory();
            var story = Story();
            Assert.That(story.name, Is.EqualTo(StoryRoom.ObjectName));
            Assert.That(story.Background.sprite.texture.name, Is.EqualTo("bg_nightsky"));
            Assert.That(story.Earth.sprite.name, Does.StartWith("spr_earthball"));
            Assert.That(story.TypewriterText.parent, Is.SameAs(story.Stage));

            yield return WaitFor(() => story.State.Typewriter.Output.Contains("golden age"), "the first sentence", 8f);
            Assert.That(Drawn(story.TypewriterText), Does.StartWith("Introduction\n\n          It was the dawn"), "the ^ never drawn, # a new line");
            Assert.That(story.State.EarthY, Is.LessThan(2142d), "obj_scroll_world rises a pixel every 3 steps");
            Assert.That(story.State.BackgroundY, Is.LessThan(0), "background_y[0] -= 1 with it");
            Assert.That(story.BackgroundTileStart().y, Is.EqualTo(StartupLayerWrap(story.State.BackgroundY, 1080) - 1080));
            Assert.That(RoomFlowDirector.Active.Flow.Current, Is.EqualTo(RoomId.Story));

            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                yield return TitleRoomPlayTests.Render(story, 1920, 1080, "story.png", (world, ui) =>
                {
                    Assert.That(TitleRoomPlayTests.Count(ui, 1920, 1080, 30, 900, 60, 200, p => p.a > 200 && p.r > 200 && p.g > 200 && p.b > 200), Is.GreaterThan(500),
                        "obj_typewriter_story's white text at 30,60");
                });
        }

        static int StartupLayerWrap(int position, int size) => ((position % size) + size) % size;

        [UnityTest]
        public IEnumerator AKeyStartsTheCampaignAndFadesToTheMap()
        {
            yield return EnterStory();
            var director = RoomFlowDirector.Active;
            yield return new WaitForSecondsRealtime(0.6f); // obj_fade_story's fade from black
            yield return Tap(keyboard.spaceKey);
            Assert.That(director.Session.Mode, Is.EqualTo(GameMode.Campaign), "gamemode = gm_campaign");
            Assert.That(director.Session.Scenario, Is.EqualTo(-1));
            Assert.That(director.Session.Level, Is.EqualTo(1));
            Assert.That(director.Flow.Fade.Mode, Is.EqualTo(FadeMode.FadeIn), "obj_fade_story.mode = \"fadein\"");
            Assert.That(director.Flow.NextRoom, Is.EqualTo(RoomId.Map));
            yield return WaitFor(() => director.Flow.Current == RoomId.Map, "room_goto(next_room) once black");
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_map", "room_map to load");
        }

        [UnityTest]
        public IEnumerator EscGoesToTheTitle()
        {
            yield return EnterStory();
            yield return Tap(keyboard.escapeKey);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_title", "obj_story's Esc: room_goto(room_title)");
            Assert.That(RoomFlowDirector.Active.Session.Mode, Is.Not.EqualTo(GameMode.Campaign), "Esc is not the any-key's campaign");
        }

        [UnityTest]
        public IEnumerator TheConversationSlidesInTypesAndAKeySendsItAway()
        {
            yield return EnterStory();
            var story = Story();
            story.enabled = false; // the room's own any-key would fade to room_map; this test is about the box alone
            var content = GameContent.Current;
            string script = content.Story.Conversation.MapScript;
            var box = new ConversationBox(content.Story.Conversation, content.Dialogue.Speaker(script), content.Dialogue.Script(script), 1920, 1080, content.Story.TextDone);
            var view = ConversationView.Show(story.Stage, box, 60);
            var sounds = new List<string>();
            view.SoundPlayed += sounds.Add;
            Assert.That(Drawn(view.SpeakerElement), Is.EqualTo("Admiral Zee"));

            box.SlideIn(); // obj_destination's current_conversation.mode = "slidein"
            yield return WaitFor(() => box.Mode == ConversationMode.Idle, "the slide in");
            Assert.That(sounds[0], Is.EqualTo("sfx_flick"));
            Assert.That(view.style.top.value.value, Is.EqualTo(824f).Within(0.5f));
            yield return Tap(keyboard.spaceKey);
            Assert.That(box.Mode, Is.EqualTo(ConversationMode.Idle), "next_line waits for print_complete");
            yield return WaitFor(() => box.PrintComplete, "Admiral Zee's line", 8f);
            Assert.That(Drawn(view.TextElement), Is.EqualTo("We need your help, Hutch!\nThe planet is in peril!\nAlien invaders have broken through the blockade!"));
            Assert.That(sounds, Does.Contain("sfx_type"));
            yield return null;
            Assert.That(view.TextDone.style.display.value, Is.EqualTo(DisplayStyle.Flex), "obj_textdone marks the finished line (U.HARE-T87)");

            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                yield return TitleRoomPlayTests.Render(story, 1920, 1080, "conversation.png", (world, ui) =>
                {
                    Assert.That(TitleRoomPlayTests.Count(ui, 1920, 1080, 0, 1920, 830, 1075, p => p.a > 100), Is.GreaterThan(200000), "spr_conversation_box along the bottom");
                    Assert.That(TitleRoomPlayTests.Count(ui, 1920, 1080, 30, 225, 854, 1049, p => p.a > 200), Is.GreaterThan(20000), "the portrait in its frame");
                });

            sounds.Clear();
            yield return Tap(keyboard.enterKey);
            Assert.That(sounds.GetRange(0, 2), Is.EqualTo(new[] { "sfx_flick", "sfx_woof" }), "next_line");
            yield return WaitFor(() => box.Mode == ConversationMode.Off, "the slide out");
            Assert.That(box.Y, Is.EqualTo(1080d));
            Assert.That(view.TextDone.style.display.value, Is.EqualTo(DisplayStyle.None), "next_line destroyed obj_textdone");
            Assert.That(Drawn(view.TextElement), Is.EqualTo(""), "the text reset");
        }
    }
}
