using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using StringAssert = NUnit.Framework.Legacy.StringAssert;
using Unity.Hare.Arena;
using Unity.Hare.Content;
using Unity.Hare.Flow;
using Unity.Hare.Player;

namespace Unity.Hare.Tests.Core
{
    /// <summary>
    /// The arena (U.HARE-T52): room_gamescreen and its siblings as the level scripts set them up, the backgrounds' scroll,
    /// view 0 and the minimap following the player, the M key, obj_player's loop and buffer-zone rules with
    /// random_positions, the off-screen arrow and the dotted leap line, and the arena section as data.
    /// </summary>
    public class ArenaWorldTests
    {
        static readonly PlayerId P1 = new PlayerId(1);

        sealed class ScriptedRandom : IRandomSource
        {
            readonly Queue<int> next = new Queue<int>();
            public readonly List<(int Min, int Max)> Asked = new List<(int, int)>();

            public ScriptedRandom(params int[] values) { foreach (var v in values) next.Enqueue(v); }

            public int IRandom(int min, int max)
            {
                Asked.Add((min, max));
                return next.Count > 0 ? next.Dequeue() : min;
            }

            public float Range(float min, float max) => min;
        }

        static ArenaWorld Arena(int level, RoomId room = RoomId.GameScreen, IRandomSource random = null)
        {
            var content = ContentRegistry.Default;
            var arena = new ArenaWorld(content.Arena, content.Rooms.Get(room), content.Arena.SettingFor(level, room), random ?? new ScriptedRandom());
            arena.Execute(new CreateArena(P1));
            return arena;
        }

        // ---- The room and its level setting ----

        [Test]
        public void Level1_IsRoomGamescreenAt3840x2160_WithTheWorldCurveStretchedAndNoLoops()
        {
            var arena = Arena(1);
            Assert.AreEqual(3840, arena.Width);
            Assert.AreEqual(2160, arena.Height);
            Assert.AreEqual(120, arena.Room.StepsPerSecond);
            Assert.AreEqual("Hydras & Saws", arena.Setting.Name);
            Assert.AreEqual(2, arena.Layers.Count);
            var back = arena.Layers[0];
            Assert.AreEqual("bg_worldcurve", back.Name, "level_01: background_index[0] = bg_worldcurve");
            Assert.IsTrue(back.Stretch, "room_gamescreen's slot 0 is stretched over the room");
            Assert.IsTrue(back.Drawn);
            Assert.IsFalse(arena.Layers[1].Drawn, "background_index[1] = noone");
            Assert.IsFalse(arena.LoopHorizontal);
            Assert.IsFalse(arena.LoopVertical);
            Assert.IsFalse(arena.RandomizeOnLoop);
        }

        [Test]
        public void EveryLevelScriptsSetting_IsData()
        {
            var tuning = ContentRegistry.Default.Arena;
            var expected = new[]
            {
                (1, RoomId.GameScreen, "bg_worldcurve"), (2, RoomId.GameScreen, "bg_moonview"), (3, RoomId.GameScreen, "bg_uphigh"),
                (4, RoomId.Shaft, "bg_stormclouds"), (5, RoomId.SideScroll, "bg_tropicalocean"), (6, RoomId.GameScreen, "bg_nightsky"),
                (7, RoomId.GameScreen, "bg_worldcurve"), (8, RoomId.GameScreen, "bg_worldcurve"), (9, RoomId.GameScreen, "bg_atmosphere"),
                (10, RoomId.GameScreen, "bg_atmosphere"),
            };
            foreach (var (level, room, background) in expected)
            {
                var setting = tuning.SettingFor(level);
                Assert.AreEqual(level, setting.Level);
                Assert.AreEqual("level_" + level.ToString("00"), setting.Script);
                Assert.AreEqual(room, setting.Room, setting.Script);
                Assert.AreEqual(background, setting.Background, setting.Script);
            }

            var shaft = tuning.SettingFor(4);
            Assert.IsTrue(shaft.LoopHorizontal && shaft.LoopVertical && !shaft.RandomizeOnLoop, "level_04 loops both ways");
            Assert.AreEqual(-1, shaft.HSpeed, "level_04: background_hspeed = -1");
            var islands = tuning.SettingFor(5);
            Assert.IsTrue(islands.LoopHorizontal && !islands.LoopVertical && islands.RandomizeOnLoop, "level_05 wraps sideways and scatters");
            Assert.IsTrue(tuning.SettingFor(6).Tiled, "level_06 tiles bg_nightsky");
            Assert.AreEqual("level_default", tuning.SettingFor(11).Script, "setup_level's else branch");
            Assert.AreEqual("bg_orange", tuning.SettingFor(11).Background);
            Assert.AreEqual("bg_gradient3", tuning.GeneratorLevel.Overlay, "level_enemygenerator puts bg_gradient3 over the night sky");
        }

