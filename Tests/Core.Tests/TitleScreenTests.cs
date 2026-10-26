using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using StringAssert = NUnit.Framework.Legacy.StringAssert;
using Unity.Hare.Content;
using Unity.Hare.Flow;
using Unity.Hare.Rooms;

namespace Unity.Hare.Tests.Core
{
    /// <summary>
    /// The title's menu (U.HARE-T26): obj_title's key and click, the buttons obj_logo makes and their captions' fade-in,
    /// each button's mouse enter, leave and left pressed events, obj_yesno_dialog with its Yes, No, Esc and Enter, and the
    /// title section as data.
    /// </summary>
    public class TitleScreenTests
    {
        static readonly PlayerId P1 = new PlayerId(1);

        static TitleScreen Created()
        {
            var screen = new TitleScreen(ContentRegistry.Default.Title, 1920, 1080);
            screen.Execute(new CreateTitle(P1));
            return screen;
        }

        /// <summary>A key, then obj_stand_title's 40-step rise to obj_logo.</summary>
        static TitleScreen WithMenu()
        {
            var screen = Created();
            screen.Execute(new CallTitleMenu(P1));
            for (int i = 0; i < 40; i++) screen.Execute(new StepTitle(P1));
            Assert.IsTrue(screen.MenuShown, "obj_logo after the rise");
            return screen;
        }

        static TitleButton Button(TitleScreen screen, string obj) => screen.Buttons.Single(b => b.Definition.Object == obj);

        static TitleResult Click(TitleScreen screen, string obj, bool fadeIdle = true)
        {
            var b = Button(screen, obj);
            return screen.Execute(new PressTitle(P1, b.X, b.Y, fadeIdle));
        }

        // ---- The data ----

        [Test]
        public void ObjLogoPlacesTheButtonsInItsOrderDownTheRightQuarter()
        {
            var screen = WithMenu();
            CollectionAssert.AreEqual(new[]
            {
                "obj_btn_campaign", "obj_btn_level01", "obj_btn_level02", "obj_btn_level03", "obj_btn_level04", "obj_btn_level05",
                "obj_btn_level06", "obj_btn_level07", "obj_btn_level08", "obj_btn_diff100", "obj_btn_diff0", "obj_btn_quit",
            }, screen.Menu.Select(b => b.Definition.Object));
            Assert.IsTrue(screen.Menu.All(b => b.X == 1440), "buttonX = room_width/2 + room_width/4");
            CollectionAssert.AreEqual(new[] { 150, 250, 300, 350, 400, 450, 500, 550, 600, 650, 700, 800 }, screen.Menu.Select(b => (int)b.Y));
            Assert.IsNull(screen.Buttons.FirstOrDefault(b => b.Definition.Object == "obj_btn_quickstart"), "its instance_create is commented out");
            Assert.IsNotNull(screen.Find("obj_btn_quickstart"), "but it is ported");
        }

        [Test]
        public void TheCaptionsAreTheOriginalsDrawCalls()
        {
            var t = ContentRegistry.Default.Title;
            string Label(string obj) => t.Buttons.Single(b => b.Object == obj).Label;
            Assert.AreEqual("Campaign", Label("obj_btn_campaign"));
            Assert.AreEqual("Quit Game", Label("obj_btn_quit"));
            Assert.AreEqual("Story Mode", Label("obj_btn_quickstart"), "obj_btn_quickstart's Draw says Story Mode");
            Assert.AreEqual("Level 01 - Hydras & Saws", Label("obj_btn_level01"));
            Assert.AreEqual("Boss 1 - Scrap Mental", Label("obj_btn_level08"));
            Assert.AreEqual("Enemy Generator", Label("obj_btn_diff100"));
            Assert.AreEqual("Empty Level", Label("obj_btn_diff0"));
            var big = t.Buttons.Where(b => b.LargeLabel).Select(b => (b.Object, b.LabelOffset)).ToArray();
            CollectionAssert.AreEquivalent(new[] { ("obj_btn_campaign", -30), ("obj_btn_quit", -30), ("obj_btn_quickstart", -30) }, big,
                "fnt_score at y-30; the rest fnt_point at y-12");
            Assert.IsTrue(t.Buttons.Where(b => !b.LargeLabel).All(b => b.LabelOffset == -12));
            Assert.AreEqual(("#FFFFFF", "#6495ED", 2, 30), (t.Color, t.HoverColor, t.Outline, t.MessageBottomMargin), "c_white, c_cornflowerblue");
            Assert.AreEqual(("Yes", "Quit the Game", -24), (t.Dialog.Yes.Label, t.Dialog.Yes.Message, t.Dialog.Yes.LabelOffset));
            Assert.AreEqual(("No", "Return to Main Menu", -24), (t.Dialog.No.Label, t.Dialog.No.Message, t.Dialog.No.LabelOffset));
        }

