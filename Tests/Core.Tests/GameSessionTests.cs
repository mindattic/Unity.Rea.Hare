using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using Unity.Rea.Hare.Content;
using Unity.Rea.Hare.Session;

namespace Unity.Rea.Hare.Tests.Core
{
    /// <summary>globals.gml's session state and the rules that change it (obj_game, obj_coin, player_hurt, the menus).</summary>
    public class GameSessionTests
    {
        static readonly PlayerId P1 = new PlayerId(1);
        const float Second = 1f; // obj_healthbar's one_second = room_speed steps

        GameSession session;

        [SetUp]
        public void SetUp() => session = new GameSession(ContentRegistry.Default);

        SessionResult Do(SessionCommand command) => session.Execute(command);

        [Test]
        public void ANewSessionHoldsGlobalsGmlsValues()
        {
            Assert.AreEqual(3, session.Lives);
            Assert.AreEqual(0, session.Coins);
            Assert.AreEqual(5, session.SkillPoints);
            Assert.AreEqual(0, session.Kills);
            Assert.AreEqual(0, session.Score);
            Assert.AreEqual(0, session.GoalScore);
            Assert.AreEqual(100, session.Difficulty);
            Assert.IsFalse(session.UseMetric);
            Assert.AreEqual(1f, session.MasterVolume);
            Assert.AreEqual(GameMode.Quickstart, session.Mode);
            Assert.AreEqual(ObjectiveKind.Points, session.Objective);
            Assert.AreEqual("Boss", session.TargetName);
            Assert.AreEqual(0, session.TimeLimitSeconds);
            Assert.AreEqual(0, session.TimeElapsedSeconds);
            Assert.AreEqual(-1, session.Scenario);
            Assert.AreEqual(1, session.Level);
            Assert.IsFalse(session.IsLevelComplete);
            Assert.IsFalse(session.IsPaused);
        }

        [Test]
        public void TheGmlEnumValuesAreKept()
        {
            Assert.AreEqual(1, (int)GameMode.Quickstart);
            Assert.AreEqual(2, (int)GameMode.Campaign);
            Assert.AreEqual(3, (int)GameMode.Generator);
            Assert.AreEqual(1, (int)ObjectiveKind.Points);
            Assert.AreEqual(2, (int)ObjectiveKind.TimeLimit);
            Assert.AreEqual(3, (int)ObjectiveKind.Defeat);
            Assert.AreEqual(4, (int)ObjectiveKind.Survive);
        }

        [Test]
        public void SessionsAreIndependent()
        {
            var other = new GameSession(ContentRegistry.Default);
            Do(new AddScore(P1, 50));
            Assert.AreEqual(50, session.Score);
            Assert.AreEqual(0, other.Score, "no shared global state");
        }

        [Test]
        public void EventsCarryTheIssuingSeat()
        {
            var p2 = new PlayerId(2);
            var result = session.Execute(new AddScore(p2, 10));
            Assert.AreEqual(p2, result.Find<ScoreChanged>().Id);
        }

        // ---- Score and the points objective ----

        [Test]
        public void AddScoreRaisesScoreChanged()
        {
            Do(new BeginLevel(P1, LevelObjective.Points(500)));
            var result = Do(new AddScore(P1, 10));
            var changed = result.Find<ScoreChanged>();
            Assert.AreEqual(10, changed.Score);
            Assert.AreEqual(10, changed.Delta);
            Assert.IsNull(result.Find<LevelCompleted>());
        }

        [Test]
        public void ReachingTheGoalScoreCompletesTheLevelOnceAndPauses()
        {
            Do(new BeginLevel(P1, LevelObjective.Points(500)));
            Do(new AddScore(P1, 490));
            var result = Do(new AddScore(P1, 10));
            Assert.IsNotNull(result.Find<LevelCompleted>());
            Assert.IsTrue(session.IsLevelComplete);
            Assert.IsTrue(session.IsPaused, "set_level_complete pauses play");
            Assert.IsTrue(result.Find<PausedChanged>().Paused);

            var again = Do(new AddScore(P1, 50));
            Assert.IsNull(again.Find<LevelCompleted>(), "set_level_complete is guarded by level_complete");
            Assert.IsFalse(Do(new CompleteLevel(P1)).Accepted);
        }

