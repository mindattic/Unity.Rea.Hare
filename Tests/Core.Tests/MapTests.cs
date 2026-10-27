using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using StringAssert = NUnit.Framework.Legacy.StringAssert;
using Unity.Hare.Art;
using Unity.Hare.Content;
using Unity.Hare.Flow;
using Unity.Hare.Map;
using Unity.Hare.Rooms;
using Unity.Hare.Session;
using Unity.Hare.Story;

namespace Unity.Hare.Tests.Core
{
    /// <summary>
    /// room_map (U.HARE-T19): obj_map, obj_earthball, obj_destination's pixel-exact touch of the mission markers,
    /// obj_white_dot, obj_scenario_details and obj_btn_upgrade, with the owner's ten missions, the kept launch and the
    /// boss's lock (docs/BIBLE.md V42), and the won missions' save.
    /// </summary>
    public class MapTests
    {
        static readonly PlayerId P1 = new PlayerId(1);

        static ContentRegistry Content => ContentRegistry.Default;

        static MapScreen Map(IEnumerable<int> won = null, ConversationBox box = null)
        {
            var content = Content;
            var screen = new MapScreen(content.Map, content.Popups.Alert, box, won, level => content.Arena.SettingFor(level)?.Name, 1920, 1080);
            Assert.IsTrue(screen.Execute(new CreateMap(P1)).Accepted);
            return screen;
        }

        static ConversationBox Box()
        {
            var content = Content;
            string script = content.Story.Conversation.MapScript;
            return new ConversationBox(content.Story.Conversation, content.Dialogue.Speaker(script), content.Dialogue.Script(script), 1920, 1080);
        }

        static List<MapEvent> Step(MapScreen screen, int steps)
        {
            var all = new List<MapEvent>();
            for (int i = 0; i < steps; i++) all.AddRange(screen.Execute(new StepMap(P1)).Events);
            return all;
        }

        /// <summary>Steps until a mission is reached (at most <paramref name="limit"/> steps); the reach, or null.</summary>
        static MapMissionReached StepToMission(MapScreen screen, int limit = 400)
        {
            for (int i = 0; i < limit; i++)
                foreach (var e in screen.Execute(new StepMap(P1)).Events)
                    if (e is MapMissionReached reached) return reached;
            return null;
        }

        static IEnumerable<int> AllButBoss => Content.Popups.Alert.Missions.Where(m => !m.Boss).Select(m => m.ScenarioId);

        // ---- Create ----

        [Test]
        public void CreatePlacesTheRoomsInstancesWhereTheirCreateEventsPutThem()
        {
            var map = Map();
            Assert.AreEqual(960, map.EarthX, "obj_earthball: x = room_width/2");
            Assert.AreEqual(1080, map.EarthY, "y = room_height");
            Assert.AreEqual(0, map.EarthAngle);
            Assert.AreEqual(0.01, map.AutoSpeed, 1e-12);
            Assert.AreEqual(1143, map.ShipX, "obj_uss_warren stays at the x it is placed at");
            Assert.AreEqual(372, map.ShipY, "1080 - 1062/2 - 211 - 66 + 100");
            Assert.AreEqual(903, map.DestinationX);
            Assert.AreEqual(549, map.DestinationY, "room_height - sprite_get_height(spr_earthball)/2");
            Assert.AreEqual(MapMode.Idle, map.Mode);
            Assert.AreEqual(-1, map.CurrentScenario);
            Assert.IsFalse(map.PanelVisible, "obj_scenario_details: visible = false");
            Assert.AreEqual(10, map.Missions.Count);
            Assert.IsNull(map.Dot);
            Assert.IsFalse(map.Execute(new CreateMap(P1)).Accepted, "once only");
        }

        [Test]
        public void CreateMakesTheTipAndClearsAnOldScenario()
        {
            var content = Content;
            var map = new MapScreen(content.Map, content.Popups.Alert, null, null, null, 1920, 1080);
            var events = map.Execute(new CreateMap(P1)).Events;
            Assert.IsTrue(events.OfType<MapTipRequested>().Any(), "obj_map's Create: tip = instance_create(0,0,obj_tip)");
            Assert.AreEqual(-1, events.OfType<MapScenarioSelected>().Single().Scenario,
                "D24: an old current_scenario cannot launch a mission no marker shows");
        }

