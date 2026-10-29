using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Hare.Flow;
using Unity.Hare.Input;
using Unity.Hare.Popups;
using Unity.Hare.Rendering;
using Unity.Hare.Rooms;
using Unity.Hare.Session;

namespace Unity.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T62, played: room_intro (which nothing in the original goes to; the flow is sent there) comes up with
    /// obj_intro and obj_drop_intro over bg_nightsky and bg_gradient3; the rabbit falls in through the clouds, stops with
    /// the controls tip, moves with A and D, and any other key sends it out of the room into a quick game at level 1 in
    /// room_gamescreen; Esc goes to room_title. With a graphics device the intro with its tip is rendered to
    /// Screenshots/intro.png.
    /// </summary>
    public class IntroRoomPlayTests : InputTestFixture
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

        static IntroRoom Intro() => UnityEngine.Object.FindAnyObjectByType<IntroRoom>();

        static IEnumerator EnterIntro()
        {
            var director = RoomFlowDirector.Create(enterFirstRoom: false);
            director.GetFullscreen = () => false;
            director.SetFullscreen = _ => { };
            director.Execute(new GoToRoom(RoomFlowDirector.Seat, RoomId.Intro));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_intro" && Intro() != null && Intro().State.Created,
                "room_intro and its Create");
        }

        static IEnumerator Hanging(IntroRoom intro) =>
            WaitFor(() => intro.State.Scene.Drop.Phase == DropPhase.Hang, "the rabbit to stop a quarter of the way down");

        [UnityTest]
        public IEnumerator TheRabbitFallsInAndStopsWithTheControlsTip()
        {
            yield return EnterIntro();
            var intro = Intro();
            Assert.That(intro.name, Is.EqualTo(IntroRoom.ObjectName));
            Assert.That(intro.SceneView.Layers.Select(l => l.sprite.texture.name).ToArray(), Is.EqualTo(new[] { "bg_nightsky", "bg_gradient3" }));
            Assert.That(intro.SceneView.Drop.sprite.name, Does.StartWith("spr_drop"));
            Assert.That(MouseView.Active == null || MouseView.Active.gameObject.scene.name != "room_intro", "obj_intro makes no obj_mouse");
            Assert.That(intro.Tip, Is.Null);
            yield return WaitFor(() => intro.State.Scene.Clouds.Count > 0, "the clouds");
            yield return Hanging(intro);
            Assert.That(intro.Tip, Is.Not.Null, "tip = instance_create(0,0,obj_tip)");
            Assert.That(intro.Tip.Tip.Text, Does.StartWith("Controls#"));
            Assert.That(intro.Tip.parent, Is.SameAs(intro.Stage));

            yield return new WaitForSecondsRealtime(3f);
            Assert.That(intro.State.Scene.Drop.Phase, Is.EqualTo(DropPhase.Hang), "no Alarm 1: it waits for a key");
            Assert.That(RoomFlowDirector.Active.Flow.Current, Is.EqualTo(RoomId.Intro));

            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                yield return TitleRoomPlayTests.Render(intro, 1920, 1080, "intro.png", (world, ui) =>
                {
                    Assert.That(TitleRoomPlayTests.Count(ui, 1920, 1080, 1350, 1880, 805, 1045, p => p.a > 100), Is.GreaterThan(20000), "obj_tip slid in at the bottom right");
                    Assert.That(TitleRoomPlayTests.Count(world, 1920, 1080, 860, 1060, 290, 840, p => p.r > 200 && p.g > 200 && p.b > 200), Is.GreaterThan(15000), "the rabbit at 960,272");
                });
        }

        [UnityTest]
        public IEnumerator AAndDSteerIt()
        {
            yield return EnterIntro();
            var intro = Intro();
            yield return Hanging(intro);
            Press(keyboard.dKey);
            yield return WaitFor(() => intro.State.Scene.Drop.StartX > 980d, "D: startX += 1 a step while held");
            Release(keyboard.dKey);
            yield return null;
            Assert.That(intro.State.Scene.Drop.Phase, Is.EqualTo(DropPhase.Hang), "D is held, so its press sends nothing on");
            double x = intro.State.Scene.Drop.StartX;
            Press(keyboard.aKey);
            yield return WaitFor(() => intro.State.Scene.Drop.StartX < x - 5d, "A: startX -= 1 a step while held");
            Release(keyboard.aKey);
            yield return null;
            Assert.That(intro.State.Scene.Drop.StartX, Is.LessThan(x));
            Assert.That(intro.State.Scene.Drop.Phase, Is.EqualTo(DropPhase.Hang));
        }

        [UnityTest]
        public IEnumerator AKeySendsItIntoAQuickGameAtLevelOne()
        {
            yield return EnterIntro();
            var intro = Intro();
            var director = RoomFlowDirector.Active;
            yield return Hanging(intro);
            yield return Tap(keyboard.spaceKey);
            Assert.That(intro.State.Scene.Drop.Phase, Is.EqualTo(DropPhase.Exit), "phase = \"exit\"");
            Assert.That(intro.Tip.Tip.Gone, Is.True, "the same key dismisses obj_tip");
            yield return WaitFor(() => director.Flow.Current == RoomId.GameScreen, "room_goto(room_gamescreen) once it is below the room");
            Assert.That(director.Session.Mode, Is.EqualTo(GameMode.Quickstart));
            Assert.That(director.Session.Level, Is.EqualTo(1));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_gamescreen", "room_gamescreen to load");
        }

        [UnityTest]
        public IEnumerator EscGoesToTheTitle()
        {
            yield return EnterIntro();
            yield return Tap(keyboard.escapeKey);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_title", "obj_intro's Esc: room_goto(room_title)");
        }
    }
}