        [Test]
        public void TheMasksAreTheSpritesBoxesAroundTheirOrigins()
        {
            var t = ContentRegistry.Default.Title;
            var area = t.Buttons.Single(b => b.Object == "obj_btn_campaign").Mask;     // spr_button_area 493x69, origin 246,34
            Assert.AreEqual((-246, -34, 246, 34), (area.Left, area.Top, area.Right, area.Bottom));
            var small = t.Buttons.Single(b => b.Object == "obj_btn_level01").Mask;     // spr_button_area_small 210x35, origin 105,17
            Assert.AreEqual((-105, -17, 104, 17), (small.Left, small.Top, small.Right, small.Bottom));
            var yesno = t.Dialog.Yes.Mask;                                              // spr_yesno_button 145x78, origin 72,39
            Assert.AreEqual((-72, -39, 72, 38), (yesno.Left, yesno.Top, yesno.Right, yesno.Bottom));
        }

        [Test]
        public void TheShippedDataFileMatchesTheCompiledInDefaults()
        {
            var path = System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory,
                "../../../../../Assets/Unity.Hare/Resources/Hare/Content/GameContent.json");
            var loaded = ContentRegistry.FromJson(System.IO.File.ReadAllText(path)).Title;
            var defaults = ContentRegistry.Default.Title;
            object Shape(TitleButtonDefinition b) => (b.Object, b.Label, b.LargeLabel, b.LabelOffset, b.Message, b.X, b.Y, b.Placed, b.MaskSprite,
                (b.Mask.Left, b.Mask.Top, b.Mask.Right, b.Mask.Bottom), b.Action, b.Sound, b.Action == TitleAction.FadeToRoom ? b.Room : default,
                b.OnlyWhenIdle, b.Mode, b.Level, b.Difficulty, b.DialogPurpose, b.DialogText, b.DialogMessage);
            CollectionAssert.AreEqual(defaults.Buttons.Select(Shape), loaded.Buttons.Select(Shape));
            Assert.AreEqual(Shape(defaults.Dialog.Yes), Shape(loaded.Dialog.Yes));
            Assert.AreEqual(Shape(defaults.Dialog.No), Shape(loaded.Dialog.No));
            var d = defaults.Dialog;
            var l = loaded.Dialog;
            Assert.AreEqual((d.Object, d.Sprite, d.Width, d.Height, d.Alpha, d.Margin, d.Outline, d.EscapeSound),
                (l.Object, l.Sprite, l.Width, l.Height, l.Alpha, l.Margin, l.Outline, l.EscapeSound));
            Assert.AreEqual((defaults.MenuFadeSpeed, defaults.HoverSound, defaults.Color, defaults.HoverColor, defaults.Outline, defaults.MessageBottomMargin),
                (loaded.MenuFadeSpeed, loaded.HoverSound, loaded.Color, loaded.HoverColor, loaded.Outline, loaded.MessageBottomMargin));
        }