        [Test]
        public void ARoomOpenedForAnotherRoomsLevel_PlaysTheFirstLevelThatUsesIt()
        {
            var tuning = ContentRegistry.Default.Arena;
            Assert.AreEqual("level_04", tuning.SettingFor(1, RoomId.Shaft).Script);
            Assert.AreEqual("level_05", tuning.SettingFor(1, RoomId.SideScroll).Script);
            Assert.AreEqual("level_01", tuning.SettingFor(1, RoomId.GameScreen).Script);
            Assert.AreEqual("level_06", tuning.SettingFor(6, RoomId.GameScreen).Script);
        }

        [Test]
        public void TheShaftTilesItsCloudsBothWays_AndScrollsThemLeft()
        {
            var arena = Arena(4, RoomId.Shaft);
            Assert.AreEqual(1920, arena.Width);
            Assert.AreEqual(12000, arena.Height);
            var back = arena.Layers[0];
            Assert.IsTrue(back.HTiled && back.VTiled && !back.Stretch);
            for (int i = 0; i < 5; i++) Assert.IsTrue(arena.Execute(new StepArena(P1)).Accepted);
            Assert.AreEqual(-5, back.X, "background_x[0] += background_hspeed[0] every step");
            Assert.AreEqual(0, back.Y);
        }

        [Test]
        public void TheBackgroundsDoNotMoveBeforeCreate()
        {
            var content = ContentRegistry.Default;
            var arena = new ArenaWorld(content.Arena, content.Rooms.Get(RoomId.Shaft), content.Arena.SettingFor(4), new ScriptedRandom());
            Assert.IsFalse(arena.Execute(new StepArena(P1)).Accepted);
            Assert.AreEqual(0, arena.Layers[0].X);
        }

        // ---- The views ----

        [Test]
        public void View0_FollowsAt40PxAStep_AndSettlesCentredOnThePlayer()
        {
            var arena = Arena(1);
            Assert.AreEqual(0f, arena.View.X);
            Assert.AreEqual(0f, arena.View.Y);
            arena.Execute(new FollowPlayer(P1, 1920f, 1080f));
            Assert.AreEqual(40f, arena.View.X, "hspeed 40");
            Assert.AreEqual(40f, arena.View.Y, "vspeed 40");
            for (int i = 0; i < 30; i++) arena.Execute(new FollowPlayer(P1, 1920f, 1080f));
            Assert.AreEqual(960f, arena.View.X, "centred: 1920 - 1920/2");
            Assert.AreEqual(540f, arena.View.Y);
            Assert.AreEqual(1920f, arena.View.CenterX);
        }

        [Test]
        public void TheViewsStayInsideTheRoom()
        {
            var arena = Arena(1);
            for (int i = 0; i < 200; i++) arena.Execute(new FollowPlayer(P1, 5000f, 3000f));
            Assert.AreEqual(3840f - 1920f, arena.View.X);
            Assert.AreEqual(2160f - 1080f, arena.View.Y);
            Assert.AreEqual(3840f - 3000f, arena.Minimap.X, "the minimap too");
            Assert.AreEqual(2160f - 2000f, arena.Minimap.Y);
            for (int i = 0; i < 200; i++) arena.Execute(new FollowPlayer(P1, -500f, -500f));
            Assert.AreEqual(0f, arena.View.X);
            Assert.AreEqual(0f, arena.View.Y);
        }

        [Test]
        public void TheMinimap_FollowsWithNoSpeedLimit()
        {
            var arena = Arena(1);
            arena.Execute(new FollowPlayer(P1, 2000f, 1500f));
            Assert.AreEqual(500f, arena.Minimap.X, "2000 - 3000/2, in one step");
            Assert.AreEqual(160f, arena.Minimap.Y, "1500 - 1000 = 500 is past the room's bottom; kept at 2160 - 2000");
        }