        [Test]
        public void NothingStepsBeforeCreate()
        {
            var content = Content;
            var map = new MapScreen(content.Map, content.Popups.Alert, null, null, null, 1920, 1080);
            Assert.IsFalse(map.Execute(new StepMap(P1)).Accepted);
            Assert.IsFalse(map.Execute(new PressMapD(P1)).Accepted);
            Assert.IsFalse(map.Execute(new PressMapKey(P1)).Accepted);
        }

        // ---- The earth, the markers and A/D ----

        [Test]
        public void IdleTheEarthTurnsBackAHundredthOfADegreeAStep_AndTheMarkersRideItsRim()
        {
            var map = Map();
            Step(map, 1);
            Assert.AreEqual(359.99, map.EarthAngle, 1e-9, "image_angle -= auto_speed, then normalize_angle");
            var first = map.Missions[0].Marker;
            Assert.AreEqual(16 + 359.99, first.ImageAngle, 1e-9, "image_angle = degree + obj_earthball.image_angle");
            Assert.AreEqual(960 + 550 * Math.Cos((16 + 359.99) * Math.PI / 180), first.X, 1e-9);
            Assert.AreEqual(1080 - 550 * Math.Sin((16 + 359.99) * Math.PI / 180), first.Y, 1e-9);
        }

        [Test]
        public void DTurnsClockwise_ScrollsTheSkyRight_AndSlidesTheCommBoxOut()
        {
            var box = Box();
            var map = Map(box: box);
            var events = map.Execute(new PressMapD(P1)).Events;
            Assert.AreEqual("sfx_flick", events.OfType<MapSoundRequested>().Single().Sound);
            Assert.AreEqual(MapMode.Moving, map.Mode);
            Assert.AreEqual(MapTurn.Clockwise, map.Turning);
            Assert.AreEqual(1, map.SkySpeed, "background_hspeed[0] = 1");
            Assert.AreEqual(9, map.CurrentMission, "current_mission 0 wraps to mission_count - 1");
            Assert.AreEqual(ConversationMode.SlideOut, box.Mode);
            Assert.IsFalse(map.Execute(new PressMapA(P1)).Accepted, "only while idle");

            Step(map, 1);
            Assert.AreEqual(2, map.EarthAngle, 1e-9, "obj_destination: image_angle += turn_speed (no auto turn while moving)");
            Assert.AreEqual(1, map.SkyX, "background_x[0] by its hspeed");
        }

        [Test]
        public void ATurnsCounterclockwise_AndScrollsTheSkyLeft()
        {
            var map = Map();
            Assert.IsTrue(map.Execute(new PressMapA(P1)).Accepted);
            Assert.AreEqual(MapTurn.Counterclockwise, map.Turning);
            Assert.AreEqual(-1, map.SkySpeed);
            Assert.AreEqual(1, map.CurrentMission);
            Step(map, 1);
            Assert.AreEqual(358, map.EarthAngle, 1e-9);
            Assert.AreEqual(-1, map.SkyX);
        }

        [Test]
        public void TurningStopsOnTheNextMarkerItTouches_ShowingItsScenario()
        {
            var box = Box();
            var map = Map(box: box);
            map.Execute(new PressMapD(P1));
            var reached = StepToMission(map);
            Assert.IsNotNull(reached);
            Assert.AreEqual(6, reached.Scenario, "the 83 degree marker is the first to come under obj_destination turning clockwise");
            Assert.AreEqual(6, reached.Level);
            Assert.AreEqual(MapMode.Idle, map.Mode);
            Assert.AreEqual(0, map.SkySpeed, "background_hspeed[0] = 0");
            Assert.AreEqual(6, map.CurrentScenario);
            Assert.AreEqual(6, map.LastScenario);
            Assert.IsTrue(map.PanelVisible);
            Assert.AreEqual("Scenario 6#Wraiths", map.PanelText, "\"Scenario \" + string(scenario_id), then its level's name");
            Assert.AreEqual("Scenario 6\nWraiths", map.PanelDrawText);
            Assert.AreEqual(ConversationMode.SlideIn, box.Mode, "current_conversation.mode = \"slidein\"");
        }