        [Test]
        public void ABrokenTitleSectionIsRefusedListingEveryProblem()
        {
            var e = Assert.Throws<ContentValidationException>(() => ContentRegistry.FromJson(
                "{ \"title\": { \"Buttons\": [ { \"Object\": \"obj_x\", \"Label\": \"\", \"Message\": \"m\", \"MaskSprite\": \"s\"," +
                " \"Mask\": { \"Left\": 5, \"Right\": 1 }, \"Action\": \"DialogYes\" } ], \"HoverColor\": \"blue\", \"MenuFadeSpeed\": 0 } }"));
            string all = string.Join("\n", e.Errors);
            StringAssert.Contains("title.Buttons[0].Label", all);
            StringAssert.Contains("title.Buttons[0].Mask.Right", all);
            StringAssert.Contains("title.Buttons[0].Action", all);
            StringAssert.Contains("title.HoverColor", all);
            StringAssert.Contains("title.MenuFadeSpeed", all);
        }

        // ---- obj_title and obj_logo ----

        [Test]
        public void CreateMakesThePointerHiddenAndNoMenu()
        {
            var screen = new TitleScreen(ContentRegistry.Default.Title, 1920, 1080);
            Assert.IsFalse(screen.Execute(new CallTitleMenu(P1)).Accepted, "nothing before Create");
            Assert.IsTrue(screen.Execute(new CreateTitle(P1)).Accepted);
            Assert.IsFalse(screen.Execute(new CreateTitle(P1)).Accepted, "Create runs once");
            Assert.IsFalse(screen.PointerVisible, "obj_title: mouse.visible = false");
            Assert.IsFalse(screen.MenuShown);
            Assert.IsEmpty(screen.Menu);
        }

        [Test]
        public void AKeyOrClickRisesTheRabbitAndTheMenuComesWithTheLogoFortyStepsLater()
        {
            var screen = Created();
            var result = screen.Execute(new CallTitleMenu(P1));
            Assert.IsTrue(result.Accepted);
            CollectionAssert.AreEqual(new[] { typeof(TitleDropRemoved), typeof(MenuCalled) }, result.Events.Select(e => e.GetType()),
                "with(obj_drop_title) instance_destroy(); ... obj_stand_title.phase = \"on\"");
            Assert.IsNull(screen.Scene.Drop);
            Assert.IsFalse(screen.Scene.EmitterAlive, "with(obj_drop_emitter) instance_destroy()");
            Assert.IsTrue(screen.Stand.On);
            Assert.IsFalse(screen.MenuShown, "no menu until the rabbit has risen");
            Assert.IsFalse(screen.PointerVisible);
            Assert.IsFalse(screen.Execute(new CallTitleMenu(P1)).Accepted, "a key while it rises changes nothing");

            for (int i = 1; i < 40; i++)
            {
                Assert.IsFalse(screen.Execute(new StepTitle(P1)).Events.OfType<MenuShown>().Any(), "step " + i);
                Assert.AreEqual(2592 - 40 * i, screen.Stand.Y, "vspeed = -40 while y > room_height - 30");
            }
            Assert.AreEqual(1032d, screen.Stand.Y);
            var logo = screen.Execute(new StepTitle(P1));
            Assert.AreEqual(1, logo.Events.OfType<MenuShown>().Count(), "the 40th step makes obj_logo");
            Assert.AreEqual(1032d, screen.Stand.Y, "and stops it within 30 px of the bottom");
            Assert.IsFalse(screen.Stand.On, "phase = \"off\"");
            Assert.IsTrue(screen.PointerVisible, "obj_logo: obj_mouse.visible = true");
            Assert.AreEqual(0d, screen.MenuAlpha, "obj_logo: image_alpha = 0, not stepped in the step that makes it");
            Assert.AreEqual(12, screen.Menu.Count);
            Assert.AreEqual((475.5, 455d), (screen.LogoX, screen.LogoY), "width/2 + 200, height/2 + 200");
            Assert.AreEqual(0, screen.Scene.Backgrounds[0].VSpeed, "background_vspeed[0] = 0");

            Assert.IsFalse(screen.Execute(new CallTitleMenu(P1)).Accepted, "a second key brings no second menu (BIBLE D23)");
            Assert.IsFalse(screen.Execute(new ShowTitleMenu(P1)).Accepted);
            for (int i = 0; i < 5; i++) screen.Execute(new StepTitle(P1));
            Assert.AreEqual(12, screen.Menu.Count);
            Assert.IsFalse(screen.Stand.On);
        }