        [Test]
        public void AViewWiderThanItsRoom_ShowsTheRoomsLeftEdge()
        {
            Assert.AreEqual(0f, ArenaView.FollowAxis(0f, 960, 3000, 1920, -1, 1920), "room_shaft is 1920 wide; the minimap 3000");
        }

        [Test]
        public void ASmallBorder_OnlyMovesTheViewToKeepThePlayerThatFarInside()
        {
            Assert.AreEqual(100f, ArenaView.FollowAxis(100f, 500, 1000, 200, -1, 5000), "inside the borders: no move");
            Assert.AreEqual(250f, ArenaView.FollowAxis(100f, 1050, 1000, 200, -1, 5000), "1050 + 200 - 1000");
            Assert.AreEqual(50f, ArenaView.FollowAxis(100f, 250, 1000, 200, -1, 5000), "250 - 200");
        }

        [Test]
        public void TheMinimapStartsHiddenFromShowMap_AndMTogglesIt()
        {
            var content = ContentRegistry.Default;
            var arena = new ArenaWorld(content.Arena, content.Rooms.Get(RoomId.GameScreen), content.Arena.SettingFor(1), new ScriptedRandom());
            var created = arena.Execute(new CreateArena(P1));
            Assert.IsTrue(created.Accepted);
            Assert.IsFalse(arena.Minimap.Visible, "globals.gml: show_map = false");
            Assert.IsTrue(arena.View.Visible);
            Assert.IsFalse(((MinimapToggled)created.Events.Single()).Visible);
            Assert.IsFalse(arena.Execute(new CreateArena(P1)).Accepted, "Create runs once");

            var shown = arena.Execute(new ToggleMinimap(P1));
            Assert.IsTrue(arena.ShowMap);
            Assert.IsTrue(arena.Minimap.Visible);
            Assert.IsTrue(shown.Events.OfType<MinimapToggled>().Single().Visible);
            arena.Execute(new ToggleMinimap(P1));
            Assert.IsFalse(arena.Minimap.Visible);
        }

        [Test]
        public void TheMinimapIs3000x2000DrawnAt1600x800_300x200()
        {
            var minimap = Arena(1).Minimap.Definition;
            Assert.AreEqual((3000, 2000, 1600, 800, 300, 200), (minimap.Width, minimap.Height, minimap.PortX, minimap.PortY, minimap.PortWidth, minimap.PortHeight));
        }

        // ---- obj_player's boundaries ----

        [Test]
        public void InsideTheRoomOrTheBufferZone_NothingHappens()
        {
            var arena = Arena(1);
            Assert.IsFalse(arena.Execute(new CheckPlayerBounds(P1, 1920f, 1080f)).Accepted);
            Assert.IsFalse(arena.Execute(new CheckPlayerBounds(P1, -400f, 2160f + 400f)).Accepted, "exactly at the buffer zone is still in");
            Assert.IsFalse(arena.Execute(new CheckPlayerBounds(P1, 1920f, -96f)).Accepted, "reset_player's drop-in point");
        }

        [Test]
        public void PastTheBufferZone_ALifeGoes_AndResetPlayerPutsItAboveTheRoomsMiddle()
        {
            var arena = Arena(1);
            foreach (var (x, y) in new[] { (-401f, 500f), (3840f + 401f, 500f), (500f, -401f), (500f, 2160f + 401f) })
            {
                var result = arena.Execute(new CheckPlayerBounds(P1, x, y));
                Assert.IsTrue(result.Accepted, $"({x}, {y})");
                var gone = result.Events.OfType<PlayerOutOfBounds>().Single();
                Assert.AreEqual(1920f, gone.X, "room_width / 2");
                Assert.AreEqual(-96f, gone.Y, "-sprite_get_height(spr_rabbit_leap)");
                Assert.IsEmpty(result.Events.OfType<PlayerWrapped>().ToList());
            }
            Assert.AreEqual(1, arena.Execute(new CheckPlayerBounds(P1, -500f, -500f)).Events.OfType<PlayerOutOfBounds>().Count(),
                "the horizontal reset leaves it inside the vertical buffer, so one life, not two");
        }

