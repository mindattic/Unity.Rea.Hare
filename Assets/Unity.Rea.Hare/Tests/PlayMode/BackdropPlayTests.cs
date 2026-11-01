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
using Unity.Rea.Hare.Arena;
using Unity.Rea.Hare.Enemies;
using Unity.Rea.Hare.Flow;
using Unity.Rea.Hare.Rendering;
using Unity.Rea.Hare.Session;

namespace Unity.Rea.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T58, seen: every level's backdrop as its script selects it (bg_worldcurve, bg_moonview, bg_uphigh,
    /// bg_stormclouds scrolling left in the shaft, bg_tropicalocean with obj_surface_reflection's water line, bg_nightsky
    /// tiled, bg_atmosphere, bg_orange for level_default, bg_gradient3 over bg_nightsky for the generator), each rendered
    /// with a graphics device to Screenshots/backdrop_&lt;script&gt;.png and checked to be drawn.
    /// </summary>
    public class BackdropPlayTests : InputTestFixture
    {
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

        static ArenaRoom Arena() => UnityEngine.Object.FindAnyObjectByType<ArenaRoom>();

        static IEnumerator Enter(RoomId room, int level, bool generator = false)
        {
            var director = RoomFlowDirector.Active != null ? RoomFlowDirector.Active : RoomFlowDirector.Create(enterFirstRoom: false);
            director.GetFullscreen = () => false;
            director.SetFullscreen = _ => { };
            if (generator) director.ExecuteSession(new ChooseGenerator(RoomFlowDirector.Seat, 0));
            else director.ExecuteSession(new ChooseQuickstart(RoomFlowDirector.Seat, level));
            director.Execute(new GoToRoom(RoomFlowDirector.Seat, room));
            string name = RoomList.GmlName(room);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == name && Arena() != null && Arena().State.Created && Arena().Player != null,
                name + " and obj_game's Create");
        }

        /// <summary>The level as it starts, without its enemies and rocks in front of the backdrop.</summary>
        static IEnumerator Quiet(ArenaRoom arena)
        {
            EnemyDirector.ClearAll();
            arena.Rocks.ClearAll();
            arena.Player.Land();
            yield return null;
        }

        static void Put(ArenaRoom arena, float x, float y)
        {
            var world = arena.ToWorld(x, y);
            var ship = arena.Player;
            ship.transform.position = new Vector3(world.x, world.y, ship.transform.position.z);
            ship.GetComponent<Rigidbody2D>().position = world;
        }

        /// <summary>Renders view 0 at 1920x1080 to Screenshots/<paramref name="file"/> and returns its mean brightness.</summary>
        static double Render(ArenaRoom arena, string file)
        {
            const int W = 1920, H = 1080;
            var camera = arena.ViewCamera;
            var letterbox = camera.GetComponent<LetterboxCamera>();
            bool letterboxWas = letterbox != null && letterbox.enabled;
            var rect = camera.rect;
            var target = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            var texture = new Texture2D(W, H, TextureFormat.RGBA32, false);
            try
            {
                if (letterbox != null) letterbox.enabled = false;
                camera.rect = new Rect(0f, 0f, 1f, 1f);
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                texture.Apply();
                var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Screenshots");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, file), texture.EncodeToPNG());
                return texture.GetPixels32().Average(p => (p.r + p.g + p.b) / 3.0);
            }
            finally
            {
                RenderTexture.active = previous;
                camera.targetTexture = null;
                camera.rect = rect;
                if (letterbox != null) letterbox.enabled = letterboxWas;
                UnityEngine.Object.Destroy(texture);
                target.Release();
                UnityEngine.Object.Destroy(target);
            }
        }

        static void RequireGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("needs a graphics device: run the PlayMode tests without -nographics (tools/unity.ps1 -Graphics)");
        }

        [UnityTest]
        public IEnumerator EveryOpenArenaLevel_DrawsTheBackdropItsScriptSelects()
        {
            RequireGraphics();
            yield return Enter(RoomId.GameScreen, 1);
            var arena = Arena();
            foreach (var (level, background) in new[]
            {
                (1, "bg_worldcurve"), (2, "bg_moonview"), (3, "bg_uphigh"), (6, "bg_nightsky"), (7, "bg_worldcurve"),
                (9, "bg_atmosphere"), (10, "bg_atmosphere"), (11, "bg_orange"),
            })
            {
                arena.Begin(level);
                yield return Quiet(arena);
                Put(arena, 1920f, 1080f);
                yield return new WaitForSecondsRealtime(0.4f);
                Assert.That(arena.Layers[0].sprite.texture.name, Is.EqualTo(background), arena.State.Setting.Script);
                double brightness = Render(arena, "backdrop_" + arena.State.Setting.Script + ".png");
                Assert.That(brightness, Is.GreaterThan(4.0), arena.State.Setting.Script + ": " + background + " is drawn, not a black screen");
            }
        }

        [UnityTest]
        public IEnumerator TheGenerator_DrawsBgGradient3OverTheNightSky()
        {
            RequireGraphics();
            yield return Enter(RoomId.GameScreen, 1, generator: true);
            var arena = Arena();
            Assert.That(arena.State.Setting.Script, Is.EqualTo("level_enemygenerator"));
            yield return Quiet(arena);
            Assert.That(arena.Layers[0].sprite.texture.name, Is.EqualTo("bg_nightsky"));
            Assert.That(arena.Layers[1].sprite.texture.name, Is.EqualTo("bg_gradient3"), "background_index[1]");
            Assert.That(arena.Layers[1].sortingOrder, Is.GreaterThan(arena.Layers[0].sortingOrder), "slot 1 over slot 0");
            Put(arena, 1920f, 1080f);
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(Render(arena, "backdrop_level_enemygenerator.png"), Is.GreaterThan(4.0));
        }

        [UnityTest]
        public IEnumerator TheStageRooms_DrawTheirBackdrops_TheShaftsCloudsScrollingLeft_AndTheSideScrollsWaterReflection()
        {
            RequireGraphics();
            yield return Enter(RoomId.Shaft, 4);
            var shaft = Arena();
            yield return Quiet(shaft);
            Put(shaft, 960f, 3000f);
            int before = shaft.State.Layers[0].X;
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(shaft.State.Layers[0].X, Is.LessThan(before), "level_04: background_hspeed = -1");
            Assert.That(Render(shaft, "backdrop_level_04.png"), Is.GreaterThan(4.0));

            yield return Enter(RoomId.SideScroll, 5);
            var side = Arena();
            yield return Quiet(side);
            Put(side, 3000f, 700f); // 130 px above the water line: its reflection 130 px below it
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.That(side.ReflectionView.Sprite.enabled, Is.True, "obj_surface_reflection is drawn");
            Assert.That(Render(side, "backdrop_level_05.png"), Is.GreaterThan(4.0));
        }
    }
}