        [Test]
        public void BeginLevelZeroesScoreAndClockAndSetsTheObjectiveText()
        {
            Do(new AddScore(P1, 300));
            Do(new TickSession(P1, 3 * Second));
            var result = Do(new BeginLevel(P1, LevelObjective.Points(500)));
            Assert.AreEqual(0, session.Score);
            Assert.AreEqual(0, session.TimeElapsedSeconds);
            Assert.AreEqual(-300, result.Find<ScoreChanged>().Delta);
            Assert.AreEqual("Accumulate 500 points", result.Find<ObjectiveSet>().Text);
            Assert.AreEqual("Accumulate 500 points", session.ObjectiveText);
        }

        [Test]
        public void Level10KeepsTheScoreTheLevelBeforeLeft()
        {
            Do(new AddScore(P1, 120));
            Do(new BeginLevel(P1, LevelObjective.Points(18000, resetScore: false)));
            Assert.AreEqual(120, session.Score);
        }

        [Test]
        public void BeginLevelClearsLevelCompleteAndPause()
        {
            Do(new BeginLevel(P1, LevelObjective.Points(10)));
            Do(new AddScore(P1, 10));
            Assert.IsTrue(session.IsLevelComplete);
            Do(new BeginLevel(P1, LevelObjective.Points(500)));
            Assert.IsFalse(session.IsLevelComplete);
            Assert.IsFalse(session.IsPaused);
        }

        // ---- Time limit ----

        [Test]
        public void TheClockCountsWholeSecondsOnlyWhileUnpaused()
        {
            Do(new BeginLevel(P1, LevelObjective.Survive()));
            var result = Do(new TickSession(P1, 2.5f * Second));
            Assert.AreEqual(2, session.TimeElapsedSeconds);
            Assert.AreEqual(2, result.Events.OfType<SecondElapsed>().Count());

            Do(new SetPaused(P1, true));
            Do(new TickSession(P1, 5 * Second));
            Assert.AreEqual(2, session.TimeElapsedSeconds, "obj_healthbar skips its count while paused");

            Do(new SetPaused(P1, false));
            Do(new TickSession(P1, 0.5f * Second));
            Assert.AreEqual(3, session.TimeElapsedSeconds, "the alarm kept cycling, so its half second carried");
        }

        [Test]
        public void ATimeLimitWarnsEachOfItsLastTenSecondsThenCompletes()
        {
            Do(new BeginLevel(P1, LevelObjective.TimeLimit(90, 500)));
            Assert.AreEqual("Survive 1 minute, 30 seconds", session.ObjectiveText);

            var warnings = Enumerable.Range(0, 89)
                .SelectMany(_ => Do(new TickSession(P1, Second)).Events.OfType<TimeLimitWarning>())
                .Select(w => w.SecondsLeft).ToArray();
            CollectionAssert.AreEqual(new[] { 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 }, warnings);
            Assert.IsFalse(session.IsLevelComplete);

            var last = Do(new TickSession(P1, Second));
            Assert.IsNotNull(last.Find<LevelCompleted>());
            Assert.IsNull(last.Find<TimeLimitWarning>());
        }

        [Test]
        public void ATimeLimitIgnoresScore()
        {
            Do(new BeginLevel(P1, LevelObjective.TimeLimit(90, 500)));
            Do(new AddScore(P1, 1000));
            Assert.IsFalse(session.IsLevelComplete);
        }

        // ---- Defeat and survive ----

        [Test]
        public void DefeatCompletesWhenTheTargetIsGone()
        {
            var result = Do(new BeginLevel(P1, LevelObjective.Defeat("Scrap Mental", 500)));
            Assert.AreEqual("Defeat Scrap Mental", result.Find<ObjectiveSet>().Text);
            Do(new AddScore(P1, 1000));
            Do(new TickSession(P1, Second));
            Assert.IsFalse(session.IsLevelComplete);

            Assert.IsNotNull(Do(new DefeatTarget(P1)).Find<LevelCompleted>());
        }

        [Test]
        public void SurviveNeverCompletes()
        {
            Do(new BeginLevel(P1, LevelObjective.Survive()));
            Assert.AreEqual("Survive the Alien Onslaught!", session.ObjectiveText);
            Do(new AddScore(P1, 100000));
            Do(new TickSession(P1, 600 * Second));
            Assert.IsFalse(session.IsLevelComplete);
        }