        [Test]
        public void LoopHorizontal_WrapsPastTheSpriteWidth_ToTheOtherSide()
        {
            var arena = Arena(4, RoomId.Shaft);
            Assert.IsFalse(arena.Execute(new CheckPlayerBounds(P1, -96f, 500f)).Accepted);
            var left = arena.Execute(new CheckPlayerBounds(P1, -97f, 500f)).Events.OfType<PlayerWrapped>().Single();
            Assert.AreEqual(1920f + 96f, left.X, "x = room_width + sprite_width");
            Assert.AreEqual(500f, left.Y);
            Assert.IsTrue(left.Horizontal);
            Assert.IsFalse(left.RandomizePositions, "level_04 does not randomize");
            var right = arena.Execute(new CheckPlayerBounds(P1, 1920f + 97f, 500f)).Events.OfType<PlayerWrapped>().Single();
            Assert.AreEqual(-96f, right.X, "x = -sprite_width");
        }

        [Test]
        public void LoopVertical_WrapsPastTheBufferZone_ToJustBeyondTheOtherEdge()
        {
            var arena = Arena(4, RoomId.Shaft);
            Assert.IsFalse(arena.Execute(new CheckPlayerBounds(P1, 500f, -400f)).Accepted);
            var top = arena.Execute(new CheckPlayerBounds(P1, 500f, -401f)).Events.OfType<PlayerWrapped>().Single();
            Assert.AreEqual(12000f + 96f, top.Y, "y = room_height + sprite_height");
            Assert.IsFalse(top.Horizontal);
            var bottom = arena.Execute(new CheckPlayerBounds(P1, 500f, 12000f + 401f)).Events.OfType<PlayerWrapped>().Single();
            Assert.AreEqual(-96f, bottom.Y, "y = -sprite_height");
        }

        [Test]
        public void TheSideScroll_WrapsSideways_ButCostsALifeOffTheTopOrBottom()
        {
            var arena = Arena(5, RoomId.SideScroll);
            var wrap = arena.Execute(new CheckPlayerBounds(P1, 12000f + 97f, 500f)).Events.OfType<PlayerWrapped>().Single();
            Assert.IsTrue(wrap.RandomizePositions, "level_05: randomize_on_loop");
            var gone = arena.Execute(new CheckPlayerBounds(P1, 500f, 1080f + 401f)).Events.OfType<PlayerOutOfBounds>().Single();
            Assert.AreEqual(6000f, gone.X);
        }

        [Test]
        public void RandomPositions_RollsXThenYOverTheWholeRoom_ForEveryObject()
        {
            var random = new ScriptedRandom(10, 20, 30, 40);
            var arena = Arena(5, RoomId.SideScroll, random);
            var scattered = arena.Execute(new ScatterObjects(P1, 2)).Events.OfType<PositionsScattered>().Single();
            CollectionAssert.AreEqual(new[] { new Vec2(10, 20), new Vec2(30, 40) }, scattered.Points);
            CollectionAssert.AreEqual(new[] { (0, 12000), (0, 1080), (0, 12000), (0, 1080) }, random.Asked, "irandom_range(0, room_width), irandom_range(0, room_height)");
        }

        // ---- The stage rooms (U.HARE-T21) ----

        [Test]
        public void TheStageRooms_View0FollowsWithNoSpeedLimit_AndTheMinimapSitsAt1300x860()
        {
            foreach (var (level, room, x, y, viewX, viewY) in new[]
            {
                (5, RoomId.SideScroll, 9000f, 500f, 9000f - 960f, 0f),
                (4, RoomId.Shaft, 500f, 9000f, 0f, 9000f - 540f),
            })
            {
                var arena = Arena(level, room);
                arena.Execute(new FollowPlayer(P1, x, y));
                Assert.AreEqual(viewX, arena.View.X, $"{room}: hspeed -1, centred in one step");
                Assert.AreEqual(viewY, arena.View.Y, $"{room}: vspeed -1, kept inside the room");
                var minimap = arena.Minimap.Definition;
                Assert.AreEqual((1300, 860, 300, 200), (minimap.PortX, minimap.PortY, minimap.PortWidth, minimap.PortHeight), room.ToString());
            }
        }

