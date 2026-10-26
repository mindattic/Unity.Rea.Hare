using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Hare.Arena;
using Unity.Hare.Flow;
using Unity.Hare.Input;
using Unity.Hare.Player;
using Unity.Hare.Rendering;

namespace Unity.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T61, played: the map and upgrade rooms come up with obj_mouse's spr_pointer on the mouse, the title's
    /// pointer is made hidden (obj_logo shows it), the play rooms' reticle sits on the ship's own aim point and turns
    /// green in flight, blue within the jump arc and red outside it, and the system cursor is hidden while the rooms run
    /// and given back as it was when they go. With a graphics device the reticle and the pointer are also rendered to
    /// Screenshots/reticle.png and Screenshots/pointer.png.
    /// </summary>
    public class MousePlayTests : InputTestFixture
    {
        Mouse mouse;
        bool cursorVisible;
        Func<bool> getVisible;
        Action<bool> setVisible;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            // The real cursor is left alone: the seams record what the rooms do to it.
            cursorVisible = true;
            getVisible = SystemCursor.GetVisible;
            setVisible = SystemCursor.SetVisible;
            SystemCursor.GetVisible = () => cursorVisible;
            SystemCursor.SetVisible = value => cursorVisible = value;
        }

        [UnityTearDown]
        public IEnumerator LeaveNoRoomBehind()
        {
            if (RoomFlowDirector.Active != null) UnityEngine.Object.Destroy(RoomFlowDirector.Active.gameObject);
            yield return null;
            SystemCursor.Restore();
            SystemCursor.GetVisible = getVisible;
            SystemCursor.SetVisible = setVisible;
            var empty = SceneManager.GetSceneByName("MousePlayTests.Empty");
            if (!empty.IsValid()) empty = SceneManager.CreateScene("MousePlayTests.Empty");
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != empty) yield return SceneManager.UnloadSceneAsync(scene);
            }
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

        static ArenaRoom Arena() => UnityEngine.Object.FindAnyObjectByType<ArenaRoom>();

        static IEnumerator Enter(RoomId room)
        {
            var director = RoomFlowDirector.Active != null ? RoomFlowDirector.Active : RoomFlowDirector.Create(enterFirstRoom: false);
            director.GetFullscreen = () => false;
            director.SetFullscreen = _ => { };
            director.Execute(new GoToRoom(RoomFlowDirector.Seat, room));
            string name = RoomList.GmlName(room);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == name && MouseView.Active != null
                && MouseView.Active.gameObject.scene.name == name, name + " and its obj_mouse");
            if (ProjectRoom(room)) yield return WaitFor(() => Arena() != null && Arena().State.Created && Arena().Player != null, "obj_game's Create");
        }

        static bool ProjectRoom(RoomId room) => room == RoomId.GameScreen || room == RoomId.SideScroll || room == RoomId.Shaft;

        static void Put(ArenaRoom arena, float x, float y)
        {
            var world = arena.ToWorld(x, y);
            var ship = arena.Player;
            ship.transform.position = new Vector3(world.x, world.y, ship.transform.position.z);
            ship.GetComponent<Rigidbody2D>().position = world;
        }

        [UnityTest]
        public IEnumerator TheMapAndUpgradeRoomsDrawThePointerOnTheMouse()
        {
            foreach (var room in new[] { RoomId.Map, RoomId.Upgrade })
            {
                yield return Enter(room);
                var view = MouseView.Active;
                Assert.That(view.name, Is.EqualTo(MouseView.ObjectName));
                Set(mouse.position, new Vector2(300f, 200f));
                yield return null;
                yield return null;
                Assert.That(view.State.Sprite, Is.EqualTo("spr_pointer"), room.ToString());
                Assert.That(view.Drawn, Is.True, room.ToString());
                Assert.That(view.Renderer.sprite, Is.Not.Null);
                Vector2 expected = Camera.main.ScreenToWorldPoint(new Vector3(300f, 200f, -Camera.main.transform.position.z));
                Assert.That(Vector2.Distance(view.transform.position, expected), Is.LessThan(0.01f), "x = mouse_x; y = mouse_y");
                Assert.That(view.Renderer.sortingOrder, Is.EqualTo(5000), "depth -5000");
                Assert.That(cursorVisible, Is.False, "option_showcursor False");
            }
        }

        [UnityTest]
        public IEnumerator TheTitlesPointerIsMadeHiddenUntilObjLogoShowsIt()
        {
            yield return Enter(RoomId.Title);
            var view = MouseView.Active;
            yield return null;
            Assert.That(view.State.Sprite, Is.EqualTo("spr_pointer"));
            Assert.That(view.Drawn, Is.False, "obj_title: mouse.visible = false");
            view.State.SetVisible(true); // obj_logo's Create
            yield return null;
            Assert.That(view.Drawn, Is.True);
        }

        [UnityTest]
        public IEnumerator TheReticleSitsOnTheShipsAimPointAndTakesItsColourFromTheShip()
        {
            yield return Enter(RoomId.GameScreen);
            var arena = Arena();
            var view = MouseView.Active;
            var camera = Camera.main;

            // reset_player flies the ship in: the reticle is green.
            Set(mouse.position, (Vector2)camera.WorldToScreenPoint(arena.Player.transform.position) + new Vector2(0f, -200f));
            yield return null;
            yield return null;
            view.Draw(); // obj_mouse's step on this frame's aim point (a test resumes after Update, before LateUpdate)
            Assert.That(arena.Player.State, Is.EqualTo(PlayerFlightState.Moving));
            Assert.That(view.State.Sprite, Is.EqualTo("spr_reticle_green"));
            Assert.That(arena.Player.AimPoint.HasValue, Is.True);
            Assert.That(Vector2.Distance(view.transform.position, arena.Player.AimPoint.Value), Is.LessThan(0.01f), "on mouse_x, mouse_y");

            // Perched facing 270 with the mouse below it: within jump_arc, blue.
            Put(arena, 1920f, 1080f);
            arena.Player.Land();
            yield return null;
            Set(mouse.position, (Vector2)camera.WorldToScreenPoint(arena.ToWorld(1920f, 1400f)));
            yield return null;
            yield return null;
            Assert.That(arena.Player.CanLeap, Is.True);
            Assert.That(view.State.Sprite, Is.EqualTo("spr_reticle_blue"));
            Assert.That(view.Renderer.sprite.name, Does.StartWith("spr_reticle_blue"));
            view.Draw(); // on this frame's aim point: view 0 may still be easing onto the moved ship
            Assert.That(Vector2.Distance(view.transform.position, arena.Player.AimPoint.Value), Is.LessThan(0.01f));

            // Behind the ship: outside jump_arc, red.
            Set(mouse.position, (Vector2)camera.WorldToScreenPoint(arena.ToWorld(1920f, 800f)));
            yield return null;
            yield return null;
            Assert.That(arena.Player.CanLeap, Is.False);
            Assert.That(view.State.Sprite, Is.EqualTo("spr_reticle_red"));
            Assert.That(view.Drawn, Is.True);
        }

        [UnityTest]
        public IEnumerator TheSystemCursorIsHiddenInTheRoomsAndGivenBackAfter()
        {
            yield return Enter(RoomId.Map);
            Assert.That(cursorVisible, Is.False);
            Assert.That(SystemCursor.Hidden, Is.True);
            UnityEngine.Object.Destroy(RoomFlowDirector.Active.gameObject);
            yield return null;
            Assert.That(cursorVisible, Is.True, "restored as it was");
            Assert.That(SystemCursor.Hidden, Is.False);
        }

        [UnityTest]
        public IEnumerator TheReticleAndPointerRender()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("needs a graphics device: run the PlayMode tests without -nographics (tools/unity.ps1 -Graphics)");

            yield return Enter(RoomId.GameScreen);
            var arena = Arena();
            Put(arena, 1920f, 1080f);
            arena.Player.Land();
            yield return new WaitForSecondsRealtime(0.3f);
            var camera = arena.ViewCamera;
            Set(mouse.position, (Vector2)Camera.main.WorldToScreenPoint(arena.ToWorld(1920f, 1300f)));
            yield return null;
            yield return null;
            Assert.That(MouseView.Active.State.Sprite, Is.EqualTo("spr_reticle_blue"));
            Assert.That(Render(camera, "reticle.png"), Is.GreaterThan(4.0));

            yield return Enter(RoomId.Map);
            Set(mouse.position, (Vector2)Camera.main.WorldToScreenPoint(Vector3.zero)); // the middle of the room
            yield return null;
            yield return null;
            Assert.That(MouseView.Active.Drawn, Is.True);
            Assert.That(Vector2.Distance(MouseView.Active.transform.position, Vector2.zero), Is.LessThan(0.01f));
            Assert.That(Render(Camera.main, "pointer.png"), Is.GreaterThan(0.0), "the pointer on the black map room");
        }

        /// <summary>Renders <paramref name="camera"/> at 1920x1080 to Screenshots/<paramref name="file"/> and returns its mean brightness.</summary>
        static double Render(Camera camera, string file)
        {
            const int W = 1920, H = 1080;
            var target = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var letterbox = camera.GetComponent<LetterboxCamera>();
            if (letterbox != null) letterbox.enabled = false;
            var rect = camera.rect;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.targetTexture = target;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(W, H, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;
            camera.rect = rect;
            if (letterbox != null) letterbox.enabled = true;
            var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Screenshots");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, file), texture.EncodeToPNG());
            double brightness = texture.GetPixels32().Average(p => (p.r + p.g + p.b) / 3.0);
            UnityEngine.Object.Destroy(texture);
            target.Release();
            return brightness;
        }
    }
}