        [Test]
        public void TheScenarioIsClearedAndThePanelHiddenWhileTheEarthTurns()
        {
            var map = Map();
            map.Execute(new PressMapD(P1));
            StepToMission(map);
            map.Execute(new PressMapD(P1));
            var events = Step(map, 1);
            Assert.AreEqual(-1, map.CurrentScenario);
            Assert.IsFalse(map.PanelVisible);
            Assert.AreEqual(-1, events.OfType<MapScenarioSelected>().Single().Scenario);
            Assert.AreEqual(6, map.LastScenario, "obj_destination keeps last_scenario, so it does not stop on the marker it leaves");
        }

        [Test]
        public void EveryMissionCanBeReached_InOrderAroundTheEarth_BothWays()
        {
            var clockwise = Map();
            var seen = new List<int>();
            for (int i = 0; i < 10; i++)
            {
                Assert.IsTrue(clockwise.Execute(new PressMapD(P1)).Accepted);
                var reached = StepToMission(clockwise);
                Assert.IsNotNull(reached, "a marker is touched within a turn of the earth");
                seen.Add(reached.Scenario);
            }
            CollectionAssert.AreEqual(new[] { 6, 5, 1, 10, 4, 3, 9, 8, 2, 7 }, seen, "by degree, downward from obj_destination's 96 degrees");

            var counter = Map();
            seen.Clear();
            for (int i = 0; i < 10; i++)
            {
                counter.Execute(new PressMapA(P1));
                seen.Add(StepToMission(counter).Scenario);
            }
            CollectionAssert.AreEquivalent(Enumerable.Range(1, 10), seen);
        }

        [Test]
        public void LeftIdleTheSlowTurnBringsAMarkerUnderTheDestination()
        {
            var map = Map();
            var reached = StepToMission(map, 3000);
            Assert.IsNotNull(reached, "obj_destination's collision event runs whatever the mode");
            Assert.AreEqual(7, reached.Scenario, "117 degrees, the nearest above obj_destination, comes down at 0.01 a step");
        }

        [Test]
        public void TheMarkersPingEvery30StepsOnlyWhileTheMapIdles()
        {
            var map = Map();
            var pings = Step(map, 30).OfType<MapMarkerPinged>().ToList();
            Assert.AreEqual(10, pings.Count, "each marker's alarm[0] = ping_rate");
            Assert.AreEqual("#FF0000", pings[0].Color);
            Assert.AreEqual(0, Step(map, 29).OfType<MapMarkerPinged>().Count());
            map.Execute(new PressMapD(P1));
            Assert.AreEqual(0, Step(map, 1).OfType<MapMarkerPinged>().Count(), "the alarm goes off while moving, without a ring");
            Assert.AreEqual(30, map.Missions[0].Marker.Alarm, "and is set again");
        }

        // ---- The launch ----

        [Test]
        public void AnyKeyDoesNothingWithoutAMission_OrWhileAOrDOrEscIsHeld()
        {
            var map = Map();
            Assert.IsFalse(map.Execute(new PressMapKey(P1)).Accepted, "current_scenario == -1");
            map.Execute(new PressMapD(P1));
            StepToMission(map);
            Assert.IsFalse(map.Execute(new PressMapKey(P1, aHeld: true)).Accepted);
            Assert.IsFalse(map.Execute(new PressMapKey(P1, dHeld: true)).Accepted);
            Assert.IsFalse(map.Execute(new PressMapKey(P1, escapeHeld: true)).Accepted);
            Assert.IsNull(map.Dot);
        }

