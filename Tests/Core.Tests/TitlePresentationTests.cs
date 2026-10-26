using System.Collections.Generic;
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
    /// The title and intro presentation (U.HARE-T62): obj_drop_title and obj_drop_intro falling in with their smoke,
    /// obj_drop_emitter's clouds, bg_nightsky scrolling under bg_gradient3, obj_stand_title's rise to obj_logo (from a key
    /// or from the drop falling out of the room), the stand's eye following the mouse, room_intro's A and D, tip, key and
    /// way out, and both sections as data.
    /// </summary>
    public class TitlePresentationTests
    {
        static readonly PlayerId P1 = new PlayerId(1);

        /// <summary>Deals the queued values, then the middle of each range (so the drop's shake is 0).</summary>
        sealed class ScriptedRandom : IRandomSource
        {
            readonly Queue<int> next = new Queue<int>();
            public readonly List<(int Min, int Max)> Asked = new List<(int, int)>();
            public void Queue(params int[] values) { foreach (var v in values) next.Enqueue(v); }
            public int IRandom(int min, int max)
            {
                Asked.Add((min, max));
                return next.Count > 0 ? next.Dequeue() : (min + max) / 2;
            }
            public float Range(float min, float max) => min;
        }

        static TitleScreen Title(ScriptedRandom random = null)
        {
            var screen = new TitleScreen(ContentRegistry.Default.Title, 1920, 1080, random ?? new ScriptedRandom());
            screen.Execute(new CreateTitle(P1));
            return screen;
        }

        static IntroScreen Intro(ScriptedRandom random = null)
        {
            var screen = new IntroScreen(ContentRegistry.Default.Intro, 1920, 1080, random ?? new ScriptedRandom());
            screen.Execute(new CreateIntro(P1));
            return screen;
        }

        // ---- The room as placed ----

        [Test]
        public void TheTitleComesUpWithTheDropAboveTheRoomAndTheRabbitBelowIt()
        {
            var screen = Title();
            var drop = screen.Scene.Drop;
            Assert.AreEqual((960d, -608d, 960d, 10d, DropPhase.Start), (drop.X, drop.Y, drop.StartX, drop.VSpeed, drop.Phase), "room_title: obj_drop_title at 960,-608; vspeed = 10");
            Assert.IsTrue(screen.Scene.EmitterAlive);
            Assert.IsEmpty(screen.Scene.Clouds);
            Assert.AreEqual((928d, 2592d, false), (screen.Stand.X, screen.Stand.Y, screen.Stand.On), "obj_stand_title at 928,2592, phase = \"off\"");
            CollectionAssert.AreEqual(new[] { ("bg_nightsky", -5), ("bg_gradient3", 0) }, screen.Scene.Backgrounds.Select(b => (b.Name, b.VSpeed)),
                "bg_gradient3 drawn over bg_nightsky, which scrolls up 5 px a step");
            Assert.IsFalse(screen.MenuShown);
        }

        [Test]
        public void TheDropFallsTenAStepShakingAndStopsAQuarterOfTheWayDown()
        {
            var random = new ScriptedRandom();
            var screen = Title(random);
            var trail = new List<TitleSmokePuffed>();
            var puffs = new List<(int Step, TitleSmokePuffed Puff)>();
            for (int step = 1; step <= 89; step++)
            {
                var events = screen.Execute(new StepTitle(P1)).Events.OfType<TitleSmokePuffed>().ToList();
                trail.AddRange(events.Where(e => e.Smoke == DropSmoke.Trail));
                puffs.AddRange(events.Where(e => e.Smoke == DropSmoke.Puff).Select(e => (step, e)));
                if (step < 89) Assert.AreEqual(DropPhase.Start, screen.Scene.Drop.Phase, "step " + step);
            }
            var drop = screen.Scene.Drop;
            Assert.AreEqual(DropPhase.Hang, drop.Phase, "y > room_height/4 at the 89th step");
            Assert.AreEqual(272d, drop.Y, "-608 + 88 * 10");
            Assert.AreEqual(0d, drop.VSpeed);
            Assert.AreEqual(200, drop.HangAlarm, "obj_drop_title: alarm[1] = 200");
            Assert.AreEqual(89, trail.Count, "ef_smokeup every step");
            Assert.IsTrue(trail.All(t => t.Color == "#FFA040" && t.Size == 2), "c_orange, size 2");
            Assert.AreEqual(-608d + 40d, trail[0].Y, "at y + 40");
            CollectionAssert.AreEqual(new[] { 20, 40, 60, 80 }, puffs.Select(p => p.Step), "Alarm 0 every smoke_delay = 20 steps");
            Assert.IsTrue(puffs.All(p => p.Puff.Color == "#000000"), "c_black");
            Assert.IsTrue(random.Asked.Contains((-3, 3)), "x = startX + irandom_range(-3,3)");
        }

        [Test]
        public void TheDropShakesAboutStartX()
        {
            var random = new ScriptedRandom();
            var screen = Title(random);
            random.Queue(4, -425, 60, 1, 1, 1, -425, 60, 1, 1, 1, -425, 60, 1, 1, 1, -425, 60, 1, 1, 1); // step 1's clouds
            random.Queue(3);
            screen.Execute(new StepTitle(P1));
            Assert.AreEqual(963d, screen.Scene.Drop.X);
            random.Queue(-3);
            screen.Execute(new StepTitle(P1));
            Assert.AreEqual(957d, screen.Scene.Drop.X);
            Assert.AreEqual(960d, screen.Scene.Drop.StartX);
        }

        [Test]
        public void TheEmitterMakesFourToEightCloudsEveryTwentyStepsThatRushUpAndGo()
        {
            var random = new ScriptedRandom();
            var screen = Title(random);
            // Alarm 0 at step 1: irandom_range(4,8) clouds, each: x, vspeed, alpha, the y multiple, randomasset.
            random.Queue(5);
            random.Queue(-425, 60, 1, 1, 1);
            random.Queue(1494, 80, 3, 2, 4);
            random.Queue(0, 70, 2, 1, 2);
            random.Queue(100, 61, 1, 2, 3);
            random.Queue(200, 79, 3, 1, 1);
            screen.Execute(new StepTitle(P1));
            CollectionAssert.AreEqual(new[] { (4, 8), (-425, 1494), (60, 80), (1, 3), (1, 2), (1, 4) }, random.Asked.Take(6),
                "irandom_range(4,8), then x within -sprite_width/2 .. room_width - sprite_width/2 (spr_cloud1's 851, truncated)");
            var clouds = screen.Scene.Clouds;
            Assert.AreEqual(5, clouds.Count);
            var first = clouds[0];
            Assert.AreEqual(("spr_cloud1", -425d, -60d, 0.1), (first.Sprite, first.X, first.VSpeed, first.Alpha));
            Assert.AreEqual(1080d + 313d - 60d, first.Y, "room_height + sprite_height * 1, then the step's motion");
            var second = clouds[1];
            Assert.AreEqual(("spr_cloud4", 1494d, -80d), (second.Sprite, second.X, second.VSpeed));
            Assert.AreEqual(0.3, second.Alpha, 1e-12);
            Assert.AreEqual(1080d + 626d - 80d, second.Y);

            for (int step = 2; step <= 20; step++) screen.Execute(new StepTitle(P1));
            Assert.AreEqual(5, screen.Scene.Clouds.Count);
            screen.Execute(new StepTitle(P1));
            Assert.AreEqual(5 + 6, screen.Scene.Clouds.Count, "rate = 20: the next batch on step 21");

            // The second cloud, from 1706 at 80 a step, is above -313 at the start of step 27's Step: gone then.
            for (int step = 22; step <= 26; step++) screen.Execute(new StepTitle(P1));
            CollectionAssert.Contains(screen.Scene.Clouds.ToList(), second);
            screen.Execute(new StepTitle(P1));
            CollectionAssert.DoesNotContain(screen.Scene.Clouds.ToList(), second, "if(y < -sprite_height) instance_destroy()");
            CollectionAssert.Contains(screen.Scene.Clouds.ToList(), first, "the slower one still rising");
        }

        [Test]
        public void AKeyDestroysTheDropAndEmitterButTheCloudsFlyOn()
        {
            var screen = Title();
            for (int i = 0; i < 3; i++) screen.Execute(new StepTitle(P1));
            int clouds = screen.Scene.Clouds.Count;
            Assert.Greater(clouds, 0);
            screen.Execute(new CallTitleMenu(P1));
            Assert.IsNull(screen.Scene.Drop);
            Assert.IsFalse(screen.Scene.EmitterAlive);
            var cloud = screen.Scene.Clouds[0];
            double y = cloud.Y;
            var events = screen.Execute(new StepTitle(P1)).Events;
            Assert.IsEmpty(events.OfType<TitleSmokePuffed>(), "no drop, no smoke");
            Assert.AreEqual(y + cloud.VSpeed, cloud.Y, "the clouds already made fly on");
            for (int i = 0; i < 60; i++) screen.Execute(new StepTitle(P1));
            Assert.IsEmpty(screen.Scene.Clouds, "and none come after them");
        }

        [Test]
        public void LeftAloneTheDropHangsFallsOutWithSfxLandAndTheRabbitRisesToTheMenu()
        {
            var screen = Title();
            var all = new List<(int Step, TitleEvent Event)>();
            for (int step = 1; step <= 420; step++)
                foreach (var e in screen.Execute(new StepTitle(P1)).Events) all.Add((step, e));

            int left = all.Single(e => e.Event is TitleDropRemoved).Step;
            Assert.AreEqual(370, left, "stops at step 89, Alarm 1 fires 200 steps later, then 81 steps from y 272 to past 1080");
            Assert.AreEqual(left, all.Single(e => e.Event is TitleSoundRequested s && s.Sound == "sfx_land").Step, "audio_play_sound(sfx_land,1,0)");
            Assert.AreEqual(left, all.Single(e => e.Event is MenuCalled).Step, "obj_stand_title.phase = \"on\"");
            Assert.AreEqual(left + 39, all.Single(e => e.Event is MenuShown).Step, "the rise, then obj_logo");
            Assert.IsNull(screen.Scene.Drop);
            Assert.IsFalse(screen.Scene.EmitterAlive, "with(obj_drop_emitter) instance_destroy()");
            Assert.IsTrue(screen.MenuShown);
            Assert.AreEqual(1032d, screen.Stand.Y);
            Assert.IsFalse(screen.Execute(new CallTitleMenu(P1)).Accepted, "a key after that brings no second menu (BIBLE D23)");
        }

        [Test]
        public void ThePhaseTurnsToExitOnAlarmOne()
        {
            var screen = Title();
            for (int step = 1; step <= 288; step++) screen.Execute(new StepTitle(P1));
            Assert.AreEqual(DropPhase.Hang, screen.Scene.Drop.Phase);
            Assert.AreEqual(1, screen.Scene.Drop.HangAlarm);
            screen.Execute(new StepTitle(P1));
            Assert.AreEqual(DropPhase.Exit, screen.Scene.Drop.Phase, "Alarm 1: phase = \"exit\"");
            Assert.AreEqual(282d, screen.Scene.Drop.Y, "vspeed = 10 again");
        }

        [Test]
        public void TheNightSkyScrollsUpUntilTheLogoStopsIt()
        {
            var screen = Title();
            for (int i = 0; i < 10; i++) screen.Execute(new StepTitle(P1));
            Assert.AreEqual(-50, screen.Scene.Backgrounds[0].Y, "background_vspeed[0] = -5");
            Assert.AreEqual(0, screen.Scene.Backgrounds[1].Y, "bg_gradient3 still");
            screen.Execute(new CallTitleMenu(P1));
            for (int i = 0; i < 40; i++) screen.Execute(new StepTitle(P1));
            Assert.IsTrue(screen.MenuShown);
            int y = screen.Scene.Backgrounds[0].Y;
            for (int i = 0; i < 10; i++) screen.Execute(new StepTitle(P1));
            Assert.AreEqual(y, screen.Scene.Backgrounds[0].Y, "obj_logo: background_vspeed[0] = 0");
        }

        [Test]
        public void TheStandsMoonAndEyeFollowItAndThePupilFollowsTheMouse()
        {
            var screen = Title();
            screen.Execute(new CallTitleMenu(P1));
            for (int i = 0; i < 40; i++) screen.Execute(new StepTitle(P1));
            var stand = screen.Stand;
            Assert.AreEqual((0d, 1282d), (stand.MoonX, stand.MoonY), "draw_sprite(spr_moon_surface, 0, 0, y+250)");
            Assert.AreEqual((922d, 664d), (stand.EyeX, stand.EyeY), "spr_eyeback at x-6, y-368");
            stand.PupilAt(0, 0, out double x0, out double y0);
            Assert.AreEqual((916d, 662d), (x0, y0), "spr_pupil at x-12, y-370 with the mouse at 0,0");
            stand.PupilAt(1920, 1080, out double x1, out double y1);
            Assert.AreEqual((932d, 680d), (x1, y1), "+ mouse_x/120, + mouse_y/60");
        }

        // ---- room_intro ----

        [Test]
        public void TheIntroDropStopsWithTheControlsTipAndHangsUntilAKey()
        {
            var screen = Intro();
            var tips = new List<int>();
            for (int step = 1; step <= 1000; step++)
            {
                var events = screen.Execute(new StepIntro(P1)).Events;
                if (events.OfType<IntroTipRequested>().Any()) tips.Add(step);
                Assert.IsEmpty(events.OfType<IntroRoomRequested>(), "no Alarm 1: it never leaves by itself");
            }
            CollectionAssert.AreEqual(new[] { 89 }, tips, "tip = instance_create(0,0,obj_tip) as it stops, once");
            Assert.IsTrue(screen.TipShown);
            Assert.AreEqual((DropPhase.Hang, 272d, -1), (screen.Scene.Drop.Phase, screen.Scene.Drop.Y, screen.Scene.Drop.HangAlarm));
        }

        [Test]
        public void AAndDMoveTheLineItShakesAboutWithinThreeHundredOfTheEdges()
        {
            var screen = Intro();
            screen.Execute(new StepIntro(P1, right: true));
            Assert.AreEqual(961d, screen.Scene.Drop.StartX, "D: startX += 1");
            screen.Execute(new StepIntro(P1, left: true));
            screen.Execute(new StepIntro(P1, left: true));
            Assert.AreEqual(959d, screen.Scene.Drop.StartX, "A: startX -= 1");
            screen.Execute(new StepIntro(P1, left: true, right: true));
            Assert.AreEqual(959d, screen.Scene.Drop.StartX, "both held cancel out");
            for (int i = 0; i < 1000; i++) screen.Execute(new StepIntro(P1, right: true));
            Assert.AreEqual(1620d, screen.Scene.Drop.StartX, "if(startX < room_width - 300)");
            for (int i = 0; i < 2000; i++) screen.Execute(new StepIntro(P1, left: true));
            Assert.AreEqual(300d, screen.Scene.Drop.StartX, "if(startX > 300)");
            Assert.AreEqual(300d, screen.Scene.Drop.X, "the shake is about startX");
        }

        [Test]
        public void AnyKeyButAOrDSendsTheHangingRabbitIntoAQuickGameAtLevelOne()
        {
            var screen = Intro();
            Assert.IsFalse(screen.Execute(new PressIntroKey(P1)).Accepted, "while it falls in, a key does nothing");
            for (int i = 0; i < 89; i++) screen.Execute(new StepIntro(P1));
            Assert.IsFalse(screen.Execute(new PressIntroKey(P1, aHeld: true)).Accepted, "if (!keyboard_check(ord(\"A\")) ...");
            Assert.IsFalse(screen.Execute(new PressIntroKey(P1, dHeld: true)).Accepted, "... && !keyboard_check(ord(\"D\")))");
            var press = screen.Execute(new PressIntroKey(P1));
            Assert.IsTrue(press.Accepted);
            Assert.IsInstanceOf<IntroDropReleased>(press.Events.Single());
            Assert.AreEqual(DropPhase.Exit, screen.Scene.Drop.Phase);
            Assert.IsFalse(screen.Execute(new PressIntroKey(P1)).Accepted, "only a hanging rabbit");

            var all = new List<(int Step, IntroEvent Event)>();
            for (int step = 1; step <= 120; step++)
                foreach (var e in screen.Execute(new StepIntro(P1)).Events) all.Add((step, e));
            var mode = all.Single(e => e.Event is IntroModeChosen);
            Assert.AreEqual(1, ((IntroModeChosen)mode.Event).Level, "gamemode = gm_quickstart; current_scenario = -1; current_level = 1");
            var room = all.Single(e => e.Event is IntroRoomRequested);
            Assert.AreEqual(RoomId.GameScreen, ((IntroRoomRequested)room.Event).Room, "room_goto(room_gamescreen)");
            Assert.AreEqual(mode.Step, room.Step);
            Assert.AreEqual(82, room.Step, "from y 272, 10 a step, until y > room_height");
            Assert.IsTrue(screen.Left);
            Assert.IsFalse(all.Any(e => e.Event is IntroTipRequested), "one tip only");
        }

        // ---- The data ----

        [Test]
        public void TheShippedDataFileMatchesTheCompiledInDefaults()
        {
            var path = System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory,
                "../../../../../Assets/Unity.Hare/Resources/Hare/Content/GameContent.json");
            var loaded = ContentRegistry.FromJson(System.IO.File.ReadAllText(path));
            var defaults = ContentRegistry.Default;
            object Scene(DropSceneTuning s) => (s.DropSprite, s.DropX, s.DropY, s.DropScale, s.FallSpeed, s.Jitter, s.HangFraction, s.HangSteps,
                s.SmokeOffsetY, s.SmokeSize, s.SmokeDelaySteps, s.TrailColor, s.PuffColor, s.LandSound,
                (s.CloudFirstAlarmSteps, s.CloudRateSteps, s.CloudBatchMin, s.CloudBatchMax, string.Join(",", s.CloudSprites), s.CloudWidth, s.CloudHeight),
                (s.CloudSpeedMin, s.CloudSpeedMax, s.CloudAlphaStep, s.CloudAlphaMin, s.CloudAlphaMax, s.CloudBelowMin, s.CloudBelowMax),
                string.Join(",", s.Backgrounds.Select(b => b.Name + ":" + b.HSpeed + ":" + b.VSpeed)));
            Assert.AreEqual(Scene(defaults.Title.Scene), Scene(loaded.Title.Scene));
            Assert.AreEqual(Scene(defaults.Intro.Scene), Scene(loaded.Intro.Scene));
            object Stand(StandTuning s) => (s.Sprite, s.X, s.Y, s.Scale, s.RiseSpeed, s.StopAboveBottom, s.MoonSprite, s.MoonX, s.MoonOffsetY,
                (s.EyeSprite, s.EyeOffsetX, s.EyeOffsetY, s.EyeScale), (s.PupilSprite, s.PupilOffsetX, s.PupilOffsetY, s.PupilMouseXDivisor, s.PupilMouseYDivisor));
            Assert.AreEqual(Stand(defaults.Title.Stand), Stand(loaded.Title.Stand));
            var dl = defaults.Title.Logo;
            var ll = loaded.Title.Logo;
            Assert.AreEqual((dl.Sprite, dl.Width, dl.Height, dl.Margin, dl.StopsBackground), (ll.Sprite, ll.Width, ll.Height, ll.Margin, ll.StopsBackground));
            var di = defaults.Intro;
            var li = loaded.Intro;
            Assert.AreEqual((di.SteerMargin, di.SteerStep, di.ExitLevel, di.ExitRoom), (li.SteerMargin, li.SteerStep, li.ExitLevel, li.ExitRoom));
            Assert.AreEqual((200, "sfx_land"), (defaults.Title.Scene.HangSteps, defaults.Title.Scene.LandSound), "obj_drop_title's Alarm 1 and sfx_land");
            Assert.AreEqual((0, ""), (defaults.Intro.Scene.HangSteps, defaults.Intro.Scene.LandSound), "obj_drop_intro has neither");
        }

        [Test]
        public void BrokenTitleAndIntroPresentationIsRefusedListingEveryProblem()
        {
            var e = Assert.Throws<ContentValidationException>(() => ContentRegistry.FromJson(
                "{ \"title\": { \"Scene\": { \"FallSpeed\": 0, \"TrailColor\": \"orange\", \"CloudBatchMax\": 1 }, \"Stand\": { \"RiseSpeed\": 0 }, \"Logo\": { \"Sprite\": \"\" } }," +
                "  \"intro\": { \"SteerStep\": 0, \"ExitLevel\": 0, \"Scene\": { \"HangFraction\": 2 } } }"));
            string all = string.Join("\n", e.Errors);
            StringAssert.Contains("title.Scene.FallSpeed", all);
            StringAssert.Contains("title.Scene.TrailColor", all);
            StringAssert.Contains("title.Scene.CloudBatchMax", all);
            StringAssert.Contains("title.Stand.RiseSpeed", all);
            StringAssert.Contains("title.Logo.Sprite", all);
            StringAssert.Contains("intro.SteerStep", all);
            StringAssert.Contains("intro.ExitLevel", all);
            StringAssert.Contains("intro.Scene.HangFraction", all);
        }
    }
}
