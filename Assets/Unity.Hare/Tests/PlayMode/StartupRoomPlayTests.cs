using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Unity.Hare.Flow;
using Unity.Hare.Rendering;
using Unity.Hare.Rooms;
using Unity.Hare.UI;

namespace Unity.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T51, played: the startup scene comes up fullscreen with its three circuit boards drifting over the veil and
    /// schematic and the red outlined controller note at the bottom, and a real key press (F1 among them) or mouse click
    /// goes on to the title. With a graphics device, the room is also rendered to Screenshots/startup.png.
    /// </summary>
    public class StartupRoomPlayTests : InputTestFixture
    {
        Keyboard keyboard;
        Mouse mouse;
        bool fullscreen;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
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

        IEnumerator Tap(ButtonControl button)
        {
            Press(button, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(button, queueEventOnly: true);
            yield return null;
        }

        /// <summary>A director with the fullscreen seam on <see cref="fullscreen"/>, sent to startup the way the bootstrap sends it.</summary>
        IEnumerator EnterStartup()
        {
            fullscreen = false;
            var director = RoomFlowDirector.Create(enterFirstRoom: false);
            director.GetFullscreen = () => fullscreen;
            director.SetFullscreen = value => fullscreen = value;
            director.Execute(new GoToRoom(RoomFlowDirector.Seat, RoomId.Startup));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "startup" && Startup() != null && Startup().State.Created,
                "the startup scene and obj_startup's Create");
        }

        static StartupRoom Startup() => UnityEngine.Object.FindAnyObjectByType<StartupRoom>();

        [UnityTest]
        public IEnumerator TheRoomGoesFullscreenDrawsItsDriftingBoardsAndTheNote()
        {
            yield return EnterStartup();
            var startup = Startup();
            Assert.That(startup.name, Is.EqualTo(StartupRoom.ObjectName));
            Assert.That(fullscreen, Is.True, "Create: window_set_fullscreen(true)");

            // startup.room.gmx's five backgrounds, back to front, tiled over the whole room.
            var names = new[] { "bg_circuitboard3", "bg_circuitboard2", "bg_circuitboard", "bg_semiopaque80", "bg_rabbit_schematic_transparent" };
            Assert.That(startup.Layers.Count, Is.EqualTo(names.Length));
            for (int i = 0; i < names.Length; i++)
            {
                var renderer = startup.Layers[i];
                Assert.That(renderer.sprite, Is.Not.Null, names[i] + " is drawn with the imported art");
                Assert.That(renderer.sprite.texture.name, Is.EqualTo(names[i]));
                Assert.That(renderer.drawMode, Is.EqualTo(SpriteDrawMode.Tiled), names[i] + " tiles");
                Assert.That(renderer.size.x, Is.GreaterThanOrEqualTo(19.2f * 2f - 0.001f), names[i] + " covers the room wherever it has scrolled");
                Assert.That(renderer.sortingLayerName, Is.EqualTo(HareSortingLayers.Backdrop));
                Assert.That(renderer.sortingOrder, Is.EqualTo(i), "background " + i + " draws over the ones before it");
            }

            // The boards drift on the room's clock; the veil and schematic stay put.
            var before = startup.State.Layers.Select(l => (l.X, l.Y)).ToArray();
            yield return new WaitForSecondsRealtime(0.5f);
            var after = startup.State.Layers.Select(l => (l.X, l.Y)).ToArray();
            Assert.That(Enumerable.Range(0, 3).Any(i => after[i] != before[i]), Is.True, "a circuit board moved");
            Assert.That(after.Skip(3).All(p => p == (0, 0)), Is.True, "the veil and schematic do not move");
            Assert.That(startup.State.Layers.Take(3).All(l => l.Alarm >= 0 && l.Alarm <= 720), Is.True, "alarm[0..2] armed for 1 to 12 seconds");

            // Each board is drawn where GameMaker would put its first tile: background_x mod 1920, one image back.
            startup.PlaceLayers();
            for (int i = 0; i < names.Length; i++)
            {
                var layer = startup.State.Layers[i];
                Assert.That(startup.TileStart(i), Is.EqualTo(new Vector2Int(StartupLayer.Wrap(layer.X, 1920) - 1920, StartupLayer.Wrap(layer.Y, 1080) - 1080)), names[i]);
            }

            // The note: fnt_point, red on a 2 px black outline, centred, its top at room_height - 60.
            var note = startup.Note;
            Assert.That(note, Is.Not.Null);
            var labels = note.Query<Label>().ToList();
            Assert.That(labels.Count, Is.EqualTo(5 * 5 + 1), "draw_text_outline_color draws the (2*2+1)^2 outline square, then the text");
            Assert.That(labels.Last().text, Does.Contain("wireless Xbox 360 controller plugged in"));
            Assert.That(labels.Last().text, Does.Contain("\nThis is a known issue with the development software."), "# is a line break");
            Assert.That(labels.Last().resolvedStyle.color, Is.EqualTo(Color.red), "c_red");
            Assert.That(labels.Take(labels.Count - 1).All(l => l.resolvedStyle.color == Color.black), Is.True, "c_black outline");
            Assert.That(labels.Last().resolvedStyle.unityFontDefinition.fontAsset, Is.SameAs(HareFonts.Get(HareFonts.Point)), "fnt_point");
            Assert.That(note.resolvedStyle.unityTextAlign, Is.EqualTo(TextAnchor.UpperCenter), "fa_center");
            Assert.That(note.layout.y, Is.EqualTo(1080f - 60f), "room_height - 60");
            Assert.That(note.layout.width, Is.EqualTo(1920f), "centred on room_width / 2");
        }

        [UnityTest]
        public IEnumerator TheRoomRendersTheBoardsAndTheRedNote()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("needs a graphics device: run the PlayMode tests without -nographics (tools/unity.ps1 -Graphics)");
            const int W = 1920, H = 1080;
            yield return EnterStartup();
            var startup = Startup();
            yield return new WaitForSecondsRealtime(0.3f);

            // The room camera's picture: the backgrounds.
            var camera = Camera.main;
            var world = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var rect = camera.rect;
            var letterbox = camera.GetComponent<LetterboxCamera>();
            letterbox.enabled = false;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.targetTexture = world;

            // The note's panel, drawn on its own over clear.
            // HareUiFit centres the stage for the batch-mode window's own aspect (not 16:9), so for this 1920x1080 capture
            // it is switched off and the stage pinned where a 16:9 screen puts it, at the origin.
            var ui = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32);
            ui.Create();
            var document = startup.GetComponentInChildren<UIDocument>();
            document.GetComponent<HareUiFit>().enabled = false;
            var stage = document.rootVisualElement.Q(name: HarePanel.StageName);
            var fitted = HareUiFit.ComputeStageRect(W, H);
            stage.style.left = fitted.x;
            stage.style.top = fitted.y;
            var settings = document.panelSettings;
            settings.targetTexture = ui;
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1f;
            settings.clearColor = true;
            settings.colorClearValue = Color.clear;
            document.panelSettings = settings;
            for (int i = 0; i < 4; i++) yield return null;
            var backdrop = Read(world, W, H);
            var note = Read(ui, W, H);
            camera.targetTexture = null;
            camera.rect = rect;
            letterbox.enabled = true;

            var composed = new Color32[W * H];
            for (int i = 0; i < composed.Length; i++) composed[i] = Color32.Lerp(backdrop[i], new Color32(note[i].r, note[i].g, note[i].b, 255), note[i].a / 255f);
            var texture = new Texture2D(W, H, TextureFormat.RGBA32, false);
            texture.SetPixels32(composed);
            texture.Apply();
            var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Screenshots");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "startup.png"), texture.EncodeToPNG());
            texture.SetPixels32(note);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(folder, "startup-note.png"), texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
            world.Release();
            ui.Release();

            double brightness = backdrop.Average(p => (p.r + p.g + p.b) / 3.0);
            Assert.That(brightness, Is.GreaterThan(4.0), "the circuit boards are drawn, not a black screen");
            bool Red(Color32 p) => p.a > 200 && p.r > 180 && p.g < 80 && p.b < 80;
            int RedIn(int top, int bottom) // rows counted from the top, as GameMaker counts y
            {
                int count = 0;
                for (int y = top; y < bottom; y++)
                for (int x = 0; x < W; x++)
                    if (Red(note[(H - 1 - y) * W + x])) count++;
                return count;
            }
            Assert.That(RedIn(1015, H), Is.GreaterThan(200), "the red note at room_height - 60");
            Assert.That(RedIn(0, 1010), Is.EqualTo(0), "nothing red above it");
        }

        static Color32[] Read(RenderTexture rt, int width, int height)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            var pixels = texture.GetPixels32();
            UnityEngine.Object.Destroy(texture);
            return pixels;
        }

        [UnityTest]
        public IEnumerator F1GoesToTheTitleWithoutTogglingFullscreen()
        {
            yield return EnterStartup();
            fullscreen = false;
            yield return Tap(keyboard.f1Key);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_title", "F1 (any key) to go to the title");
            Assert.That(RoomFlowDirector.Active.Flow.Current, Is.EqualTo(RoomId.Title));
            Assert.That(fullscreen, Is.False, "the title's F1 event does not also answer the key that left startup");
        }

        [UnityTest]
        public IEnumerator AnyKeyGoesToTheTitle()
        {
            yield return EnterStartup();
            yield return Tap(keyboard.spaceKey);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_title", "a key press to go to the title");
        }

        [UnityTest]
        public IEnumerator TheLeftMouseButtonGoesToTheTitle()
        {
            yield return EnterStartup();
            yield return Tap(mouse.leftButton);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_title", "a click to go to the title");
        }

        [UnityTest]
        public IEnumerator WithoutInputTheRoomStays()
        {
            yield return EnterStartup();
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("startup"));
            Assert.That(Startup().State.Continued, Is.False);
        }
    }
}