        [TestCase(90, "1 minute, 30 seconds")]
        [TestCase(61, "1 minute, 1 second")]
        [TestCase(150, "2 minutes, 30 seconds")]
        [TestCase(45, "45 seconds")]
        [TestCase(1, "1 second")]
        [TestCase(120, "2 minutes")]
        [TestCase(60, "1 minute")]
        public void TimeLimitTextIsWrittenOut(int seconds, string text) =>
            Assert.AreEqual(text, GameSession.DescribeTimeLimit(seconds));

        // ---- Levels ----

        [Test]
        public void FlyingOffTheTopZeroesTheScoreAndAdvances()
        {
            Do(new BeginLevel(P1, LevelObjective.Points(10)));
            Do(new AddScore(P1, 10));
            var result = Do(new AdvanceLevel(P1, resetScore: true));
            Assert.AreEqual(2, session.Level);
            Assert.AreEqual(0, session.Score);
            Assert.IsFalse(session.IsLevelComplete);
            Assert.AreEqual(2, result.Find<LevelAdvanced>().Level);
        }

        [Test]
        public void TheFadeAdvancesWithoutTouchingTheScore()
        {
            Do(new AddScore(P1, 40));
            Do(new AdvanceLevel(P1, resetScore: false));
            Assert.AreEqual(2, session.Level);
            Assert.AreEqual(40, session.Score);
        }

        // ---- Lives, coins, skill points, kills ----

        [Test]
        public void LosingALifeRaisesLifeLost()
        {
            var result = Do(new LoseLife(P1));
            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(2, session.Lives);
            Assert.AreEqual(2, result.Find<LifeLost>().Lives);
            Assert.IsTrue(session.HasLivesLeft);
        }

        [Test]
        public void LosingALifeWithNoneLeftRestartsTheGame()
        {
            Do(new LoseLife(P1));
            Do(new LoseLife(P1));
            Do(new LoseLife(P1));
            Assert.AreEqual(0, session.Lives);
            Assert.IsFalse(session.HasLivesLeft);
            Do(new AddScore(P1, 70));

            var result = Do(new LoseLife(P1));
            Assert.IsFalse(result.Accepted);
            Assert.IsInstanceOf<GameOver>(result.Events[0]);
            Assert.IsInstanceOf<SessionReset>(result.Events[1]);
            Assert.AreEqual(3, session.Lives);
            Assert.AreEqual(0, session.Score);
        }

        [Test]
        public void TheHundredthCoinIsAnExtraLife()
        {
            Do(new CollectCoin(P1, 50));
            Do(new CollectCoin(P1, 45));
            Assert.AreEqual(95, session.Coins);
            var result = Do(new CollectCoin(P1, 10));
            Assert.AreEqual(0, session.Coins, "obj_coin zeroes COINS, the overflow is lost");
            Assert.AreEqual(4, session.Lives);
            Assert.AreEqual(4, result.Find<ExtraLifeEarned>().Lives);
            Assert.AreEqual(0, result.Find<CoinsChanged>().Coins);
        }

        [Test]
        public void SkillPointsAreSpentUntilNoneAreLeft()
        {
            for (int i = 4; i >= 0; i--)
            {
                var result = Do(new SpendSkillPoint(P1));
                Assert.IsTrue(result.Accepted);
                Assert.AreEqual(i, result.Find<SkillPointsChanged>().SkillPoints);
            }
            var refused = Do(new SpendSkillPoint(P1));
            Assert.IsFalse(refused.Accepted);
            Assert.AreEqual(0, refused.Events.Count);
            Assert.AreEqual(0, session.SkillPoints);
        }

        [Test]
        public void KillsAreCounted()
        {
            Do(new RecordKill(P1));
            var result = Do(new RecordKill(P1));
            Assert.AreEqual(2, session.Kills);
            Assert.AreEqual(2, result.Find<KillsChanged>().Kills);
        }

        // ---- Menus ----