        [Test]
        public void AnyKeySendsTheWhiteDotFromTheShipToTheDestination_AndItsArrivalLaunchesTheMission()
        {
            var map = Map();
            map.Execute(new PressMapD(P1));
            StepToMission(map);
            var launch = map.Execute(new PressMapKey(P1));
            Assert.IsTrue(launch.Accepted);
            Assert.AreEqual("sfx_takeoff", launch.Events.OfType<MapSoundRequested>().Single().Sound);
            Assert.AreEqual(6, launch.Events.OfType<MapLaunched>().Single().Scenario);
            var dot = map.Dot;
            Assert.AreEqual(1143, dot.X);
            Assert.AreEqual(372, dot.Y);
            Assert.AreEqual(MapWhiteDot.PointDirection(1143, 372, 903, 549), dot.ImageAngle, 1e-12, "image_angle = direction");
            Assert.AreEqual(0, map.AutoSpeed, "obj_earthball.auto_speed = 0");
            Assert.IsFalse(map.Execute(new PressMapA(P1)).Accepted, "A and D are refused once the dot exists");
            Assert.IsFalse(map.Execute(new PressMapKey(P1)).Accepted, "one dot");

            double angle = map.EarthAngle;
            Step(map, 1);
            Assert.AreEqual(1143 + Math.Cos(dot.Direction * Math.PI / 180), dot.X, 1e-9, "move_towards_point(destX, destY, 1)");
            Assert.AreEqual(1 - 0.003, dot.Alpha, 1e-12, "image_alpha -= fade_speed");
            Assert.AreEqual(angle, map.EarthAngle, "the earth's slow turn has stopped");

            List<MapEvent> arrival = null;
            int steps = 1;
            while (arrival == null && steps < 400)
            {
                steps++;
                var events = map.Execute(new StepMap(P1)).Events;
                if (events.OfType<MapRoomRequested>().Any()) arrival = events.ToList();
            }
            Assert.IsNotNull(arrival, "it arrives");
            Assert.That(steps, Is.InRange(285, 300), "about 298 px at a pixel a step, less its box");
            Assert.LessOrEqual(dot.DistanceToPoint(903, 549), 1d);
            var chosen = arrival.OfType<MapMissionChosen>().Single();
            Assert.AreEqual(6, chosen.Scenario);
            Assert.AreEqual(6, chosen.Level, "V42: the mission is kept, not discarded");
            Assert.AreEqual(RoomId.Intro, arrival.OfType<MapRoomRequested>().Single().Room, "room_goto(room_intro)");
            Assert.IsTrue(map.Left);
            Assert.IsFalse(Step(map, 3).OfType<MapRoomRequested>().Any(), "once");
        }

        // ---- The boss ----

        [Test]
        public void TheBossIsLockedUntilEveryOtherMissionIsWon()
        {
            var map = Map();
            Assert.IsTrue(map.BossLocked);
            var boss = map.Missions.Single(m => m.Definition.Boss);
            Assert.AreEqual(8, boss.Level, "level_08, Scrap Mental");
            Assert.IsTrue(boss.Locked);
            Assert.IsTrue(map.Missions.Where(m => m != boss).All(m => !m.Locked));

            Assert.IsTrue(Map(AllButBoss.Skip(1)).BossLocked, "one mission still to win");
            var unlocked = Map(AllButBoss);
            Assert.IsFalse(unlocked.BossLocked);
            Assert.IsFalse(unlocked.Missions.Single(m => m.Definition.Boss).Locked);
        }

        [Test]
        public void TheLockedBossShowsItsLockAndRefusesTheLaunch()
        {
            var map = Map();
            MapMissionReached reached = null;
            for (int i = 0; i < 10 && (reached == null || reached.Scenario != 8); i++)
            {
                map.Execute(new PressMapD(P1));
                reached = StepToMission(map);
            }
            Assert.AreEqual(8, reached.Scenario);
            Assert.IsTrue(reached.Locked);
            StringAssert.EndsWith("#Locked: win every other mission first", map.PanelText);
            var refused = map.Execute(new PressMapKey(P1));
            Assert.IsFalse(refused.Accepted);
            Assert.AreEqual(8, refused.Events.OfType<MapLaunchRefused>().Single().Scenario);
            Assert.IsNull(map.Dot);
        }