        [Test]
        public void AProjectileGoesOnlyPastTheBufferZoneOfItsOwnRoom()
        {
            var side = Arena(5, RoomId.SideScroll);
            Assert.IsFalse(side.OutOfPlay(11000f, 500f), "deep in the 12000 px side-scroll, far past room_gamescreen's width");
            Assert.IsFalse(side.OutOfPlay(12000f + 400f, 1080f + 400f), "exactly at the buffer zone is still in");
            Assert.IsTrue(side.OutOfPlay(12000f + 401f, 500f));
            Assert.IsTrue(side.OutOfPlay(500f, 1080f + 401f), "x - buffer_zone > room_width; y likewise below");
            Assert.IsTrue(side.OutOfPlay(500f, -401f));

            var shaft = Arena(4, RoomId.Shaft);
            Assert.IsFalse(shaft.OutOfPlay(1000f, 11000f), "deep in the 12000 px shaft");
            Assert.IsTrue(shaft.OutOfPlay(-401f, 5000f), "projectiles never wrap, even where the ship does");
            Assert.IsTrue(shaft.OutOfPlay(1920f + 401f, 5000f));

            var open = Arena(1);
            Assert.IsTrue(open.OutOfPlay(3840f + 401f, 1000f));
            Assert.IsFalse(open.OutOfPlay(-400f, -400f));
        }

        // ---- obj_surface_reflection (U.HARE-T58) ----

        [Test]
        public void OnlyTheSideScrollHasAWaterLine_250PxAboveItsBottom()
        {
            var side = Arena(5, RoomId.SideScroll);
            Assert.IsTrue(side.Reflection.Active);
            Assert.AreEqual(830f, side.Reflection.SurfaceY, "surface_y = room_height - range");
            Assert.AreEqual((0.3f, 1f, 0.5f), (side.Reflection.Alpha, side.Reflection.XScale, side.Reflection.YScale));
            Assert.AreEqual(2, side.Reflection.Definition.Depth);

            foreach (var (level, room) in new[] { (1, RoomId.GameScreen), (4, RoomId.Shaft) })
            {
                var arena = Arena(level, room);
                Assert.IsFalse(arena.Reflection.Active, room.ToString());
                Assert.IsFalse(arena.Execute(new StepReflection(P1, 500f, 2000f, 0f, 300f)).Accepted, room + " has no obj_surface_reflection");
            }
        }

        [Test]
        public void TheReflection_IsAsFarBelowTheWaterAsTheShipIsAboveIt_AtTheShipsAngle()
        {
            var side = Arena(5, RoomId.SideScroll);
            var result = side.Execute(new StepReflection(P1, 4000f, 630f, 45f, 45f));
            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(4000f, side.Reflection.X, "drawn at player.x");
            Assert.AreEqual(1030f, side.Reflection.Y, "830 + (830 - 630)");
            Assert.AreEqual(45f, side.Reflection.Angle, "angle = player.image_angle");
            Assert.IsEmpty(result.Events.ToList(), "high above the water nothing splashes or skims");

            side.Execute(new StepReflection(P1, 4000f, -200f, 90f, 90f));
            Assert.AreEqual(1860f, side.Reflection.Y, "above the room the reflection runs below it");
        }

        [Test]
        public void BelowTheWaterLine_TheShipIsPutBackOnIt_SkimmingLevel()
        {
            var side = Arena(5, RoomId.SideScroll);
            foreach (var (direction, expected) in new[] { (300f, 0f), (271f, 0f), (270f, 180f), (200f, 180f), (90f, 180f), (-30f, 0f), (630f, 180f) })
            {
                var skim = side.Execute(new StepReflection(P1, 100f, 850f, direction, direction)).Events.OfType<ShipSkimmedSurface>().Single();
                Assert.AreEqual(830f, skim.Y, "player.y = surface_y");
                Assert.AreEqual(100f, skim.X);
                Assert.AreEqual(expected, skim.Direction, $"direction {direction}: > 270 skims right (0), else left (180)");
                Assert.AreEqual(830f, side.Reflection.Y, "a ship under the water is reflected on the water line");
            }
            Assert.IsEmpty(side.Execute(new StepReflection(P1, 100f, 830f, 0f, 0f)).Events.OfType<ShipSkimmedSurface>().ToList(), "on the line is not below it");
        }

        [Test]
        public void EveryFifthStep_AShipNearTheWaterSplashesIt_32PxAhead()
        {
            var side = Arena(5, RoomId.SideScroll);
            var splashes = new List<(int Step, SurfaceSplashed Splash)>();
            for (int step = 1; step <= 15; step++)
                foreach (var e in side.Execute(new StepReflection(P1, 1000f, 800f, 180f, 180f)).Events.OfType<SurfaceSplashed>())
                    splashes.Add((step, e));
            CollectionAssert.AreEqual(new[] { 5, 10, 15 }, splashes.Select(s => s.Step).ToList(), "alarm[0] = rate (5), set again each time it rings");
            var splash = splashes[0].Splash;
            Assert.AreEqual(1000f - 32f, splash.X, 1e-3f, "player.x + lengthdir_x(32, image_angle)");
            Assert.AreEqual(830f, splash.Y, "on surface_y");
            Assert.AreEqual(2, splash.Size, "ef_firework size 2");
            Assert.AreEqual("#9ACEEB", splash.Color, "c_cornflowerblue");

            var high = Arena(5, RoomId.SideScroll);
            for (int step = 1; step <= 10; step++)
                Assert.IsEmpty(high.Execute(new StepReflection(P1, 1000f, 798f, 0f, 0f)).Events.OfType<SurfaceSplashed>().ToList(), "798 is not below 830 - 32");
        }