        [Test]
        public void QuickstartPicksALevelAndClearsTheScenario()
        {
            Do(new SelectScenario(P1, 2));
            var result = Do(new ChooseQuickstart(P1, 5));
            Assert.AreEqual(GameMode.Quickstart, session.Mode);
            Assert.AreEqual(-1, session.Scenario);
            Assert.AreEqual(5, session.Level);
            Assert.AreEqual(5, result.Find<GameModeChosen>().Level);
        }

        [Test]
        public void CampaignStartsAtLevelOne()
        {
            Do(new ChooseQuickstart(P1, 7));
            Do(new ChooseCampaign(P1));
            Assert.AreEqual(GameMode.Campaign, session.Mode);
            Assert.AreEqual(-1, session.Scenario);
            Assert.AreEqual(1, session.Level);
        }

        [Test]
        public void TheGeneratorSetsOnlyModeAndDifficulty()
        {
            Do(new ChooseQuickstart(P1, 4));
            Do(new ChooseGenerator(P1, 0));
            Assert.AreEqual(GameMode.Generator, session.Mode);
            Assert.AreEqual(0, session.Difficulty);
            Assert.AreEqual(4, session.Level);

            Do(new ChooseGenerator(P1, null));
            Assert.AreEqual(0, session.Difficulty, "obj_btn_level09 leaves difficulty alone");
        }

        [Test]
        public void ScenarioSelectionRaisesScenarioChanged() =>
            Assert.AreEqual(3, Do(new SelectScenario(P1, 3)).Find<ScenarioChanged>().Scenario);

        // ---- Settings ----

        [Test]
        public void MasterVolumeStepsByHundredthsWithinZeroAndOne()
        {
            Assert.IsNull(Do(new StepMasterVolume(P1, +1)).Find<MasterVolumeChanged>(), "already at 1");
            for (int i = 0; i < 150; i++) Do(new StepMasterVolume(P1, -1));
            Assert.AreEqual(0f, session.MasterVolume);
            var up = Do(new StepMasterVolume(P1, +1));
            Assert.AreEqual(0.01f, up.Find<MasterVolumeChanged>().Volume, 1e-6f);
            for (int i = 0; i < 150; i++) Do(new StepMasterVolume(P1, +1));
            Assert.AreEqual(1f, session.MasterVolume);
        }

        [Test]
        public void UToggleSwitchesUnits()
        {
            Assert.IsTrue(Do(new ToggleUnits(P1)).Find<UnitsChanged>().UseMetric);
            Assert.IsTrue(session.UseMetric);
            Do(new ToggleUnits(P1));
            Assert.IsFalse(session.UseMetric);
            Assert.IsTrue(Do(new SetUnits(P1, true)).Find<UnitsChanged>().UseMetric, "the profile's saved units");
            Assert.IsNull(Do(new SetUnits(P1, true)).Find<UnitsChanged>(), "already metric");
        }

        [Test]
        public void PTogglesPaused_ButNotOnceTheLevelIsComplete()
        {
            // U.HARE-T60, obj_game's P key press: if(!level_complete) paused = !paused.
            var pause = Do(new TogglePause(P1));
            Assert.IsTrue(pause.Accepted);
            Assert.IsTrue(pause.Find<PausedChanged>().Paused);
            Assert.IsTrue(session.IsPaused);
            Assert.IsFalse(Do(new TogglePause(P1)).Find<PausedChanged>().Paused);
            Assert.IsFalse(session.IsPaused);

            // set_level_complete pauses for the results screen; P cannot lift it (only the results' Enter does).
            Do(new CompleteLevel(P1));
            Assert.IsTrue(session.IsPaused);
            var refused = Do(new TogglePause(P1));
            Assert.IsFalse(refused.Accepted);
            Assert.IsNull(refused.Find<PausedChanged>());
            Assert.IsTrue(session.IsPaused);
        }

        [Test]
        public void F12TogglesTheDebugOverlay()
        {
            Assert.IsFalse(session.Debug, "globals.gml: debug = false");
            Assert.AreEqual("Pinball Spring", session.TrackName, "globals.gml: track_name = \"Pinball Spring\"");
            Assert.IsTrue(Do(new ToggleDebug(P1)).Find<DebugChanged>().Debug);
            Assert.IsTrue(session.Debug);
            Do(new ToggleDebug(P1));
            Assert.IsFalse(session.Debug);
        }

