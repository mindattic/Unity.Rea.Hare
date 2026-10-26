using System;
using System.Collections;
using System.Collections.Generic;
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
using Unity.Hare.Input;
using Unity.Hare.Rendering;
using Unity.Hare.Rooms;
using Unity.Hare.Session;
using Unity.Hare.UI;

namespace Unity.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T26, played: room_title comes up with obj_title's hidden pointer; a key brings in obj_logo's buttons and
    /// shows it; hovering a caption turns it c_cornflowerblue with its message at the bottom; Campaign fades to
    /// room_story in the campaign, a level button fades to room_gamescreen in a quick game at that level, and Quit opens
    /// obj_yesno_dialog, whose No and Esc close it and whose Yes and Enter end the game (through a seam, so the run goes
    /// on). With a graphics device the title with its menu and dialog is rendered to Screenshots/title.png.
    /// </summary>
    public class TitleRoomPlayTests : InputTestFixture
    {
        Keyboard keyboard;
        Mouse mouse;
        Action quit;
        int quits;
        Func<bool> getVisible;
        Action<bool> setVisible;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            quit = TitleRoom.QuitGame;
            quits = 0;
            TitleRoom.QuitGame = () => quits++;
            getVisible = SystemCursor.GetVisible;
            setVisible = SystemCursor.SetVisible;
            bool cursor = true;
            SystemCursor.GetVisible = () => cursor;
            SystemCursor.SetVisible = value => cursor = value;
        }

        [UnityTearDown]
        public IEnumerator LeaveNoRoomBehind()
        {
            TitleRoom.QuitGame = quit;
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

        static TitleRoom Title() => UnityEngine.Object.FindAnyObjectByType<TitleRoom>();

        static IEnumerator EnterTitle()
        {
            var director = RoomFlowDirector.Create(enterFirstRoom: false);
            director.GetFullscreen = () => false;
            director.SetFullscreen = _ => { };
            director.Execute(new GoToRoom(RoomFlowDirector.Seat, RoomId.Title));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_title" && Title() != null && Title().State.Created,
                "room_title and obj_title's Create");
        }

        /// <summary>Puts the mouse on room pixel (<paramref name="x"/>, <paramref name="y"/>) and lets the room see it.</summary>
        IEnumerator MoveTo(TitleRoom title, double x, double y)
        {
            var world = title.ToWorld((float)x, (float)y);
            Set(mouse.position, (Vector2)Camera.main.WorldToScreenPoint(new Vector3(world.x, world.y, 0f)));
            yield return null;
            yield return null;
        }

        IEnumerator ClickAt(TitleRoom title, TitleButton button)
        {
            yield return MoveTo(title, button.X, button.Y);
            yield return Tap(mouse.leftButton);
        }

        /// <summary>The title with its menu up and obj_fade_title's fade-out over.</summary>
        IEnumerator TitleWithMenu()
        {
            yield return EnterTitle();
            yield return WaitFor(() => RoomFlowDirector.Active.Flow.Fade.Mode == FadeMode.Off, "obj_fade_title's fade-out");
            yield return Tap(keyboard.spaceKey);
            var title = Title();
            yield return WaitFor(() => title.State.MenuShown, "a key to bring in the menu (scene " + SceneManager.GetActiveScene().name
                + ", " + UnityEngine.Object.FindObjectsByType<TitleRoom>(FindObjectsSortMode.None).Length + " obj_title)");
        }

        static TitleButton Button(TitleRoom title, string obj) => title.State.Buttons.Single(b => b.Definition.Object == obj);

        [UnityTest]
        public IEnumerator TheTitleMakesItsHiddenPointerAndAKeyBringsInTheMenu()
        {
            yield return EnterTitle();
            var title = Title();
            Assert.That(title.name, Is.EqualTo(TitleRoom.ObjectName));
            Assert.That(title.Pointer, Is.Not.Null, "obj_title: mouse = instance_create(0,0,obj_mouse)");
            Assert.That(MouseView.Active, Is.SameAs(title.Pointer), "the room flow makes no second obj_mouse");
            Assert.That(title.Pointer.gameObject.scene.name, Is.EqualTo("room_title"));
            yield return MoveTo(title, 300, 300);
            Assert.That(title.Pointer.State.Sprite, Is.EqualTo("spr_pointer"));
            Assert.That(title.Pointer.Drawn, Is.False, "mouse.visible = false");
            Assert.That(title.PointerElement.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(title.MenuLayer.childCount, Is.EqualTo(0), "no buttons before obj_logo");

            yield return Tap(keyboard.f1Key);
            Assert.That(title.State.MenuShown, Is.False, "obj_title's key press skips F1");

            yield return Tap(keyboard.aKey);
            Assert.That(title.State.MenuShown, Is.True);
            Assert.That(title.State.Menu.Count, Is.EqualTo(12));
            Assert.That(title.MenuLayer.childCount, Is.EqualTo(12 * 2), "a caption and a hover message each");
            yield return null;
            Assert.That(title.Pointer.Drawn, Is.True, "obj_logo: obj_mouse.visible = true");
            Assert.That(title.Pointer.Renderer.enabled, Is.False, "drawn once, on the panel, not also as a world sprite");
            Assert.That(title.PointerElement.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(title.PointerElement.resolvedStyle.left, Is.EqualTo(300f).Within(1f), "spr_pointer's origin (0, 0) on the mouse");
            Assert.That(title.PointerElement.resolvedStyle.top, Is.EqualTo(300f).Within(1f));

            // The captions fade in with obj_logo's image_alpha, 0.02 a step at 60 steps a second.
            var campaign = title.MenuLayer.Q(name: "obj_btn_campaign");
            float early = campaign.resolvedStyle.opacity;
            yield return new WaitForSecondsRealtime(1.2f);
            Assert.That(title.State.MenuAlpha, Is.EqualTo(1d).Within(1e-9));
            Assert.That(campaign.resolvedStyle.opacity, Is.EqualTo(1f));
            Assert.That(early, Is.LessThan(1f));
            Assert.That(campaign.Q<Label>().text, Is.EqualTo("Campaign"));
            Assert.That(campaign.Query<Label>().ToList().Last().resolvedStyle.unityFontDefinition.fontAsset, Is.SameAs(HareFonts.Get(HareFonts.Score)), "fnt_score");
            var level = title.MenuLayer.Q(name: "obj_btn_level01");
            Assert.That(level.Query<Label>().ToList().Last().resolvedStyle.unityFontDefinition.fontAsset, Is.SameAs(HareFonts.Get(HareFonts.Point)), "fnt_point");
            Assert.That(level.layout.y, Is.EqualTo(250f - 12f).Within(2f), "y - 12 (the panel snaps to device pixels)");
            Assert.That(level.worldBound.center.x - title.Stage.worldBound.x, Is.EqualTo(1440f).Within(1f), "centred on x");
        }

        [UnityTest]
        public IEnumerator HoveringACaptionTurnsItCornflowerBlueAndShowsItsMessage()
        {
            yield return TitleWithMenu();
            var title = Title();
            var played = new List<TitleEvent>();
            title.EventPlayed += played.Add;
            var campaign = Button(title, "obj_btn_campaign");
            yield return MoveTo(title, campaign.X + 100, campaign.Y + 20);
            Assert.That(campaign.Hovered, Is.True);
            Assert.That(played.OfType<TitleSoundRequested>().Select(s => s.Sound), Is.EqualTo(new[] { "sfx_hover" }));
            yield return null;
            var caption = title.MenuLayer.Q(name: "obj_btn_campaign");
            var top = caption.Query<Label>().ToList().Last();
            Assert.That(top.resolvedStyle.color.b, Is.EqualTo(237f / 255f).Within(0.01f), "c_cornflowerblue");
            Assert.That(top.resolvedStyle.color.r, Is.EqualTo(100f / 255f).Within(0.01f));
            var message = title.MenuLayer.Q(name: "obj_btn_campaign-message");
            Assert.That(message.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(message.Query<Label>().ToList().Last().text, Is.EqualTo("Play through campaign"));
            Assert.That(message.layout.y, Is.EqualTo(1080f - 30f), "room_height - 30");

            yield return MoveTo(title, 200, 900);
            Assert.That(campaign.Hovered, Is.False);
            yield return null;
            Assert.That(message.resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(top.resolvedStyle.color, Is.EqualTo(Color.white));
        }

        [UnityTest]
        public IEnumerator CampaignFadesToTheStoryInTheCampaign()
        {
            yield return TitleWithMenu();
            var title = Title();
            var director = RoomFlowDirector.Active;
            yield return ClickAt(title, Button(title, "obj_btn_campaign"));
            Assert.That(director.Session.Mode, Is.EqualTo(GameMode.Campaign));
            Assert.That(director.Session.Level, Is.EqualTo(1));
            Assert.That(director.Flow.Fade.Mode, Is.EqualTo(FadeMode.FadeIn));
            Assert.That(director.Flow.NextRoom, Is.EqualTo(RoomId.Story));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_story", "the fade to room_story");
        }

        [UnityTest]
        public IEnumerator ALevelButtonStartsAQuickGameAtThatLevel()
        {
            yield return TitleWithMenu();
            var title = Title();
            var director = RoomFlowDirector.Active;
            yield return ClickAt(title, Button(title, "obj_btn_level03"));
            Assert.That(director.Session.Mode, Is.EqualTo(GameMode.Quickstart));
            Assert.That(director.Session.Level, Is.EqualTo(3));

            // A second press while the fade runs does nothing: obj_fade_title.mode is not "off".
            yield return ClickAt(title, Button(title, "obj_btn_diff0"));
            Assert.That(director.Session.Mode, Is.EqualTo(GameMode.Quickstart));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_gamescreen", "the fade to room_gamescreen");
        }

        [UnityTest]
        public IEnumerator QuitAsksFirst_NoAndEscCloseTheDialog_YesAndEnterEndTheGame()
        {
            yield return TitleWithMenu();
            var title = Title();
            yield return ClickAt(title, Button(title, "obj_btn_quit"));
            Assert.That(title.State.Dialog, Is.Not.Null);
            Assert.That(title.DialogView, Is.Not.Null);
            Assert.That(title.DialogView.parent, Is.SameAs(title.GuiLayer), "Draw GUI, over the fade");
            Assert.That(title.Pointer.State.OnGui, Is.True, "mouse.on_gui = true");
            var box = title.DialogView.Q(name: "spr_tip_box");
            Assert.That(box.layout.x, Is.EqualTo(686f).Within(2f), "960 - 548/2 (the panel snaps to device pixels)");
            Assert.That(box.layout.y, Is.EqualTo(415f).Within(2f), "540 - 250/2");
            Assert.That(box.layout.width, Is.EqualTo(548f).Within(2f));
            Assert.That(box.layout.height, Is.EqualTo(250f).Within(2f));
            Assert.That(box.resolvedStyle.opacity, Is.EqualTo(0.7f).Within(0.001f));
            Assert.That(title.DialogView.Q(name: "text").Query<Label>().ToList().Last().text, Is.EqualTo("Quit Game"));
            Assert.That(title.DialogView.Q(name: "message").Query<Label>().ToList().Last().text, Is.EqualTo("Are you sure?"));

            yield return ClickAt(title, title.State.Dialog.No);
            Assert.That(title.State.Dialog, Is.Null);
            Assert.That(title.DialogView, Is.Null);
            Assert.That(title.Pointer.State.OnGui, Is.False, "No: mouse.on_gui = false");
            Assert.That(quits, Is.EqualTo(0));

            yield return ClickAt(title, Button(title, "obj_btn_quit"));
            yield return Tap(keyboard.escapeKey);
            Assert.That(title.State.Dialog, Is.Null, "Esc closes it");
            Assert.That(RoomFlowDirector.Active.Flow.Current, Is.EqualTo(RoomId.Title), "and the title has no Esc of its own");

            yield return ClickAt(title, Button(title, "obj_btn_quit"));
            yield return ClickAt(title, title.State.Dialog.Yes);
            Assert.That(quits, Is.EqualTo(1), "Yes: game_end()");

            yield return Tap(keyboard.enterKey);
            Assert.That(quits, Is.EqualTo(2), "Enter: game_end()");
            Assert.That(Title(), Is.SameAs(title), "the seam ends nothing in a test run");
        }

        [UnityTest]
        public IEnumerator TheTitleRendersItsMenuAndDialog()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("needs a graphics device: run the PlayMode tests without -nographics (tools/unity.ps1 -Graphics)");
            const int W = 1920, H = 1080;
            yield return TitleWithMenu();
            var title = Title();
            yield return new WaitForSecondsRealtime(1.2f); // the captions at full alpha
            yield return MoveTo(title, Button(title, "obj_btn_level05").X, Button(title, "obj_btn_level05").Y);
            yield return Render(title, W, H, "title.png", ui => Assert.That(Count(ui, W, H, 1300, 1580, 140, 820, White), Is.GreaterThan(500), "the captions down x 1440"));
            Assert.That(Button(title, "obj_btn_level05").Hovered, Is.True, "the mouse stayed on Level 05 while it was drawn");
            yield return ClickAt(title, Button(title, "obj_btn_quit"));
            yield return MoveTo(title, title.State.Dialog.Yes.X, title.State.Dialog.Yes.Y);
            yield return Render(title, W, H, "title-quit.png", ui => Assert.That(Count(ui, W, H, 686, 1234, 415, 665, p => p.a > 100), Is.GreaterThan(20000), "spr_tip_box at 0.7"));
        }

        static bool White(Color32 p) => p.a > 200 && p.r > 200 && p.g > 200 && p.b > 200;

        static int Count(Color32[] pixels, int w, int h, int left, int right, int top, int bottom, Func<Color32, bool> match)
        {
            int count = 0;
            for (int y = top; y < bottom; y++)
            for (int x = left; x < right; x++)
                if (match(pixels[(h - 1 - y) * w + x])) count++;
            return count;
        }

        /// <summary>Draws the room camera and the title's panel at 1920x1080, saves the composite and checks the panel.</summary>
        IEnumerator Render(TitleRoom title, int w, int h, string file, Action<Color32[]> check)
        {
            // The room camera, drawn at once so no frame runs with it pointed at the texture (the mouse reads through it).
            var camera = Camera.main;
            var world = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            var rect = camera.rect;
            var letterbox = camera.GetComponent<LetterboxCamera>();
            letterbox.enabled = false;
            camera.rect = new Rect(0f, 0f, 1f, 1f);
            camera.targetTexture = world;
            camera.Render();
            var backdrop = Read(world, w, h);
            camera.targetTexture = null;
            camera.rect = rect;
            letterbox.enabled = true;

            var ui = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
            ui.Create();
            var document = title.GetComponentInChildren<UIDocument>();
            var fit = document.GetComponent<HareUiFit>();
            fit.enabled = false;
            var stage = document.rootVisualElement.Q(name: HarePanel.StageName);
            stage.style.left = 0;
            stage.style.top = 0;
            var settings = document.panelSettings;
            var scaleMode = settings.scaleMode;
            settings.targetTexture = ui;
            settings.scaleMode = PanelScaleMode.ConstantPixelSize;
            settings.scale = 1f;
            settings.clearColor = true;
            settings.colorClearValue = Color.clear;
            for (int i = 0; i < 4; i++) yield return null;
            var panel = Read(ui, w, h);
            settings.targetTexture = null;
            settings.scaleMode = scaleMode;
            settings.clearColor = false;
            fit.enabled = true;

            var composed = new Color32[w * h];
            for (int i = 0; i < composed.Length; i++) composed[i] = Color32.Lerp(backdrop[i], new Color32(panel[i].r, panel[i].g, panel[i].b, 255), panel[i].a / 255f);
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.SetPixels32(composed);
            texture.Apply();
            var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Screenshots");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, file), texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
            world.Release();
            ui.Release();
            check(panel);
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