        [Test]
        public void TheReflectionDoesNothingBeforeCreate()
        {
            var content = ContentRegistry.Default;
            var arena = new ArenaWorld(content.Arena, content.Rooms.Get(RoomId.SideScroll), content.Arena.SettingFor(5), new ScriptedRandom());
            Assert.IsFalse(arena.Execute(new StepReflection(P1, 0f, 900f, 0f, 0f)).Accepted);
            Assert.IsFalse(arena.Reflection.Placed);
        }

        [Test]
        public void SkimSurface_TurnsTheShipsHeadingAndFacingTogether()
        {
            var rig = new PlayerRig();
            Assert.IsTrue(rig.Do(new SkimSurface(rig.Id, 180f)).Accepted);
            Assert.AreEqual(180f, rig.Flight.Direction);
            Assert.AreEqual(180f, rig.Flight.Facing, "player.image_angle = player.direction");
            Assert.AreEqual(PlayerFlightState.Moving, rig.Flight.State, "it flies on");
        }

        // ---- obj_moon_surface (U.HARE-T58) ----

        [Test]
        public void TheMoonSurface_StaysPutUntilTurnedOn_ThenRises40PxAStepIntoTheRoom()
        {
            var moon = new MoonSurface(ContentRegistry.Default.Arena.MoonSurface, 1200f);
            Assert.IsFalse(moon.On, "Create: phase = \"off\"");
            Assert.IsFalse(moon.Step(1080));
            Assert.AreEqual(1200f, moon.Y);

            moon.TurnOn();
            var arrivals = new List<int>();
            for (int step = 1; step <= 6; step++)
                if (moon.Step(1080)) arrivals.Add(step);
            // 1200 -> 1160 -> 1120 -> 1080; then y > room_height fails and it stops.
            Assert.AreEqual(1080f, moon.Y);
            CollectionAssert.AreEqual(new[] { 4 }, arrivals, "the first step it is not below the room it stops and clears the drop emitters, once");
            Assert.IsFalse(moon.On, "phase = \"off\"");
            Assert.AreEqual(0f, moon.VSpeed);
        }

        // ---- obj_player's Draw ----

        [Test]
        public void TheOffscreenArrow_Sits64PxInsideTheEdgeThePlayerLeftBy()
        {
            Assert.IsFalse(ArenaWorld.OffscreenArrow(100f, 100f, 3840, 2160, 64, out _));
            Assert.IsTrue(ArenaWorld.OffscreenArrow(-10f, 700f, 3840, 2160, 64, out var left));
            Assert.AreEqual(new Vec2(64f, 700f), left);
            ArenaWorld.OffscreenArrow(3900f, 700f, 3840, 2160, 64, out var right);
            Assert.AreEqual(new Vec2(3840f - 64f, 700f), right);
            ArenaWorld.OffscreenArrow(900f, -5f, 3840, 2160, 64, out var top);
            Assert.AreEqual(new Vec2(900f, 64f), top);
            ArenaWorld.OffscreenArrow(900f, 2200f, 3840, 2160, 64, out var bottom);
            Assert.AreEqual(new Vec2(900f, 2160f - 64f), bottom);
            ArenaWorld.OffscreenArrow(-10f, -10f, 3840, 2160, 64, out var corner);
            Assert.AreEqual(new Vec2(64f, -10f), corner, "the left edge is checked first");
        }

        [Test]
        public void TheDottedLine_Draws4PxDashesWith4PxGaps()
        {
            var dashes = ArenaWorld.DottedLine(new Vec2(0f, 0f), new Vec2(20f, 0f), 4f);
            Assert.AreEqual(3, dashes.Count, "m = 0, 8, 16");
            Assert.AreEqual(new Vec2(0f, 0f), dashes[0].From);
            Assert.AreEqual(new Vec2(4f, 0f), dashes[0].To);
            Assert.AreEqual(new Vec2(16f, 0f), dashes[2].From);
            Assert.AreEqual(new Vec2(20f, 0f), dashes[2].To);
            Assert.IsEmpty(ArenaWorld.DottedLine(new Vec2(5f, 5f), new Vec2(5f, 5f), 4f));
        }

