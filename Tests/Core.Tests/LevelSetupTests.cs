using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using Unity.Hare.Arena;
using Unity.Hare.Content;
using Unity.Hare.Flow;
using Unity.Hare.Player;
using Unity.Hare.Session;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.Core
{
    /// <summary>
    /// setup_level and the objective engine (U.HARE-T57): which script runs for the game mode and level and the room it
    /// sends the game to, obj_levelstart's alarms and fade, level_name and the sticky stationary_percent, the elapsed-time
    /// clock on room steps, the four objective kinds and set_level_complete, and the new obj_player setup_level makes.
    /// </summary>
    public class LevelSetupTests
    {
        static readonly PlayerId P1 = new PlayerId(1);
        const float Step = 1f / GameClock.StepsPerSecond;

        static ArenaTuning Arena => ContentRegistry.Default.Arena;

        static GameSession NewSession() => new GameSession(ContentRegistry.Default);

        // ---- Which script, which room ----

        [Test]
        public void QuickstartRunsTheLevelScriptsThenLevelDefault()
        {
            Assert.AreEqual("level_01", LevelSetup.ScriptFor(Arena, GameMode.Quickstart, 1).Script);
            Assert.AreEqual("level_10", LevelSetup.ScriptFor(Arena, GameMode.Quickstart, 10).Script);
            Assert.AreEqual("level_default", LevelSetup.ScriptFor(Arena, GameMode.Quickstart, 11).Script);
            Assert.AreEqual("level_02", LevelSetup.ScriptFor(Arena, GameMode.Campaign, 2).Script, "load_scenario is U.HARE-T64's; scenario 1 plays level_01..05");
        }

        [Test]
        public void GeneratorModeRunsLevelEnemyGeneratorWhateverTheLevel()
        {
            Assert.AreEqual("level_enemygenerator", LevelSetup.ScriptFor(Arena, GameMode.Generator, 1).Script);
            Assert.AreEqual("level_enemygenerator", LevelSetup.ScriptFor(Arena, GameMode.Generator, 7).Script);
            Assert.AreEqual(ObjectiveKind.Survive, LevelSetup.ScriptFor(Arena, GameMode.Generator, 7).Objective.Kind);
        }

        [Test]
        public void ALevelScriptSendsTheGameToItsOwnRoom()
        {
            Assert.IsNull(LevelSetup.RoomGoto(Arena, GameMode.Quickstart, 1, RoomId.GameScreen));
            Assert.AreEqual(RoomId.Shaft, LevelSetup.RoomGoto(Arena, GameMode.Quickstart, 4, RoomId.GameScreen), "level_04: room_goto(room_shaft)");
            Assert.AreEqual(RoomId.SideScroll, LevelSetup.RoomGoto(Arena, GameMode.Quickstart, 5, RoomId.Shaft), "level_05: room_goto(room_sidescroll)");
            Assert.AreEqual(RoomId.GameScreen, LevelSetup.RoomGoto(Arena, GameMode.Quickstart, 6, RoomId.SideScroll));
            Assert.IsNull(LevelSetup.RoomGoto(Arena, GameMode.Generator, 4, RoomId.GameScreen));
        }

        [Test]
        public void ARoomOpenedOnItsOwnPlaysTheFirstLevelThatPlaysThere()
        {
            Assert.AreEqual("level_04", LevelSetup.SettingFor(Arena, GameMode.Quickstart, 1, RoomId.Shaft).Script);
            Assert.AreEqual("level_05", LevelSetup.SettingFor(Arena, GameMode.Quickstart, 1, RoomId.SideScroll).Script);
            Assert.AreEqual("level_03", LevelSetup.SettingFor(Arena, GameMode.Quickstart, 3, RoomId.GameScreen).Script);
        }

        // ---- obj_levelstart ----

        [Test]
        public void TheBannerHoldsFor120StepsThenFadesAHundredthEveryOtherStep()
        {
            var banner = new LevelStartBanner("Hydras & Saws", "Accumulate 500 points", Arena);
            for (int i = 0; i < 119; i++) Assert.IsTrue(banner.Step());
            Assert.IsFalse(banner.Fading, "alarm[0] = 120");
            Assert.AreEqual(1d, banner.Alpha);

            Assert.IsTrue(banner.Step()); // step 120: Alarm 0 (fade = true), then Alarm 1 (its 60th firing) takes the first 0.01
            Assert.IsTrue(banner.Fading);
            Assert.AreEqual(0.99d, banner.Alpha, 1e-12);
            Assert.IsTrue(banner.Step());
            Assert.AreEqual(0.99d, banner.Alpha, 1e-12, "rate = 2: nothing on the odd step");
            Assert.IsTrue(banner.Step());
            Assert.AreEqual(0.98d, banner.Alpha, 1e-12);
        }

        [Test]
        public void TheBannerGoesOnceItsDoubleAlphaIsNoLongerAboveZero()
        {
            // GameMaker's alpha is a double: subtract 0.01 from 1 until it is not above 0, then one more firing destroys it.
            double alpha = 1d;
            int firings = 0;
            while (alpha > 0d) { alpha -= 0.01; firings++; }
            int goneAt = 120 + 2 * firings;

            var banner = new LevelStartBanner("", "", Arena);
            int steps = 0;
            while (banner.Step()) steps++;
            Assert.AreEqual(goneAt, steps + 1, "destroyed on the Alarm 1 after the last subtraction");
            Assert.IsTrue(banner.Gone);
            Assert.AreEqual(alpha, banner.Alpha);
            Assert.AreEqual(0f, banner.DrawAlpha, "draw_set_alpha holds it at 0");
            Assert.IsFalse(banner.Step());
        }

        // ---- level_name, stationary_percent ----

        [Test]
        public void ALevelScriptNamesItsLevelAndStationaryPercentSticks()
        {
            var session = NewSession();
            Assert.AreEqual("Welcome to H.O.P", session.LevelName);
            Assert.AreEqual(50, session.StationaryPercent);

            var level3 = Arena.SettingFor(3);
            var set = session.Execute(new BeginLevel(P1, level3.Objective.ToObjective(), level3.Name, level3.StationaryPercent)).Find<ObjectiveSet>();
            Assert.AreEqual("Sky Worms & Daggers", set.LevelName);
            Assert.AreEqual(15, session.StationaryPercent);

            var level4 = Arena.SettingFor(4);
            session.Execute(new BeginLevel(P1, level4.Objective.ToObjective(), level4.Name, level4.StationaryPercent));
            Assert.AreEqual("Storm Shaft", session.LevelName);
            Assert.AreEqual(15, session.StationaryPercent, "level_04 has no stationary_percent line");
            Assert.AreEqual(level4.StationaryPercentAfter(15), session.StationaryPercent);

            session.Execute(new RestartGame(P1));
            Assert.AreEqual("Welcome to H.O.P", session.LevelName, "game_restart runs globals.gml again");
            Assert.AreEqual(50, session.StationaryPercent);
        }

        // ---- The clock and the objectives ----

        static GameSession Begin(LevelSetting level)
        {
            var session = NewSession();
            session.Execute(new BeginLevel(P1, level.Objective.ToObjective(), level.Name, level.StationaryPercent));
            return session;
        }

        static List<SessionEvent> RoomSteps(GameSession session, int steps)
        {
            var events = new List<SessionEvent>();
            for (int i = 0; i < steps; i++) events.AddRange(session.Execute(new TickSession(P1, Step)).Events);
            return events;
        }

        [Test]
        public void ASecondIs120RoomSteps()
        {
            var session = Begin(Arena.SettingFor(1));
            RoomSteps(session, 119);
            Assert.AreEqual(0, session.TimeElapsedSeconds);
            RoomSteps(session, 1);
            Assert.AreEqual(1, session.TimeElapsedSeconds, "obj_healthbar's alarm[0] = room_speed");
            RoomSteps(session, 120 * 59);
            Assert.AreEqual(60, session.TimeElapsedSeconds, "a minute of room steps is 60 seconds, not 59");
        }

        [Test]
        public void PointsCompleteTheLevelAtTheGoal()
        {
            var session = Begin(Arena.SettingFor(1));
            Assert.AreEqual(ObjectiveKind.Points, session.Objective);
            Assert.AreEqual("Accumulate 500 points", session.ObjectiveText);
            Assert.IsNull(session.Execute(new AddScore(P1, 499)).Find<LevelCompleted>());
            var done = session.Execute(new AddScore(P1, 1));
            Assert.IsNotNull(done.Find<LevelCompleted>(), "current_score >= goal_score: set_level_complete");
            Assert.IsTrue(done.Find<PausedChanged>().Paused, "set_level_complete: paused = true");
            Assert.IsTrue(session.IsLevelComplete);
        }

        [Test]
        public void ATimeLimitBeepsEachOfItsLastTenSecondsThenCompletes()
        {
            var session = Begin(Arena.SettingFor(5));
            Assert.AreEqual("Survive 1 minute, 30 seconds", session.ObjectiveText, "set_time_limit_string");

            var events = RoomSteps(session, 120 * 79);
            Assert.IsEmpty(events.OfType<TimeLimitWarning>().ToList());
            events = RoomSteps(session, 120 * 10);
            CollectionAssert.AreEqual(Enumerable.Range(1, 10).Reverse().ToArray(), events.OfType<TimeLimitWarning>().Select(w => w.SecondsLeft).ToArray(),
                "sfx_beep at 10..1 seconds left");
            Assert.IsFalse(session.IsLevelComplete);
            events = RoomSteps(session, 120);
            Assert.AreEqual(1, events.OfType<LevelCompleted>().Count(), "time_elapsed_seconds >= time_limit_seconds");
            Assert.IsEmpty(events.OfType<TimeLimitWarning>().ToList(), "no beep at 0 left");
        }

        [Test]
        public void ThePausedClockDoesNotCount()
        {
            var session = Begin(Arena.SettingFor(5));
            session.Execute(new SetPaused(P1, true));
            RoomSteps(session, 120 * 5);
            Assert.AreEqual(0, session.TimeElapsedSeconds, "obj_healthbar: if(!paused) time_elapsed_seconds += 1");
        }

        [Test]
        public void DefeatCompletesWhenTheTargetIsGone()
        {
            var session = Begin(Arena.SettingFor(8));
            Assert.AreEqual("Defeat Scrap Mental", session.ObjectiveText);
            RoomSteps(session, 600);
            Assert.IsFalse(session.IsLevelComplete);
            Assert.IsNotNull(session.Execute(new DefeatTarget(P1)).Find<LevelCompleted>(), "!instance_exists(target_id)");
        }

        [Test]
        public void SurviveHasNoWinConditionButTheWormholeEmittersEnd()
        {
            var session = Begin(Arena.GeneratorLevel);
            Assert.AreEqual("Survive the Alien Onslaught!", session.ObjectiveText);
            session.Execute(new AddScore(P1, 100000));
            RoomSteps(session, 120 * 120);
            Assert.IsFalse(session.IsLevelComplete, "o_survive: obj_game checks nothing");
            Assert.IsTrue(session.Execute(new CompleteLevel(P1)).Accepted, "obj_wormhole_emitter's set_level_complete");
            Assert.IsFalse(session.Execute(new CompleteLevel(P1)).Accepted, "if(!level_complete)");
        }

        [Test]
        public void BeginningALevelUnpausesAndClearsLevelComplete()
        {
            var session = Begin(Arena.SettingFor(1));
            session.Execute(new AddScore(P1, 500));
            Assert.IsTrue(session.IsPaused);
            var level2 = Arena.SettingFor(2);
            var result = session.Execute(new BeginLevel(P1, level2.Objective.ToObjective(), level2.Name, level2.StationaryPercent));
            Assert.IsFalse(session.IsPaused, "setup_level: paused = false");
            Assert.IsFalse(session.IsLevelComplete, "level_complete = false");
            Assert.AreEqual(0, session.Score, "level_02: current_score = 0");
            Assert.IsNull(result.Find<LevelCompleted>());
        }

        // ---- The new obj_player ----

        [Test]
        public void SetUpShipIsANewObjPlayerFlownInByResetPlayer()
        {
            var rig = new PlayerRig();
            var session = NewSession();
            rig.Do(new UseLives(rig.Id, new SessionLives(() => session, rig.Id, null)));
            rig.Ammo.SetCurrent(AmmoPool.GroundPrimary, 3);
            rig.Ammo.SetCurrent(AmmoPool.AirPrimary, 0);
            rig.Do(new TickHealth(rig.Id, 2f));
            while (!rig.Health.IsDead)
            {
                rig.Do(new TakeHit(rig.Id));
                rig.Do(new TickHealth(rig.Id, 2f));
            }
            int lives = session.Lives;
            rig.Events.Clear();

            Assert.IsTrue(rig.Do(new SetUpShip(rig.Id)).Accepted);
            Assert.IsFalse(rig.Health.IsDead);
            Assert.AreEqual(rig.Health.MaxHealth, rig.Health.Health, "reset_player: HP = MAXHP");
            Assert.IsTrue(rig.Health.IsSpawnProtected, "justspawned");
            Assert.AreEqual(1, rig.Seen<PlayerRespawned>().Count());
            float heading = rig.Seen<PlayerRespawned>().Single().Direction;
            Assert.That(heading, Is.InRange(240f, 300f), "270 + irandom_range(-30, 30)");
            Assert.AreEqual(PlayerFlightState.Moving, rig.Flight.State);
            Assert.AreEqual(150, rig.Ammo.Current(AmmoPool.GroundPrimary), "Create: ground_ammo_primary = 150");
            Assert.AreEqual(20, rig.Ammo.Current(AmmoPool.AirPrimary), "Create: air_ammo_primary = 20");
            Assert.AreEqual(lives, session.Lives, "LIVES is a global the new obj_player shares");
        }

        [Test]
        public void SetUpShipEndsAGameOverShip()
        {
            var rig = new PlayerRig();
            var session = NewSession();
            rig.Do(new UseLives(rig.Id, new SessionLives(() => session, rig.Id, null)));
            for (int i = 0; i < 10 && !rig.Health.IsGameOver; i++) rig.Do(new LeaveArena(rig.Id));
            Assert.IsTrue(rig.Health.IsGameOver);
            rig.Do(new SetUpShip(rig.Id));
            Assert.IsFalse(rig.Health.IsGameOver);
            Assert.IsFalse(rig.Health.IsDead);
        }
    }
}