        [Test]
        public void TheCaptionsFadeInTwoHundredthsAStepAndHoldAtOne()
        {
            var screen = WithMenu();
            screen.Execute(new StepTitle(P1));
            Assert.AreEqual(0.02, screen.MenuAlpha, 1e-12);
            for (int i = 0; i < 49; i++) screen.Execute(new StepTitle(P1));
            Assert.AreEqual(1d, screen.MenuAlpha, 1e-9, "50 steps to opaque");
            for (int i = 0; i < 3; i++) screen.Execute(new StepTitle(P1));
            Assert.AreEqual(1d, screen.MenuAlpha, "then image_alpha = 1");
        }

        [Test]
        public void StepsBeforeTheMenuLeaveTheAlphaAtZero()
        {
            var screen = Created();
            Assert.IsTrue(screen.Execute(new StepTitle(P1)).Accepted);
            Assert.AreEqual(0d, screen.MenuAlpha);
        }

        // ---- Mouse enter and leave ----

        [Test]
        public void EnteringAButtonPlaysSfxHoverAndLeavingItDoesNot()
        {
            var screen = WithMenu();
            var campaign = Button(screen, "obj_btn_campaign");
            var enter = screen.Execute(new PointTitle(P1, 1440 - 246, 150 + 34)); // the mask's inclusive corner
            Assert.AreEqual("sfx_hover", ((TitleSoundRequested)enter.Events[0]).Sound);
            var changed = (TitleHoverChanged)enter.Events[1];
            Assert.AreSame(campaign, changed.Button);
            Assert.IsTrue(changed.Hovered);
            Assert.IsTrue(campaign.Hovered);

            Assert.IsEmpty(screen.Execute(new PointTitle(P1, 1440, 150)).Events, "still over it: no second enter");
            var leave = screen.Execute(new PointTitle(P1, 1440 + 247, 150));
            Assert.AreEqual(1, leave.Events.Count);
            Assert.IsFalse(((TitleHoverChanged)leave.Events[0]).Hovered);
            Assert.IsFalse(campaign.Hovered);
        }

        [Test]
        public void MovingFromOneLevelButtonToTheNextLeavesOneAndEntersTheOther()
        {
            var screen = WithMenu();
            screen.Execute(new PointTitle(P1, 1440, 250));
            var result = screen.Execute(new PointTitle(P1, 1440, 300));
            var changes = result.Events.OfType<TitleHoverChanged>().Select(c => (c.Button.Definition.Object, c.Hovered)).ToArray();
            CollectionAssert.AreEqual(new[] { ("obj_btn_level01", false), ("obj_btn_level02", true) }, changes);
            Assert.AreEqual(1, result.Events.OfType<TitleSoundRequested>().Count());
        }

        // ---- Left pressed ----

        [Test]
        public void CampaignPlaysSfxRoitSetsTheCampaignAndFadesToTheStory()
        {
            var screen = WithMenu();
            var result = Click(screen, "obj_btn_campaign");
            Assert.IsTrue(result.Accepted);
            Assert.AreEqual("sfx_roit", ((TitleSoundRequested)result.Events[0]).Sound);
            Assert.AreEqual(TitleMode.Campaign, ((TitleModeChosen)result.Events[1]).Mode);
            var fade = (TitleFadeRequested)result.Events[2];
            Assert.AreEqual((RoomId.Story, true), (fade.Room, fade.OnlyWhenIdle));
        }