        // ---- The player's half ----

        [Test]
        public void LeavingTheArena_CostsALifeAndFliesTheShipBackIn()
        {
            var rig = new PlayerRig();
            int lives = rig.Health.Lives;
            Assert.IsTrue(rig.Do(new LeaveArena(rig.Id)).Accepted);
            Assert.AreEqual(lives - 1, rig.Health.Lives);
            Assert.AreEqual(lives - 1, rig.Seen<PlayerLeftArena>().Single().Lives);
            Assert.AreEqual(1, rig.Seen<PlayerRespawned>().Count(), "reset_player");
            Assert.AreEqual(PlayerFlightState.Moving, rig.Flight.State);
            Assert.IsTrue(rig.Health.IsSpawnProtected, "justspawned");
            Assert.AreEqual(rig.Health.MaxHealth, rig.Health.Health);
        }

        [Test]
        public void LeavingTheArenaWithNoLivesLeft_RestartsTheGame()
        {
            var rig = new PlayerRig();
            while (rig.Health.Lives > 0) rig.Do(new LeaveArena(rig.Id));
            Assert.IsTrue(rig.Do(new LeaveArena(rig.Id)).Accepted);
            Assert.AreEqual(1, rig.Seen<GameEnded>().Count(), "game_restart");
            Assert.IsTrue(rig.Health.IsGameOver);
            Assert.IsFalse(rig.Do(new LeaveArena(rig.Id)).Accepted);
        }

        [Test]
        public void ThePerchedShip_CanLeapOnlyWithinTheJumpArc()
        {
            var rig = new PlayerRig();
            Assert.IsFalse(rig.Flight.CanLeap(rig.Flight.Direction), "not in flight");
            rig.Landed();
            float facing = rig.Flight.Direction;
            Assert.IsTrue(rig.Flight.CanLeap(facing + 100f));
            Assert.IsFalse(rig.Flight.CanLeap(facing + 101f));
            Assert.IsTrue(rig.Flight.CanLeap(facing - 100f));
            Assert.AreEqual(0f, rig.Flight.Spin, "landing stops the spin");
        }

        // ---- Data ----

        [Test]
        public void TheShippedArenaSection_MatchesTheDefaults()
        {
            var fromFile = ContentRegistry.FromJson(File.ReadAllText(ShippedGameContentJsonPath())).Arena;
            var defaults = ContentRegistry.Default.Arena;
            Assert.AreEqual(defaults.BufferZone, fromFile.BufferZone);
            Assert.AreEqual(defaults.PlayerSize, fromFile.PlayerSize);
            Assert.AreEqual(defaults.ShowMapOnStart, fromFile.ShowMapOnStart);
            Assert.AreEqual(defaults.LeapLineColor, fromFile.LeapLineColor);
            Assert.AreEqual(defaults.Rooms.Length, fromFile.Rooms.Length);
            for (int i = 0; i < defaults.Rooms.Length; i++)
            {
                var a = defaults.Rooms[i];
                var b = fromFile.Rooms[i];
                Assert.AreEqual(a.Room, b.Room);
                CollectionAssert.AreEqual(a.Backgrounds.Select(s => (s.Name, s.Visible, s.HTiled, s.VTiled, s.Stretch)).ToList(),
                    b.Backgrounds.Select(s => (s.Name, s.Visible, s.HTiled, s.VTiled, s.Stretch)).ToList(), a.Room.ToString());
                foreach (var (x, y) in new[] { (a.View, b.View), (a.Minimap, b.Minimap) })
                    Assert.AreEqual((x.Width, x.Height, x.PortX, x.PortY, x.PortWidth, x.PortHeight, x.HBorder, x.VBorder, x.HSpeed, x.VSpeed),
                        (y.Width, y.Height, y.PortX, y.PortY, y.PortWidth, y.PortHeight, y.HBorder, y.VBorder, y.HSpeed, y.VSpeed), a.Room.ToString());
                Assert.AreEqual(
                    (a.Reflection.Enabled, a.Reflection.Range, a.Reflection.Alpha, a.Reflection.XScale, a.Reflection.YScale, a.Reflection.SplashRateSteps,
                        a.Reflection.SplashBand, a.Reflection.SplashLead, a.Reflection.SplashSize, a.Reflection.SplashColor, a.Reflection.Depth),
                    (b.Reflection.Enabled, b.Reflection.Range, b.Reflection.Alpha, b.Reflection.XScale, b.Reflection.YScale, b.Reflection.SplashRateSteps,
                        b.Reflection.SplashBand, b.Reflection.SplashLead, b.Reflection.SplashSize, b.Reflection.SplashColor, b.Reflection.Depth),
                    a.Room + "'s obj_surface_reflection");
            }
            Assert.AreEqual((defaults.MoonSurface.Sprite, defaults.MoonSurface.Depth, defaults.MoonSurface.RiseSpeed),
                (fromFile.MoonSurface.Sprite, fromFile.MoonSurface.Depth, fromFile.MoonSurface.RiseSpeed));
            var all = defaults.Levels.Concat(new[] { defaults.DefaultLevel, defaults.GeneratorLevel }).ToList();
            var loaded = fromFile.Levels.Concat(new[] { fromFile.DefaultLevel, fromFile.GeneratorLevel }).ToList();
            CollectionAssert.AreEqual(
                all.Select(l => (l.Level, l.Script, l.Name, l.Room, l.Background, l.Overlay, l.HSpeed, l.VSpeed, l.Tiled, l.LoopHorizontal, l.LoopVertical, l.RandomizeOnLoop, l.StructureMin, l.StructureMax)).ToList(),
                loaded.Select(l => (l.Level, l.Script, l.Name, l.Room, l.Background, l.Overlay, l.HSpeed, l.VSpeed, l.Tiled, l.LoopHorizontal, l.LoopVertical, l.RandomizeOnLoop, l.StructureMin, l.StructureMax)).ToList());
        }

