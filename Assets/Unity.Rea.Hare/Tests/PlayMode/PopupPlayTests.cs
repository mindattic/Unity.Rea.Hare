using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Unity.Hare.Arena;
using Unity.Hare.Content;
using Unity.Hare.Enemies;
using Unity.Hare.Flow;
using Unity.Hare.Popups;
using Unity.Hare.Rendering;
using Unity.Hare.UI;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T27, played: in room_gamescreen a rocket kill scores the dagger's 10 and floats "10" up from where it died,
    /// flashing and fading until it goes; obj_tip slides in at the lower right and any key takes it away; obj_alert rides
    /// obj_earthball's rim and pings. With a graphics device the score popups and a tip are rendered to
    /// Screenshots/float-text.png and Screenshots/tip.png.
    /// </summary>
    public class PopupPlayTests : InputTestFixture
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
            Gml.Paused = false;
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

        static IEnumerator Enter()
        {
            var director = RoomFlowDirector.Create(enterFirstRoom: false);
            director.GetFullscreen = () => false;
            director.SetFullscreen = _ => { };
            director.Execute(new GoToRoom(RoomFlowDirector.Seat, RoomId.GameScreen));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == RoomList.GmlName(RoomId.GameScreen) && Arena() != null
                && Arena().State.Created && Arena().Player != null, "room_gamescreen and obj_game's Create");
        }

        [UnityTest]
        public IEnumerator ARocketKillScoresAndFloatsItsPointsUpUntilTheyFade()
        {
            yield return Enter();
            var arena = Arena();
            var session = arena.Session;
            int before = session.Score;

            var at = arena.Player.transform.position;
            var spawned = EnemyDirector.Execute(new SpawnEnemy(EnemyKind.Dagger, new Vec2(at.x, at.y)));
            Assert.That(spawned.Body, Is.Not.Null);
            EnemyDirector.Execute(new StrikeRocket(spawned.Body.Id, new Vec2(at.x, at.y)));
            Assert.That(session.Score, Is.EqualTo(before + 10), "current_score += 10");
            Assert.That(arena.FloatTexts.Texts, Has.Count.GreaterThanOrEqualTo(1));
            var text = arena.FloatTexts.Texts[arena.FloatTexts.Texts.Count - 1];
            Assert.That(text.Text, Is.EqualTo("10"), "show_float_text_ext(x, y, \"10\", true, 0.01, 1)");
            Assert.That(text.FadeSpeed, Is.EqualTo(0.01));
            var room = arena.ToRoom(at);
            Assert.That(text.X, Is.EqualTo(room.x).Within(0.5));
            Assert.That(text.Y, Is.EqualTo(room.y).Within(0.5));

            yield return null;
            var element = arena.FloatTextView.ElementOf(text);
            Assert.That(element, Is.Not.Null, "drawn on the GUI layer");
            Assert.That(element.parent, Is.SameAs(arena.FloatTextView));
            Assert.That(arena.FloatTextView.parent, Is.SameAs(arena.Hud));
            Assert.That(arena.Hud.IndexOf(arena.FloatTextView), Is.LessThan(arena.Hud.IndexOf(arena.HudView)), "under the HUD objects");
            arena.Present(); // drawn against view 0 as it is now
            Assert.That(element.style.left.value.value, Is.EqualTo((float)text.X - arena.State.View.X).Within(0.01f));
            Assert.That(element.style.top.value.value, Is.EqualTo((float)text.DrawY - arena.State.View.Y).Within(0.01f));

            // It rises a pixel and fades a hundredth a step, and goes on its 101st step (0.84 s at room_speed 120).
            yield return WaitFor(() => text.Steps >= 10, "ten steps");
            Assert.That(text.Y, Is.EqualTo(room.y - text.Steps).Within(0.5), "y -= 1 a step");
            Assert.That(text.Alpha, Is.EqualTo(1.0 - 0.01 * text.Steps).Within(1e-6), "alpha -= 0.01 a step");
            yield return WaitFor(() => text.Gone, "the text fading out", 3f);
            yield return null;
            Assert.That(arena.FloatTextView.ElementOf(text), Is.Null);

            // A laser kill does not score (only KILLS).
            int score = session.Score;
            int texts = arena.FloatTexts.Texts.Count;
            var second = EnemyDirector.Execute(new SpawnEnemy(EnemyKind.Dagger, new Vec2(at.x, at.y)));
            EnemyDirector.Execute(new StrikeBeam(second.Body.Id, BeamKind.Laser));
            Assert.That(session.Score, Is.EqualTo(score));
            Assert.That(arena.FloatTexts.Texts, Has.Count.EqualTo(texts));
        }

        [UnityTest]
        public IEnumerator ATipSlidesInAndAnyKeyTakesItAway()
        {
            yield return Enter();
            var arena = Arena();
            var definition = GameContent.Current.Popups.Tip;
            var tip = new TipBox(definition, definition.IntroText, 1920, 1080);
            var view = TipView.Show(arena.Hud, tip, 60);
            Assert.That(view.parent, Is.SameAs(arena.Hud));
            Assert.That(view.style.left.value.value, Is.EqualTo(1920f), "x = view_wview");
            Assert.That(view.style.top.value.value, Is.EqualTo(800f), "y = view_hview - height - 30");
            yield return WaitFor(() => tip.Mode == TipMode.Idle, "the tip sliding in", 3f);
            Assert.That(view.style.left.value.value, Is.EqualTo(1342f), "endX = view_wview - width - 30");
            Assert.That(view.TextElement.style.left.value.value, Is.EqualTo(20f));

            Press(keyboard.qKey);
            yield return null;
            yield return null;
            Release(keyboard.qKey);
            Assert.That(tip.Gone, Is.True, "its key press event: instance_destroy()");
            Assert.That(view.parent, Is.Null);
        }

        [UnityTest]
        public IEnumerator AnAlertRidesTheEarthsRimAndPings()
        {
            var host = new GameObject("map");
            var definition = GameContent.Current.Popups.Alert;
            var mission = definition.Missions[2];
            var view = AlertMarkerView.Create(host.transform, new AlertMarker(definition, mission.Degree, mission.ScenarioId),
                (x, y) => new Vector2(GameClock.Px((float)x), -GameClock.Px((float)y)));
            int pings = 0;
            view.Pinged += _ => pings++;
            for (int step = 1; step <= 30; step++) view.Step(960, 540, 10, true);
            Assert.That(pings, Is.EqualTo(1), "alarm[0] = ping_rate: a red ef_ring every 30 steps while obj_map idles");
            Assert.That(view.Marker.ImageAngle, Is.EqualTo(280.0), "degree + obj_earthball.image_angle");
            Assert.That(view.Renderer.sprite, Is.Not.Null, "spr_alertmarker imported");
            Assert.That(view.transform.eulerAngles.z, Is.EqualTo(280f).Within(0.01f));
            Assert.That(view.transform.position.x, Is.EqualTo(GameClock.Px((float)(960 + AlertMarker.LengthDirX(550, 280)))).Within(1e-3f));
            Assert.That(view.Renderer.sortingOrder, Is.EqualTo(10), "depth -10");
            UnityEngine.Object.Destroy(host);
            yield return null;
        }

        const int W = 1920, H = 1080;
        Color32[] rendered;

        [UnityTest]
        public IEnumerator TheScorePopupsAndATipRender()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("needs a graphics device: run the PlayMode tests without -nographics (tools/unity.ps1 -Graphics)");
            yield return Enter();
            var arena = Arena();
            yield return WaitFor(() => arena.BannerView == null, "the level banner fading", 8f);
            arena.Player.Land();

            // Three kills' worth of popups in the middle of the view, a few steps old.
            var view = arena.State.View;
            float cx = view.X + view.Width / 2f, cy = view.Y + view.Height / 2f;
            arena.Score(arena.ToWorld(cx - 200, cy), 10);
            var first = arena.FloatTexts.Texts[arena.FloatTexts.Texts.Count - 1];
            arena.Score(arena.ToWorld(cx, cy - 100), 50);
            arena.Score(arena.ToWorld(cx + 200, cy), 200);
            yield return WaitFor(() => first.Steps >= 12, "a few steps of float", 2f);
            Gml.Paused = true; // hold the room still for the picture (the texts keep stepping, as obj_float_text ignores paused)
            float left = (float)first.X - arena.State.View.X, top = (float)first.DrawY - arena.State.View.Y;
            yield return Render(arena, "float-text.png");
            var hud = rendered;
            Assert.That(Count(hud, (int)left - 40, (int)top - 30, (int)left + 40, (int)top + 40), Is.GreaterThan(30), "\"10\" where it was made");
            Gml.Paused = false;

            var definition = GameContent.Current.Popups.Tip;
            var tip = new TipBox(definition, definition.IntroText, 1920, 1080);
            TipView.Show(arena.Hud, tip, 60);
            yield return WaitFor(() => tip.Mode == TipMode.Idle && arena.FloatTexts.Texts.Count == 0, "the tip sliding in", 4f);
            yield return Render(arena, "tip.png");
            hud = rendered;
            Assert.That(Count(hud, 1342, 800, 1890, 1050), Is.GreaterThan(50000), "spr_tip_box at (view_wview - 578, view_hview - 280)");
            Assert.That(Count(hud, 1300, 760, 1340, 799), Is.EqualTo(0), "nothing above and left of it");
        }

        static int Count(Color32[] hud, int left, int top, int right, int bottom) // rows counted from the top, as GameMaker counts y
        {
            int count = 0;
            for (int y = Math.Max(0, top); y < Math.Min(H, bottom); y++)
            for (int x = Math.Max(0, left); x < Math.Min(W, right); x++)
                if (hud[(H - 1 - y) * W + x].a > 128) count++;
            return count;
        }

        /// <summary>Renders the room and its GUI layer to Screenshots/<paramref name="file"/>; the GUI layer alone is left in <see cref="rendered"/>.</summary>
        IEnumerator Render(ArenaRoom arena, string file)
        {
            var camera = arena.ViewCamera;
            var world = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            var rect = camera.rect;
            var letterbox = camera.GetComponent<LetterboxCamera>();
            letterbox.enabled = false;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.targetTexture = world;

            var ui = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32);
            ui.Create();
            var document = arena.GetComponentInChildren<UIDocument>();
            document.GetComponent<HareUiFit>().enabled = false;
            var stage = document.rootVisualElement.Q(name: HarePanel.StageName);
            var fitted = HareUiFit.ComputeStageRect(W, H);
            stage.style.left = fitted.x;
            stage.style.top = fitted.y;
            var settings = document.panelSettings;
            var previousTarget = settings.targetTexture;
            var previousMode = settings.scaleMode;
            settings.targetTexture = ui;
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1f;
            settings.clearColor = true;
            settings.colorClearValue = Color.clear;
            document.panelSettings = settings;
            for (int i = 0; i < 4; i++) yield return null;
            var backdrop = Read(world, W, H);
            var hud = Read(ui, W, H);
            camera.targetTexture = null;
            camera.rect = rect;
            letterbox.enabled = true;
            settings.targetTexture = previousTarget;
            settings.scaleMode = previousMode;
            document.GetComponent<HareUiFit>().enabled = true;

            var composed = new Color32[W * H];
            for (int i = 0; i < composed.Length; i++) composed[i] = Color32.Lerp(backdrop[i], new Color32(hud[i].r, hud[i].g, hud[i].b, 255), hud[i].a / 255f);
            var texture = new Texture2D(W, H, TextureFormat.RGBA32, false);
            texture.SetPixels32(composed);
            texture.Apply();
            var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Screenshots");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, file), texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
            world.Release();
            ui.Release();
            rendered = hud;
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
    }
}