        [Test]
        public void ALevelButtonStartsAQuickGameAtItsLevelInTheArena()
        {
            var screen = WithMenu();
            for (int level = 1; level <= 8; level++)
            {
                var result = Click(screen, "obj_btn_level0" + level);
                var mode = result.Events.OfType<TitleModeChosen>().Single();
                Assert.AreEqual((TitleMode.Quickstart, level), (mode.Mode, mode.Level));
                Assert.AreEqual(RoomId.GameScreen, result.Events.OfType<TitleFadeRequested>().Single().Room);
                Assert.AreEqual("sfx_roit", result.Events.OfType<TitleSoundRequested>().Single().Sound);
            }
        }

        [Test]
        public void TheDifficultyButtonsStartTheGeneratorAtTheirBudget()
        {
            var screen = WithMenu();
            var hundred = Click(screen, "obj_btn_diff100").Events.OfType<TitleModeChosen>().Single();
            Assert.AreEqual((TitleMode.Generator, 100), (hundred.Mode, hundred.Difficulty));
            var zero = Click(screen, "obj_btn_diff0").Events.OfType<TitleModeChosen>().Single();
            Assert.AreEqual((TitleMode.Generator, 0), (zero.Mode, zero.Difficulty));
        }

        [Test]
        public void WhileTheFadeRunsTheModeButtonsDoNothingAtAll()
        {
            var screen = WithMenu();
            foreach (var obj in new[] { "obj_btn_campaign", "obj_btn_level03", "obj_btn_diff0" })
            {
                var result = Click(screen, obj, fadeIdle: false);
                Assert.IsTrue(result.Accepted, "the button was hit");
                Assert.IsEmpty(result.Events, obj + ": if(obj_fade_title.mode == \"off\") wraps even the sound");
            }
        }

        [Test]
        public void ObjBtnQuickstartGoesToTheStoryWithSfxClickAndNoChecks()
        {
            var t = ContentRegistry.Default.Title;
            var quick = t.Buttons.Single(b => b.Object == "obj_btn_quickstart");
            quick.Placed = true;
            quick.X = 960;
            quick.Y = 540;
            var screen = new TitleScreen(t, 1920, 1080);
            screen.Execute(new CreateTitle(P1));
            screen.Execute(new ShowTitleMenu(P1));
            var result = Click(screen, "obj_btn_quickstart", fadeIdle: false);
            Assert.AreEqual("sfx_click", ((TitleSoundRequested)result.Events[0]).Sound);
            Assert.IsEmpty(result.Events.OfType<TitleModeChosen>(), "it sets no game mode");
            var fade = (TitleFadeRequested)result.Events[1];
            Assert.AreEqual((RoomId.Story, false), (fade.Room, fade.OnlyWhenIdle), "next_room = room_story, whatever the fade is doing");
        }

        [Test]
        public void APressOffEveryButtonIsRefused()
        {
            var screen = WithMenu();
            var result = screen.Execute(new PressTitle(P1, 200, 200, true));
            Assert.IsFalse(result.Accepted);
            Assert.IsEmpty(result.Events);
        }

        // ---- The quit dialog ----