        [Test]
        public void TheUnlockedBossLaunches()
        {
            var map = Map(AllButBoss);
            MapMissionReached reached = null;
            for (int i = 0; i < 10 && (reached == null || reached.Scenario != 8); i++)
            {
                map.Execute(new PressMapD(P1));
                reached = StepToMission(map);
            }
            Assert.IsFalse(reached.Locked);
            Assert.AreEqual("Scenario 8#Scrap Mental", map.PanelText);
            Assert.IsTrue(map.Execute(new PressMapKey(P1)).Accepted);
        }

        // ---- obj_btn_upgrade ----

        [Test]
        public void TheUpgradesButtonHoversWithSfxHover_AndItsPressFadesToTheGarage()
        {
            var map = Map();
            var enter = map.Execute(new PointMap(P1, 100, 40));
            Assert.IsTrue(map.UpgradeHovered);
            Assert.AreEqual("sfx_hover", enter.Events.OfType<MapSoundRequested>().Single().Sound);
            Assert.IsTrue(enter.Events.OfType<MapHoverChanged>().Single().Hovered);
            Assert.IsFalse(map.Execute(new PointMap(P1, 101, 41)).Accepted, "no change while it stays over");
            var leave = map.Execute(new PointMap(P1, 100, 80));
            Assert.IsFalse(map.UpgradeHovered);
            Assert.IsFalse(leave.Events.OfType<MapSoundRequested>().Any());

            Assert.IsFalse(map.Execute(new PressMap(P1, 500, 500)).Accepted);
            var press = map.Execute(new PressMap(P1, -5, 23));
            Assert.IsTrue(press.Accepted, "the button's top left corner (100-105, 40-17)");
            Assert.AreEqual("sfx_click", press.Events.OfType<MapSoundRequested>().Single().Sound);
            Assert.AreEqual(RoomId.Upgrade, press.Events.OfType<MapFadeRequested>().Single().Room);
        }

        // ---- Masks ----

        [Test]
        public void ThePreciseMasksCollideOnlyWhereBothHaveSolidPixels()
        {
            var t = Content.Map;
            Assert.AreEqual(6, t.MarkerMask.Frames.Length, "spr_alertmarker's six frames");
            Assert.IsTrue(t.DestinationMask.Solid(0, 30, 64));
            Assert.IsFalse(t.DestinationMask.Solid(0, 24, 64), "outside its 25..35 box");
            Assert.IsTrue(t.MarkerMask.Solid(0, 14, 23), "frame 0's first row: ..............#");
            Assert.IsFalse(t.MarkerMask.Solid(0, 13, 23));
            Assert.IsFalse(t.MarkerMask.Solid(0, 14, 22), "above the hand-set box");

            // The destination strip through a marker's filled block, then beside it.
            Assert.IsTrue(GmlPreciseMask.Collide(t.DestinationMask, 0, 903, 549, 0, t.MarkerMask, 0, 897, 520, 0));
            Assert.IsFalse(GmlPreciseMask.Collide(t.DestinationMask, 0, 903, 549, 0, t.MarkerMask, 0, 960, 520, 0));
            Assert.IsFalse(GmlPreciseMask.Collide(t.DestinationMask, 0, 903, 549, 0, t.MarkerMask, 0, 897, 300, 0), "above the strip");
        }

        [Test]
        public void TheWhiteDotsDistanceIsFromItsBoundingBox()
        {
            var t = Content.Map;
            t.DotMask.Bounds(100, 100, 0, out int l, out int top, out int r, out int b);
            Assert.AreEqual(new[] { 96, 99, 104, 101 }, new[] { l, top, r, b }, "spr_dropdot's 0..8 x 3..5 box about its 4,4 origin");
            Assert.AreEqual(0d, MapWhiteDot.PointDirection(0, 0, 10, 0));
            Assert.AreEqual(90d, MapWhiteDot.PointDirection(0, 0, 0, -10), "up the screen is 90");
            Assert.AreEqual(270d, MapWhiteDot.PointDirection(0, 0, 0, 10));
        }

        // ---- obj_scanner (U.HARE-T63) ----

