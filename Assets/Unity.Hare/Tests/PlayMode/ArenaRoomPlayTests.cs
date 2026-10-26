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
using Unity.Hare.Player;
using Unity.Hare.Rendering;
using Unity.Hare.Structures;

namespace Unity.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T52, played: room_gamescreen comes up as level 1 sets it (bg_worldcurve stretched over the 3840x2160 room),
    /// view 0 follows the ship and stays in the room, a real M press shows and hides the minimap in its 300x200 port, the
    /// 1920x1080 HUD layer is up, flying past the 400 px buffer zone costs a life and drops the ship back in from above
    /// the room's middle, the big arrow points at a ship out of the room, the leap line runs from a perched ship to the
    /// mouse, and room_shaft (level 4) wraps the ship around its edges. With a graphics device the arena is also rendered
    /// to Screenshots/arena.png.
    /// </summary>
    public class ArenaRoomPlayTests : InputTestFixture
    {
        Keyboard keyboard;
        Mouse mouse;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator LeaveNoRoomBehind()
        {
            if (RoomFlowDirector.Active != null) UnityEngine.Object.Destroy(RoomFlowDirector.Active.gameObject);
            var empty = SceneManager.CreateScene("ArenaRoomPlayTests.Empty");
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
            var director = RoomFlowDirector.Create(enterFirstRoom: false);
            director.GetFullscreen = () => false;
            director.SetFullscreen = _ => { };
            director.Execute(new GoToRoom(RoomFlowDirector.Seat, room));
            string name = RoomList.GmlName(room);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == name && Arena() != null && Arena().State.Created && Arena().Player != null,
                name + " and obj_game's Create");
        }

        static void Put(ArenaRoom arena, float x, float y)
        {
            var world = arena.ToWorld(x, y);
            var ship = arena.Player;
            ship.transform.position = new Vector3(world.x, world.y, ship.transform.position.z);
            ship.GetComponent<Rigidbody2D>().position = world;
        }

        [UnityTest]
        public IEnumerator Level1_DrawsTheWorldCurve_AndView0FollowsTheShipInsideTheRoom()
        {
            yield return Enter(RoomId.GameScreen);
            var arena = Arena();
            Assert.That(arena.name, Is.EqualTo(ArenaRoom.ObjectName));
            Assert.That(arena.State.Width, Is.EqualTo(3840));
            Assert.That(arena.State.Height, Is.EqualTo(2160));
            Assert.That(arena.State.Setting.Script, Is.EqualTo("level_01"));

            var back = arena.Layers[0];
            Assert.That(back, Is.Not.Null);
            Assert.That(back.sprite.texture.name, Is.EqualTo("bg_worldcurve"));
            Assert.That(back.sortingLayerName, Is.EqualTo(HareSortingLayers.Backdrop));
            Assert.That(back.bounds.size.x, Is.EqualTo(38.4f).Within(0.01f), "stretched over room_width");
            Assert.That(back.bounds.size.y, Is.EqualTo(21.6f).Within(0.01f), "stretched over room_height");
            Assert.That(arena.Layers[1], Is.Null, "background_index[1] = noone");

            // The ship perched in the room's lower right: the view settles on it, inside the room.
            arena.Player.Land();
            Put(arena, 3000f, 1800f);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(arena.State.View.X, Is.EqualTo(3840f - 1920f), "kept inside the room");
            Assert.That(arena.State.View.Y, Is.EqualTo(2160f - 1080f));
            var camera = arena.ViewCamera;
            Assert.That(camera, Is.SameAs(Camera.main));
            Assert.That(camera.orthographicSize, Is.EqualTo(5.4f).Within(1e-4f), "1080 px tall");
            Vector2 expected = arena.ToWorld(3840f - 960f, 2160f - 540f);
            Assert.That(Vector2.Distance(camera.transform.position, expected), Is.LessThan(0.01f), "the camera is on view 0");

            Put(arena, 1920f, 1080f);
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.That(arena.State.View.X, Is.EqualTo(960f), "centred on the ship");
            Assert.That(arena.State.View.Y, Is.EqualTo(540f));
        }

        [UnityTest]
        public IEnumerator MShowsAndHidesTheMinimap_AndTheHudIs1920x1080()
        {
            yield return Enter(RoomId.GameScreen);
            var arena = Arena();
            var minimap = arena.MinimapCamera;
            Assert.That(minimap, Is.Not.Null);
            Assert.That(minimap.enabled, Is.False, "show_map starts false");
            Assert.That(minimap.CompareTag("MainCamera"), Is.False);
            Assert.That(minimap.orthographicSize, Is.EqualTo(10f).Within(1e-4f), "2000 px tall");
            var letterbox = LetterboxCamera.ComputeViewportRect(Screen.width, Screen.height, LetterboxCamera.ReferenceAspect);
            Assert.That(minimap.rect, Is.EqualTo(LetterboxCamera.ComputePortRect(letterbox, new Rect(1600f, 800f, 300f, 200f))));

            Press(keyboard.mKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(keyboard.mKey, queueEventOnly: true);
            yield return null;
            Assert.That(arena.State.ShowMap, Is.True);
            Assert.That(minimap.enabled, Is.True, "view_visible[1] = true");

            Press(keyboard.mKey, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(keyboard.mKey, queueEventOnly: true);
            yield return null;
            Assert.That(minimap.enabled, Is.False);

            yield return null;
            Assert.That(arena.Hud, Is.Not.Null);
            Assert.That(arena.Hud.layout.width, Is.EqualTo(1920f));
            Assert.That(arena.Hud.layout.height, Is.EqualTo(1080f));
        }

        [UnityTest]
        public IEnumerator PastTheBufferZone_ALifeGoes_AndTheShipDropsInAboveTheMiddle()
        {
            yield return Enter(RoomId.GameScreen);
            var arena = Arena();
            var health = arena.Player.GetComponent<PlayerHealth>();
            int lives = health.Lives;
            arena.Player.Land();

            Put(arena, 3840f + 300f, 1000f);
            yield return null;
            yield return null;
            Assert.That(health.Lives, Is.EqualTo(lives), "inside the 400 px buffer zone");

            int lost = 0;
            arena.EventPlayed += e => { if (e is PlayerOutOfBounds) lost++; };
            Put(arena, 3840f + 450f, 1000f);
            yield return WaitFor(() => lost > 0, "the buffer zone rule");
            Assert.That(health.Lives, Is.EqualTo(lives - 1), "LIVES -= 1");
            var at = arena.PlayerRoomPosition();
            Assert.That(at.x, Is.EqualTo(1920f).Within(60f), "reset_player: x = room_width / 2");
            Assert.That(at.y, Is.LessThan(100f), "dropping in from just above the room");
            Assert.That(arena.Player.State, Is.EqualTo(PlayerFlightState.Moving));
            Assert.That(health.IsSpawnProtected, Is.True, "justspawned");
            Assert.That(arena.Arrows.OffscreenArrow.enabled, Is.False, "no big arrow while just spawned");
        }

        [UnityTest]
        public IEnumerator TheBigArrowPointsAtAShipOutOfTheRoom_AndTheLeapLineRunsToTheMouse()
        {
            yield return Enter(RoomId.GameScreen);
            var arena = Arena();
            var health = arena.Player.GetComponent<PlayerHealth>();
            health.Tick(5f); // past justspawned
            arena.Player.Land();
            Put(arena, -100f, 700f);
            yield return null;
            yield return null;
            var arrow = arena.Arrows.OffscreenArrow;
            Assert.That(arrow.enabled, Is.True);
            Assert.That(arrow.sprite, Is.Not.Null, "spr_big_arrow");
            Assert.That(Vector2.Distance(arrow.transform.position, arena.ToWorld(64f, 700f)), Is.LessThan(0.01f), "64 px inside the left edge");
            Assert.That(arrow.color.a, Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(arena.Arrows.DirectionArrow.enabled, Is.False, "no spin");

            // Perched in the room, the mouse below the ship (within jump_arc of its 270 facing): the dotted leap line.
            Put(arena, 1920f, 1080f);
            yield return null;
            yield return null;
            Assert.That(arrow.enabled, Is.False);
            var camera = Camera.main;
            Vector2 below = camera.WorldToScreenPoint(arena.ToWorld(1920f, 1400f));
            Set(mouse.position, below);
            yield return null;
            yield return null;
            Assert.That(arena.Player.AimPoint.HasValue, Is.True);
            Assert.That(arena.Player.CanLeap, Is.True);
            Assert.That(arena.Arrows.LeapDashes, Is.GreaterThan(10), "4 px dashes over ~300 px");
            Assert.That(arena.Arrows.Dashes[0].color.a, Is.EqualTo(0.3f).Within(1e-4f));

            Vector2 above = camera.WorldToScreenPoint(arena.ToWorld(1920f, 800f));
            Set(mouse.position, above);
            yield return null;
            yield return null;
            Assert.That(arena.Player.CanLeap, Is.False, "behind the ship is outside jump_arc");
            Assert.That(arena.Arrows.LeapDashes, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator TheShaftWrapsTheShipAroundItsEdges()
        {
            yield return Enter(RoomId.Shaft);
            var arena = Arena();
            Assert.That(arena.State.Setting.Script, Is.EqualTo("level_04"));
            Assert.That(arena.State.LoopHorizontal && arena.State.LoopVertical, Is.True);
            Assert.That(arena.Layers[0].sprite.texture.name, Is.EqualTo("bg_stormclouds"));
            var health = arena.Player.GetComponent<PlayerHealth>();
            int lives = health.Lives;
            arena.Player.Land();
            int wraps = 0;
            arena.EventPlayed += e => { if (e is PlayerWrapped) wraps++; };
            Put(arena, -150f, 5000f);
            yield return WaitFor(() => wraps > 0, "the loop_horizontal wrap");
            Assert.That(arena.PlayerRoomPosition().x, Is.EqualTo(1920f + 96f).Within(1f), "x = room_width + sprite_width");
            Assert.That(health.Lives, Is.EqualTo(lives), "a wrap costs nothing");

            int scroll = arena.State.Layers[0].X;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(arena.State.Layers[0].X, Is.LessThan(scroll), "level_04: background_hspeed = -1");
        }

        [UnityTest]
        public IEnumerator TheShaftPlacesItsStructuresAndTheShipLandsOnOne()
        {
            // U.HARE-T56: level_04's structure_min 1 / structure_max 3, each under its cover, none over the spawn point;
            // setup_level again clears them before placing the next; a ship flown onto one perches there.
            yield return Enter(RoomId.Shaft);
            var arena = Arena();
            Structure first = null;
            for (int tries = 0; tries < 20 && first == null; tries++)
            {
                var placed = arena.Structures.Structures;
                Assert.That(placed.Count, Is.InRange(0, 3), "irandom_range(1, 3), less any move_rocks cleared");
                foreach (var s in placed)
                {
                    Assert.That(s.Cover, Is.Not.Null);
                    Assert.That((Vector2)s.Cover.transform.position, Is.EqualTo(s.Position));
                    var at = arena.ToRoom(s.Position);
                    Assert.That(at.y < 800f && Mathf.Abs(at.x - arena.State.Width / 2f) <= 400f, Is.False, "move_rocks cleared the spawn point");
                }
                // One far enough down that the ship can be put above it without the shaft's vertical wrap.
                first = placed.FirstOrDefault(s => arena.ToRoom(s.Position).y >= 400f);
                if (first != null) break;
                arena.Begin(4);
            }
            Assert.That(first, Is.Not.Null, "level_04 places a structure below y 400 in 20 setups");
            Assert.That(UnityEngine.Object.FindObjectsByType<StructureCover>(FindObjectsSortMode.None).Length, Is.EqualTo(arena.Structures.Structures.Count));

            var ship = arena.Player;
            var above = arena.ToRoom(first.Position) - new Vector2(0f, 260f);
            ship.ResetState(); // flying straight down, as reset_player sends it
            Put(arena, above.x, above.y);
            yield return WaitFor(() => ship.State == PlayerFlightState.Idle, "the ship perching on the structure");
            Assert.That(ship.Perch, Is.SameAs(first), "current_structure");

            var cover = first.Cover;
            arena.Begin(4);
            yield return null;
            Assert.That(first == null && cover == null, Is.True, "setup_level: with(par_structure) and with(par_structure_cover) instance_destroy()");
        }

        [UnityTest]
        public IEnumerator TheArenaRenders()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("needs a graphics device: run the PlayMode tests without -nographics (tools/unity.ps1 -Graphics)");
            const int W = 1920, H = 1080;
            yield return Enter(RoomId.GameScreen);
            var arena = Arena();
            arena.Player.Land();
            arena.State.Execute(new ToggleMinimap(RoomFlowDirector.Seat));
            yield return new WaitForSecondsRealtime(0.5f);

            var camera = arena.ViewCamera;
            var minimap = arena.MinimapCamera;
            var target = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var cameraRect = camera.rect;
            var minimapRect = minimap.rect;
            camera.GetComponent<LetterboxCamera>().enabled = false;
            minimap.GetComponent<LetterboxCamera>().enabled = false;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            minimap.rect = LetterboxCamera.ComputePortRect(new Rect(0f, 0f, 1f, 1f), new Rect(1600f, 800f, 300f, 200f));
            camera.targetTexture = target;
            minimap.targetTexture = target;
            camera.Render();
            minimap.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(W, H, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;
            minimap.targetTexture = null;
            camera.rect = cameraRect;
            minimap.rect = minimapRect;
            var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Screenshots");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "arena.png"), texture.EncodeToPNG());
            double brightness = texture.GetPixels32().Average(p => (p.r + p.g + p.b) / 3.0);
            UnityEngine.Object.Destroy(texture);
            target.Release();
            Assert.That(brightness, Is.GreaterThan(4.0), "the world curve is drawn, not a black screen");
        }
    }
}