        [Test]
        public void EachLevelPlacesTheOriginalsStructures()
        {
            // U.HARE-T56: the level scripts' structure_min / structure_max (setup_level resets both to 0 first).
            var tuning = ContentRegistry.Default.Arena;
            var expected = new Dictionary<int, (int Min, int Max)> { [2] = (1, 3), [4] = (1, 3), [7] = (2, 4) };
            for (int level = 1; level <= 10; level++)
            {
                var setting = tuning.SettingFor(level);
                var range = expected.TryGetValue(level, out var r) ? r : (0, 0);
                Assert.AreEqual(range, (setting.StructureMin, setting.StructureMax), setting.Script);
            }
            Assert.AreEqual((0, 0), (tuning.DefaultLevel.StructureMin, tuning.DefaultLevel.StructureMax), "level_default");
            Assert.AreEqual((0, 0), (tuning.GeneratorLevel.StructureMin, tuning.GeneratorLevel.StructureMax), "level_enemygenerator");

            var e = Assert.Throws<ContentValidationException>(() => ContentRegistry.FromJson(
                "{ \"arena\": { \"Levels\": [ { \"Level\": 1, \"Script\": \"a\", \"StructureMin\": 3, \"StructureMax\": 1 }, { \"Level\": 2, \"Script\": \"b\", \"StructureMin\": -1, \"StructureMax\": 0 } ] } }"));
            StringAssert.Contains("arena.Levels[0].StructureMax", e.Message);
            StringAssert.Contains("arena.Levels[1].StructureMin", e.Message);
        }

        [Test]
        public void ABadArenaSection_FailsValidation()
        {
            var e = Assert.Throws<ContentValidationException>(() => ContentRegistry.FromJson(
                "{ \"arena\": { \"BufferZone\": -1, \"LeapLineColor\": \"blue\", \"Levels\": [ { \"Level\": 1, \"Script\": \"a\", \"Room\": \"Title\" }, { \"Level\": 1, \"Script\": \"b\" } ] } }"));
            string message = e.Message;
            StringAssert.Contains("arena.BufferZone", message);
            StringAssert.Contains("arena.LeapLineColor", message);
            StringAssert.Contains("arena.Levels[0].Room", message);
            StringAssert.Contains("arena.Levels[1].Level", message);
        }

        static string ShippedGameContentJsonPath([CallerFilePath] string here = "")
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here), "..", ".."));
            return Path.Combine(repoRoot, "Assets", "Unity.Rea.Hare", "Resources", "Hare", "Content", "GameContent.json");
        }
    }
}