        /// <summary>Deals the given numbers in turn (then the last one again) and records each irandom_range asked.</summary>
        sealed class ScriptedRandom : IRandomSource
        {
            readonly int[] values;
            int dealt;
            public readonly List<(int Min, int Max)> Asked = new List<(int, int)>();
            public ScriptedRandom(params int[] values) => this.values = values;
            public int IRandom(int min, int max)
            {
                Asked.Add((min, max));
                return values[Math.Min(dealt++, values.Length - 1)];
            }
            public float Range(float min, float max) => min;
        }

        static MapScreen MapWith(IRandomSource random)
        {
            var content = Content;
            var screen = new MapScreen(content.Map, content.Popups.Alert, null, null, level => "", 1920, 1080, random);
            Assert.IsTrue(screen.Execute(new CreateMap(P1)).Accepted);
            return screen;
        }

        [Test]
        public void TheScannerStartsUnderTheShipAtAlpha02_WithTheSweepAtRest()
        {
            var s = MapWith(new ScriptedRandom(20)).Scanner;
            Assert.IsNotNull(s, "obj_scanner is made with the room");
            Assert.AreEqual(1045, s.X, "obj_scanner stays at the x it is placed at");
            Assert.AreEqual(388, s.Y, "1080 - 1062/2 - 211 + 50");
            Assert.AreEqual(0.2d, s.Alpha, 1e-6, "image_alpha = 0.2");
            Assert.AreEqual(0d, s.ImageAngle);
            Assert.AreEqual(0d, s.SweepY);
            Assert.IsTrue(s.SweepLeft);
        }

        [Test]
        public void TheScannerFlickersAt001TimesIrandomRange16To24EveryStep()
        {
            var random = new ScriptedRandom(16, 24, 19);
            var map = MapWith(random);
            Step(map, 1);
            Assert.AreEqual(0.16d, map.Scanner.Alpha, 1e-12);
            Step(map, 1);
            Assert.AreEqual(0.24d, map.Scanner.Alpha, 1e-12);
            Step(map, 1);
            Assert.AreEqual(0.19d, map.Scanner.Alpha, 1e-12);
            Assert.AreEqual(0.19f, map.Scanner.DrawAlpha, 1e-6f);
            CollectionAssert.AreEqual(new[] { (16, 24), (16, 24), (16, 24) }, random.Asked);
        }

        [Test]
        public void TheScannerSwaysByTheGmlSweep_Between0AndAbout26Degrees_Every102Steps()
        {
            var map = MapWith(new ScriptedRandom(20));
            var s = map.Scanner;

            // Replay obj_scanner's Step literally (GML's < and > are false within its 0.00001 epsilon).
            bool left = true;
            double sweepY = 0d, angle = 0d, min = 0d, max = 0d;
            for (int i = 0; i < 400; i++)
            {
                if (left) { if (sweepY < 0.1d && Math.Abs(sweepY - 0.1d) >= 1e-5) sweepY += 0.004d; else left = false; }
                else { if (sweepY > -0.1d && Math.Abs(sweepY + 0.1d) >= 1e-5) sweepY -= 0.004d; else left = true; }
                angle += sweepY;
                Step(map, 1);
                Assert.AreEqual(sweepY, s.SweepY, 1e-12, "sweepY at step " + (i + 1));
                Assert.AreEqual(angle, s.ImageAngle, 1e-9, "image_angle at step " + (i + 1));
                Assert.AreEqual(left, s.SweepLeft, "sweepLeft at step " + (i + 1));
                Assert.LessOrEqual(Math.Abs(s.SweepY), 0.1d + 1e-9, "sweepY never passes sweepAmount");
                min = Math.Min(min, s.ImageAngle);
                max = Math.Max(max, s.ImageAngle);
            }
            Assert.AreEqual(2.6d, max, 0.01d, "the beam swings up to about 2.6 degrees");
            Assert.AreEqual(0d, min, 0.01d, "and back to where it started");

            // Rising 25 steps to 0.1, one step to turn, 50 down to -0.1, one to turn, 25 back: 102 steps a sway.
            var again = MapWith(new ScriptedRandom(20));
            Step(again, 102);
            Assert.AreEqual(0d, again.Scanner.SweepY, 1e-9);
            Assert.AreEqual(0d, again.Scanner.ImageAngle, 1e-9);
            Assert.IsTrue(again.Scanner.SweepLeft);
        }