        [Test]
        public void QuitOpensTheDialogInTheViewsMiddleWithThePointerOnTheGui()
        {
            var screen = WithMenu();
            var result = Click(screen, "obj_btn_quit");
            Assert.IsEmpty(result.Events.OfType<TitleSoundRequested>(), "obj_btn_quit plays nothing");
            var dialog = result.Events.OfType<TitleDialogOpened>().Single().Dialog;
            Assert.AreSame(dialog, screen.Dialog);
            Assert.AreEqual((TitleDialogPurpose.QuitGame, "Quit Game", "Are you sure?"), (dialog.Purpose, dialog.Text, dialog.Message));
            Assert.AreEqual((686d, 415d), (dialog.X, dialog.Y), "960 - 548/2, 540 - 250/2");
            Assert.AreEqual((686d + 137d, 415d + 187.5d), (dialog.Yes.X, dialog.Yes.Y), "x + width/4, y + height/4*3");
            Assert.AreEqual((686d + 411d, 415d + 187.5d), (dialog.No.X, dialog.No.Y), "x + width/4*3");
            Assert.IsTrue(result.Events.OfType<TitlePointerOnGuiChanged>().Single().OnGui, "mouse.on_gui = true");
            Assert.IsTrue(screen.PointerOnGui);
            CollectionAssert.AreEqual(new[] { "obj_btn_yes", "obj_btn_no" }, screen.Buttons.Skip(12).Select(b => b.Definition.Object));

            Assert.IsEmpty(Click(screen, "obj_btn_quit").Events, "if(!instance_exists(obj_yesno_dialog))");
        }

        [Test]
        public void YesPlaysSfxRoitAndEndsTheGame()
        {
            var screen = WithMenu();
            Click(screen, "obj_btn_quit");
            var result = Click(screen, "obj_btn_yes");
            Assert.AreEqual("sfx_roit", ((TitleSoundRequested)result.Events[0]).Sound);
            Assert.IsInstanceOf<QuitRequested>(result.Events[1], "game_end()");
        }

        [Test]
        public void NoPlaysSfxRoitClosesTheDialogAndTakesThePointerOffTheGui()
        {
            var screen = WithMenu();
            Click(screen, "obj_btn_quit");
            var result = Click(screen, "obj_btn_no");
            Assert.AreEqual("sfx_roit", ((TitleSoundRequested)result.Events[0]).Sound);
            Assert.IsInstanceOf<TitleDialogClosed>(result.Events[1]);
            Assert.IsFalse(((TitlePointerOnGuiChanged)result.Events[2]).OnGui);
            Assert.IsNull(screen.Dialog);
            Assert.IsFalse(screen.PointerOnGui);
            Assert.AreEqual(12, screen.Buttons.Count(), "the Yes and No went with it");
            Assert.IsNotNull(Click(screen, "obj_btn_quit").Events.OfType<TitleDialogOpened>().SingleOrDefault(), "quit can open it again");
        }

        [Test]
        public void EscClosesTheDialogWithSfxRoitAndEnterEndsTheGame()
        {
            var screen = WithMenu();
            Assert.IsFalse(screen.Execute(new EscapeTitleDialog(P1)).Accepted, "no dialog, no Esc event");
            Assert.IsFalse(screen.Execute(new ConfirmTitleDialog(P1)).Accepted);

            Click(screen, "obj_btn_quit");
            var esc = screen.Execute(new EscapeTitleDialog(P1));
            Assert.AreEqual("sfx_roit", ((TitleSoundRequested)esc.Events[0]).Sound);
            Assert.IsInstanceOf<TitleDialogClosed>(esc.Events[1]);
            Assert.AreEqual(2, esc.Events.Count, "Esc leaves mouse.on_gui as it was");
            Assert.IsNull(screen.Dialog);

            Click(screen, "obj_btn_quit");
            var enter = screen.Execute(new ConfirmTitleDialog(P1));
            Assert.IsInstanceOf<QuitRequested>(enter.Events.Single(), "Enter: game_end()");
        }

        [Test]
        public void TheDialogsButtonsHoverAndTheMenuStillAnswersBehindIt()
        {
            var screen = WithMenu();
            Click(screen, "obj_btn_quit");
            var yes = screen.Dialog.Yes;
            var hover = screen.Execute(new PointTitle(P1, yes.X, yes.Y));
            Assert.AreSame(yes, hover.Events.OfType<TitleHoverChanged>().Single().Button);
            Assert.IsNotEmpty(Click(screen, "obj_btn_level01").Events.OfType<TitleFadeRequested>().ToArray(),
                "GameMaker sends mouse events to every instance, whatever is drawn over it");
        }
    }
}
