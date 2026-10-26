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
using UnityEngine.UIElements;
using Unity.Hare.Arena;
using Unity.Hare.Flow;
using Unity.Hare.Player;
using Unity.Hare.Rendering;
using Unity.Hare.UI;

namespace Unity.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T25, played: room_gamescreen's HUD (obj_healthbar's lives, strTime, coins and health pips, obj_weapon_gui,
    /// obj_energybar and obj_speedometer) is drawn on the 1920x1080 GUI layer and follows the ship: the air weapons in
    /// flight and the ground ones perched, Ctrl moving the select box, a hit emptying a pip, the speedometer running down
    /// once perched. With a graphics device the room and its HUD are rendered to Screenshots/hud.png.
    /// </summary>
    public class GameHudPlayTests : InputTestFixture
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
            if (RoomFlowDirector.Active != null) UnityEngine.Object.Destroy(RoomFlowDirector.Active.gameObject);
            var empty = SceneManager.GetSceneByName("GameHudPlayTests.Empty");
            if (!empty.IsValid()) empty = SceneManager.CreateScene("GameHudPlayTests.Empty");
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
        public IEnumerator TheHudIsDrawnAndFollowsTheShip()
        {
            yield return Enter();
            var arena = Arena();
            yield return null;
            yield return null;
            var view = arena.HudView;
            Assert.That(view, Is.Not.Null);
            Assert.That(view.parent, Is.SameAs(arena.Hud));

            Assert.That(view.TextOf("lives"), Is.EqualTo("x " + arena.Player.GetComponent<PlayerHealth>().Lives));
            Assert.That(view.TextOf("clock"), Is.EqualTo(arena.Clock.Text), "obj_healthbar's strTime, the one clock");
            Assert.That(view.TextOf("coins"), Is.EqualTo(arena.Session.Coins.ToString()));
            Assert.That(view.TextOf("energy"), Is.EqualTo("100%"));
            var pip = view.SpriteElement("pip0");
            Assert.That(pip, Is.Not.Null);
            Assert.That(pip.style.backgroundImage.value.sprite, Is.Not.Null, "spr_health_pip imported");
            // The style, not the layout: the panel is scaled to the batch-mode window, which rounds layout to its pixels.
            Assert.That(pip.style.left.value.value, Is.EqualTo(140f));
            Assert.That(pip.style.top.value.value, Is.EqualTo(1080f - 64f - 36f));
            Assert.That(pip.style.width.value.value, Is.EqualTo(44f), "drawn at the sprite's own size");
            Assert.That(view.SpriteElement("pip5"), Is.Not.Null, "MAXHP 6");

            // reset_player flies the ship in: the air weapons and the speedometer.
            Assert.That(arena.Player.State, Is.EqualTo(PlayerFlightState.Moving));
            Assert.That(arena.HudView.Frame.Sprite("weapon-primary").Value.Sprite, Is.EqualTo("spr_icon_rocket"));
            Assert.That(view.SpriteElement("weapon-primary").style.backgroundImage.value.sprite, Is.Not.Null);
            Assert.That(view.TextOf("speed"), Is.Not.Null.And.Not.EqualTo("0"));
            Assert.That(view.TextOf("speed-unit"), Is.EqualTo(" mph"));

            // Perched: the ground weapons, the select box on the primary until Ctrl is held.
            arena.Player.Land();
            yield return null;
            yield return null;
            var frame = view.Frame;
            Assert.That(frame.Sprite("weapon-primary").Value.Sprite, Is.EqualTo("spr_icon_vulcan"));
            Assert.That(frame.Sprite("weapon-secondary").Value.Sprite, Is.EqualTo("spr_icon_shotgun"));
            Assert.That(view.SpriteElement("weapon-select").layout.x, Is.EqualTo(view.SpriteElement("weapon-primary").layout.x));
            var ship = arena.Player.GetComponent<PlayerSeat>().Ship;
            Assert.That(view.TextOf("ammo-primary"), Is.EqualTo(ship.Ammo.Current(Unity.Hare.Weapons.AmmoPool.GroundPrimary) + " / "
                + ship.Ammo.Max(Unity.Hare.Weapons.AmmoPool.GroundPrimary)), "ground_ammo_primary / ground_ammo_primary_max");
            Press(keyboard.leftCtrlKey);
            yield return null;
            yield return null;
            Assert.That(view.SpriteElement("weapon-select").layout.x, Is.EqualTo(view.SpriteElement("weapon-secondary").layout.x), "Ctrl held");
            Release(keyboard.leftCtrlKey);

            // The speedometer runs down by 0.95 a step once perched and then goes.
            yield return WaitFor(() => view.TextElement("speed") == null, "the speedometer running down", 3f);

            // A hit empties the last pip.
            var health = arena.Player.GetComponent<PlayerHealth>();
            health.Tick(5f); // past justspawned
            Assert.That(health.TryTakeHit(), Is.True);
            yield return null;
            yield return null;
            Assert.That(view.Frame.Sprite("pip5").Value.Sprite, Is.EqualTo("spr_health_pip_empty"));
            Assert.That(view.SpriteElement("pip5-back"), Is.Not.Null);
            Assert.That(view.Frame.Sprite("pip4").Value.Sprite, Is.EqualTo("spr_health_pip"));
        }

        [UnityTest]
        public IEnumerator ObjGameDrawsTheScoreAndObjectiveAndF12TogglesTheDebugOverlay()
        {
            yield return Enter();
            var arena = Arena();
            yield return null;
            yield return null;
            var view = arena.HudView;

            // U.HARE-T59: obj_game's Draw GUI, current_score and strObjective at the lower right.
            Assert.That(view.TextOf("score"), Is.EqualTo(arena.Session.Score.ToString()));
            Assert.That(view.TextOf("objective"), Is.EqualTo(arena.Session.ObjectiveText).And.Not.Empty, "set_objective_string's strObjective");
            var score = view.TextElement("score");
            Assert.That(score.style.left.value.value, Is.EqualTo(1920f - 200f));
            Assert.That(score.style.top.value.value, Is.EqualTo(1080f - 90f));
            Assert.That(view.TextElement("objective").style.top.value.value, Is.EqualTo(1080f - 35f));
            arena.Session.Execute(new Unity.Hare.Session.AddScore(RoomFlowDirector.Seat, 120));
            yield return null;
            Assert.That(view.TextOf("score"), Is.EqualTo(arena.Session.Score.ToString()));

            // F12: debug = !debug.
            Assert.That(view.TextElement("debug-fps"), Is.Null, "debug starts off");
            Press(keyboard.f12Key);
            yield return null;
            Release(keyboard.f12Key);
            yield return null;
            Assert.That(arena.Session.Debug, Is.True);
            yield return WaitFor(() => view.TextOf("debug-fps") != null && view.TextOf("debug-fps") != "FPS: 0", "a second of frames counted", 3f);
            Assert.That(view.TextOf("debug-state"), Is.EqualTo("State: Moving"));
            Assert.That(view.TextOf("debug-rock"), Is.EqualTo("Current Rock: Null"));
            Assert.That(view.TextOf("debug-track"), Is.EqualTo("Current Track: Pinball Spring"));
            Assert.That(view.TextOf("debug-position"), Does.StartWith("X,Y: "));
            Press(keyboard.f12Key);
            yield return null;
            Release(keyboard.f12Key);
            yield return null;
            Assert.That(arena.Session.Debug, Is.False);
            Assert.That(view.TextElement("debug-fps"), Is.Null);
        }

        const int W = 1920, H = 1080;
        Color32[] rendered;

        [UnityTest]
        public IEnumerator TheHudRenders()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("needs a graphics device: run the PlayMode tests without -nographics (tools/unity.ps1 -Graphics)");
            yield return Enter();
            var arena = Arena();
            // Wait out obj_levelstart's banner so the HUD stands alone, then knock a pip off and use some energy.
            yield return WaitFor(() => arena.BannerView == null, "the level banner fading", 8f);
            var health = arena.Player.GetComponent<PlayerHealth>();
            health.Tick(5f);
            health.TryTakeHit();
            arena.Player.TryBoost();
            yield return new WaitForSecondsRealtime(0.3f);
            yield return Render(arena, "hud.png");
            var hud = rendered;

            int Drawn(int left, int top, int right, int bottom) => Count(hud, left, top, right, bottom);
            Assert.That(Drawn(0, 978, 64, 1080), Is.GreaterThan(500), "spr_extralives at the bottom left");
            Assert.That(Drawn(140, 980, 404, 1044), Is.GreaterThan(2000), "the health pips");
            Assert.That(Drawn(145, 1047, 399, 1077), Is.GreaterThan(2000), "the energy bar");
            Assert.That(Drawn(1776, 1003, 1900, 1060), Is.GreaterThan(1000), "the weapon icons");
            Assert.That(Drawn(1500, 990, 1720, 1035), Is.GreaterThan(200), "obj_game's score, right-aligned at view_wview - 200");
            Assert.That(Drawn(1300, 1045, 1720, 1065), Is.GreaterThan(200), "obj_game's strObjective under it");
            // The perched primary's ammo readout is right-aligned at 1828 on the bottom row and reaches 6 px past x 1770 while the
            // count is still above the lowered max ("150 / 120", faithful to HARE.gmx, BIBLE V34) and less once it regens to "120 / 120",
            // which is why a wider box failed only some runs. Stop the box short of the widest real readout (9 glyphs, left edge ~1764).
            Assert.That(Drawn(1723, 990, 1750, 1065), Is.EqualTo(0), "both end at view_wview - 200 (the outline reaches a pixel past it); the box stops short of the ammo readout's left edge");
            Assert.That(Drawn(600, 0, 1300, 900), Is.EqualTo(0), "nothing over the middle of the play field");
            Assert.That(Drawn(0, 0, 500, 320), Is.EqualTo(0), "no debug overlay until F12");
        }

        [UnityTest]
        public IEnumerator TheDebugOverlayRenders()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("needs a graphics device: run the PlayMode tests without -nographics (tools/unity.ps1 -Graphics)");
            yield return Enter();
            var arena = Arena();
            yield return WaitFor(() => arena.BannerView == null, "the level banner fading", 8f);
            Press(keyboard.f12Key);
            yield return null;
            Release(keyboard.f12Key);
            yield return WaitFor(() => arena.HudView.TextOf("debug-fps") != "FPS: 0", "a second of frames counted", 3f);
            yield return Render(arena, "hud-debug.png");
            var hud = rendered;

            Assert.That(Count(hud, 20, 20, 300, 40), Is.GreaterThan(100), "FPS at (20, 20)");
            Assert.That(Count(hud, 20, 60, 300, 220), Is.GreaterThan(1000), "the ship's lines from y 60 to 200");
            Assert.That(Count(hud, 20, 300, 400, 320), Is.GreaterThan(100), "Current Track at (20, 300)");
            Assert.That(Count(hud, 0, 0, 19, 320), Is.EqualTo(0), "fa_left from x 20 (the outline reaches 19)");
        }

        static int Count(Color32[] hud, int left, int top, int right, int bottom) // rows counted from the top, as GameMaker counts y
        {
            int count = 0;
            for (int y = top; y < bottom; y++)
            for (int x = left; x < right; x++)
                if (hud[(H - 1 - y) * W + x].a > 128) count++;
            return count;
        }

        /// <summary>Renders the room and its HUD to Screenshots/<paramref name="file"/>; the HUD layer alone is left in <see cref="rendered"/>.</summary>
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