        [Test]
        public void TheScannerKeepsSwayingWhileTheEarthTurns_AndAfterTheLaunch()
        {
            var map = MapWith(new ScriptedRandom(20));
            Step(map, 10);
            double before = map.Scanner.ImageAngle;
            Assert.IsTrue(map.Execute(new PressMapD(P1)).Accepted);
            Step(map, 5);
            Assert.AreNotEqual(before, map.Scanner.ImageAngle, "obj_scanner's Step ignores obj_map's mode");
            Assert.AreEqual(1045, map.Scanner.X, "and never moves");
            Assert.AreEqual(388, map.Scanner.Y);
        }

        [Test]
        public void TheGmlLessIsFalseWithinTheEpsilon()
        {
            Assert.IsTrue(MapScanner.GmlLess(0.09d, 0.1d));
            Assert.IsFalse(MapScanner.GmlLess(0.1d - 1e-6, 0.1d), "equal within 0.00001");
            Assert.IsFalse(MapScanner.GmlLess(0.1d, 0.1d));
            Assert.IsFalse(MapScanner.GmlLess(0.2d, 0.1d));
        }

        // ---- The data ----

        [Test]
        public void TheMapSectionIsTheOriginalsNumbers_AndValidates()
        {
            var t = Content.Map;
            Assert.AreEqual("bg_nightsky", t.Background);
            Assert.AreEqual(2d, t.TurnSpeed);
            Assert.AreEqual(0.01d, t.AutoSpeed);
            Assert.AreEqual(0.003d, t.DotFadeSpeed);
            Assert.AreEqual(RoomId.Intro, t.LaunchRoom);
            Assert.AreEqual("Upgrades", t.Upgrade.Label);
            Assert.AreEqual(RoomId.Upgrade, t.Upgrade.Room);

            var bad = Assert.Throws<ContentValidationException>(() => ContentRegistry.FromJson("{ \"map\": { \"DotSpeed\": 0, \"LaunchRoom\": \"Nowhere\" } }"));
            Assert.That(bad.Message, Does.Contain("map.DotSpeed"));
            var missions = Assert.Throws<ContentValidationException>(() => ContentRegistry.FromJson(
                "{ \"popups\": { \"Alert\": { \"Missions\": [ { \"Degree\": 1, \"ScenarioId\": 1, \"Level\": 1 }, { \"Degree\": 2, \"ScenarioId\": 1, \"Level\": 0 } ] } } }"));
            Assert.That(missions.Message, Does.Contain("listed more than once"));
            Assert.That(missions.Message, Does.Contain("Missions[1].Level"));
        }

        // ---- The session, room_intro and the save ----

        [Test]
        public void ChooseMissionIsTheCampaignAtTheMissionsLevel_AndWinningThatLevelWinsIt()
        {
            var session = new GameSession(Content);
            var chosen = session.Execute(new ChooseMission(P1, 6, 6));
            Assert.AreEqual(GameMode.Campaign, session.Mode);
            Assert.AreEqual(6, session.Scenario);
            Assert.AreEqual(6, session.Level);
            Assert.AreEqual(6, session.MissionLevel);
            Assert.IsNotNull(chosen.Find<GameModeChosen>());

            var won = session.Execute(new CompleteLevel(P1));
            Assert.IsNotNull(won.Find<LevelCompleted>());
            var mission = won.Find<MissionWon>();
            Assert.IsNotNull(mission);
            Assert.AreEqual(6, mission.Scenario);
            Assert.AreEqual(6, mission.Level);
        }