        /// <summary>A random that answers <see cref="Roll"/> and remembers the range it was asked for.</summary>
        sealed class FixedRoll : IRandomSource
        {
            public int Roll, Min, Max;
            public int IRandom(int min, int max) { Min = min; Max = max; return Roll; }
            public float Range(float min, float max) => min;
        }

        [Test]
        public void PlayRandomMusicRollsOneToFiveOverTheOriginalsTracks()
        {
            Assert.AreEqual("music_pinballspring", session.CurrentTrack, "globals.gml: current_track = music_pinballspring");
            var expected = new[]
            {
                ("music_pinballspring", "Pinball Spring"), ("music_inaheartbeat", "In a Heartbeat"),
                ("music_latinindustries", "Latin Industries"), ("music_presenterator", "Presenterator"),
                ("music_wallpaper", "Wallpaper"),
            };
            for (int roll = 1; roll <= 5; roll++)
            {
                var random = new FixedRoll { Roll = roll };
                var chosen = Do(new PlayRandomMusic(P1, random)).Find<MusicTrackChosen>();
                Assert.AreEqual(1, random.Min, "randomtrack = irandom_range(1,5)");
                Assert.AreEqual(5, random.Max, "randomtrack = irandom_range(1,5)");
                Assert.AreEqual(expected[roll - 1].Item1, chosen.Sound);
                Assert.AreEqual(expected[roll - 1].Item2, chosen.Title);
                Assert.AreEqual(expected[roll - 1].Item1, session.CurrentTrack);
                Assert.AreEqual(expected[roll - 1].Item2, session.TrackName, "the F12 overlay's Current Track");
            }
        }

        // ---- game_restart ----

        [Test]
        public void RestartPutsEveryGlobalBack()
        {
            Do(new ChooseCampaign(P1));
            Do(new SelectScenario(P1, 2));
            Do(new BeginLevel(P1, LevelObjective.Defeat("Scrap Mental")));
            Do(new AddScore(P1, 200));
            Do(new LoseLife(P1));
            Do(new CollectCoin(P1, 25));
            Do(new SpendSkillPoint(P1));
            Do(new RecordKill(P1));
            Do(new StepMasterVolume(P1, -1));
            Do(new ToggleUnits(P1));
            Do(new ChooseGenerator(P1, 0));
            Do(new TickSession(P1, 4 * Second));
            Do(new AdvanceLevel(P1, false));
            Do(new SetPaused(P1, true));

            var result = Do(new RestartGame(P1));
            Assert.IsNotNull(result.Find<SessionReset>());

            var fresh = new GameSession(ContentRegistry.Default);
            foreach (var property in typeof(GameSession).GetProperties().Where(p => p.Name != nameof(GameSession.Tuning)))
                Assert.AreEqual(property.GetValue(fresh), property.GetValue(session), property.Name);
        }

        // ---- Data ----

        [Test]
        public void TheStartingValuesAreData()
        {
            var content = ContentRegistry.FromJson(
                "{\"session\": {\"StartingSkillPoints\": 9, \"StartingDifficulty\": 40, \"CoinsPerExtraLife\": 10}, \"health\": {\"StartingLives\": 5}}");
            var custom = new GameSession(content);
            Assert.AreEqual(9, custom.SkillPoints);
            Assert.AreEqual(40, custom.Difficulty);
            Assert.AreEqual(5, custom.Lives, "LIVES is HealthTuning.StartingLives, one figure");
            custom.Execute(new CollectCoin(P1, 10));
            Assert.AreEqual(6, custom.Lives);
        }

        [Test]
        public void BadSessionDataIsRejected()
        {
            var ex = Assert.Throws<ContentValidationException>(() => ContentRegistry.FromJson(
                "{\"session\": {\"StartingMasterVolume\": 2, \"CoinsPerExtraLife\": 0, \"StartingScenario\": -5}}"));
            Assert.IsTrue(ex.Errors.Any(e => e.Contains("session.StartingMasterVolume")));
            Assert.IsTrue(ex.Errors.Any(e => e.Contains("session.CoinsPerExtraLife")));
            Assert.IsTrue(ex.Errors.Any(e => e.Contains("session.StartingScenario")));
        }
    }
}
