using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Rea.Hare.Flow;
using Unity.Rea.Hare.Rendering;

namespace Unity.Rea.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T50, played: the bootstrap scene starts the room flow, room scenes load by their GML names, every room
    /// with an obj_fade_* comes up from black and fades to black before next_room, and real Esc and F1 key presses
    /// do what the room's controller does.
    /// </summary>
    public class RoomFlowPlayTests : InputTestFixture
    {
        Keyboard keyboard;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [UnityTearDown]
        public IEnumerator LeaveNoRoomBehind()
        {
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

        static ScreenOverlayLayer Overlay() => Camera.main.GetComponentInChildren<ScreenOverlayLayer>();

        IEnumerator Tap(KeyControl key)
        {
            Press(key, queueEventOnly: true);
            yield return null; // the frame's input update sees the press, then the director's Update reads it
            yield return null;
            Release(key, queueEventOnly: true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheBootstrapRunsTheRoomsTheirFadesEscAndF1()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "startup", "the bootstrap to send the game to startup");
            var director = RoomFlowDirector.Active;
            Assert.That(director, Is.Not.Null, "the bootstrap's director survives the room load");
            Assert.That(director.Flow.Current, Is.EqualTo(RoomId.Startup));
            Assert.That(director.Flow.Fade, Is.Null, "startup has no obj_fade_*");

            bool fullscreen = false;
            director.GetFullscreen = () => fullscreen;
            director.SetFullscreen = value => fullscreen = value;

            // startup: no F1 or Esc event of its own; F1 is just one of obj_startup's any keys (U.HARE-T51), which go to
            // the title without the title's F1 answering the same press.
            yield return Tap(keyboard.f1Key);
            Assert.That(fullscreen, Is.False);

            // room_title comes up from black (obj_fade_title) and fades out to clear.
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_title", "room_title to load");
            Assert.That(director.Flow.Current, Is.EqualTo(RoomId.Title));
            yield return null;
            Assert.That(Overlay().Color.a, Is.GreaterThan(0.8f), "the new room starts black");
            float started = Time.realtimeSinceStartup;
            yield return WaitFor(() => director.Flow.Fade.Mode == FadeMode.Off, "the fade-out");
            float took = Time.realtimeSinceStartup - started;
            Assert.That(took, Is.InRange(0.3f, 2f), "35 steps at room_title's 60 steps a second, about 0.58 s");
            yield return null;
            Assert.That(Overlay().Color.a, Is.EqualTo(0f));

            // F1 in the title toggles fullscreen both ways; Esc does nothing there.
            yield return Tap(keyboard.f1Key);
            Assert.That(fullscreen, Is.True, "obj_title: F1");
            yield return Tap(keyboard.f1Key);
            Assert.That(fullscreen, Is.False);
            yield return Tap(keyboard.escapeKey);
            Assert.That(director.Flow.Current, Is.EqualTo(RoomId.Title), "obj_title has no Esc event");

            // A menu's next_room + fadein: black first, then room_map.
            Assert.That(director.Execute(new FadeToRoom(RoomFlowDirector.Seat, RoomId.Map, onlyWhenIdle: true)).Accepted, Is.True);
            float darkest = 0f;
            while (SceneManager.GetActiveScene().name == "room_title")
            {
                var overlay = Overlay();
                if (overlay != null) darkest = Mathf.Max(darkest, overlay.Color.a);
                yield return null;
            }
            Assert.That(darkest, Is.GreaterThan(0.9f), "the screen went black before the room changed");
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_map", "room_map to load");
            Assert.That(director.Flow.Current, Is.EqualTo(RoomId.Map));

            // room_map: F1 does nothing, Esc goes straight to the title.
            yield return Tap(keyboard.f1Key);
            Assert.That(fullscreen, Is.False, "obj_map has no F1 event");
            yield return Tap(keyboard.escapeKey);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_title", "Esc to return to the title");

            // room_gamescreen: F1 toggles, Esc returns to the title.
            director.Execute(new GoToRoom(RoomFlowDirector.Seat, RoomId.GameScreen));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_gamescreen", "room_gamescreen to load");
            yield return Tap(keyboard.f1Key);
            Assert.That(fullscreen, Is.True, "obj_game: F1");
            yield return Tap(keyboard.escapeKey);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_title", "Esc to leave the game for the title");
        }

        [UnityTest]
        public IEnumerator ARoomReloadedOutsideTheFlowStartsItsFadeOver()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "startup", "startup");
            var director = RoomFlowDirector.Active;
            director.Execute(new GoToRoom(RoomFlowDirector.Seat, RoomId.Story));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_story", "room_story");
            yield return WaitFor(() => director.Flow.Fade.Mode == FadeMode.Off, "the fade-out");

            // obj_player's death: SceneManager.LoadScene(active build index), as room_restart.
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            yield return null;
            yield return null;
            Assert.That(director.Flow.Current, Is.EqualTo(RoomId.Story));
            Assert.That(director.Flow.Fade.Mode, Is.EqualTo(FadeMode.FadeOut), "a fresh obj_fade_story");
            Assert.That(Overlay().Color.a, Is.GreaterThan(0.8f));
        }
    }
}