        [Test]
        public void OnlyTheMissionsOwnLevelWinsIt()
        {
            var session = new GameSession(Content);
            session.Execute(new ChooseMission(P1, 3, 3));
            session.Execute(new AdvanceLevel(P1, resetScore: true));
            Assert.IsNull(session.Execute(new CompleteLevel(P1)).Find<MissionWon>(), "level 4 after mission 3's level 3");

            var quick = new GameSession(Content);
            quick.Execute(new ChooseQuickstart(P1, 3));
            Assert.IsNull(quick.Execute(new CompleteLevel(P1)).Find<MissionWon>(), "a quick game wins no mission");
            Assert.AreEqual(0, quick.MissionLevel);

            var campaign = new GameSession(Content);
            campaign.Execute(new ChooseCampaign(P1));
            Assert.IsNull(campaign.Execute(new CompleteLevel(P1)).Find<MissionWon>(), "the story's campaign has no mission yet");
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChooseMission(P1, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChooseMission(P1, 1, 0));
        }

        [Test]
        public void RoomIntroKeepsTheMapsMission_InsteadOfAQuickGameAtLevel1()
        {
            var content = Content;
            var kept = new IntroScreen(content.Intro, 1920, 1080, new SeededRandom(7), missionScenario: 4, missionLevel: 4);
            Assert.AreEqual(4, kept.MissionScenario);
            var plain = new IntroScreen(content.Intro, 1920, 1080, new SeededRandom(7));
            Assert.AreEqual(-1, plain.MissionScenario);

            IntroEvent Leave(IntroScreen screen)
            {
                screen.Execute(new CreateIntro(P1));
                for (int i = 0; i < 5000 && !screen.Left; i++)
                {
                    screen.Execute(new PressIntroKey(P1));
                    var events = screen.Execute(new StepIntro(P1)).Events;
                    foreach (var e in events)
                        if (e is IntroMissionKept || e is IntroModeChosen) return e;
                }
                return null;
            }

            var keptEvent = Leave(kept) as IntroMissionKept;
            Assert.IsNotNull(keptEvent);
            Assert.AreEqual(4, keptEvent.Scenario);
            Assert.AreEqual(4, keptEvent.Level);
            var quick = Leave(plain) as IntroModeChosen;
            Assert.IsNotNull(quick, "without a map mission the original's quick game at level 1");
            Assert.AreEqual(1, quick.Level);
        }

        [Test]
        public void CampaignProgressRecordsWinsAndRoundTripsItsVersionedJson()
        {
            var progress = new CampaignProgress();
            Assert.IsTrue(progress.RecordWin(3));
            Assert.IsTrue(progress.RecordWin(1));
            Assert.IsFalse(progress.RecordWin(3), "already won");
            CollectionAssert.AreEqual(new[] { 1, 3 }, progress.Won.ToArray());
            Assert.Throws<ArgumentOutOfRangeException>(() => progress.RecordWin(0));

            string json = progress.ToJson();
            StringAssert.Contains("\"Version\": 1", json);
            var back = CampaignProgress.FromJson(json);
            CollectionAssert.AreEqual(new[] { 1, 3 }, back.Won.ToArray());
            Assert.AreEqual(0, CampaignProgress.FromJson("{\"Version\": 1}").Won.Count);

            Assert.Throws<FormatException>(() => CampaignProgress.FromJson(""));
            Assert.Throws<FormatException>(() => CampaignProgress.FromJson("not json"));
            Assert.Throws<FormatException>(() => CampaignProgress.FromJson("{\"Won\": [1]}"), "no version");
            Assert.Throws<FormatException>(() => CampaignProgress.FromJson("{\"Version\": 2, \"Won\": [1]}"), "a later format");
            Assert.Throws<FormatException>(() => CampaignProgress.FromJson("{\"Version\": 1, \"Won\": [0]}"));
            Assert.Throws<FormatException>(() => CampaignProgress.FromJson("{\"Version\": 1, \"Won\": [1.5]}"));
        }

        [Test]
        public void CampaignProgressUnlocksTheBossOnceEveryOtherMissionIsWon()
        {
            var missions = Content.Popups.Alert.Missions;
            var progress = new CampaignProgress();
            foreach (int scenario in AllButBoss.Skip(1)) progress.RecordWin(scenario);
            Assert.IsFalse(progress.BossUnlocked(missions));
            progress.RecordWin(AllButBoss.First());
            Assert.IsTrue(progress.BossUnlocked(missions));
            Assert.IsFalse(Map(progress.Won).BossLocked);
        }
    }
}
