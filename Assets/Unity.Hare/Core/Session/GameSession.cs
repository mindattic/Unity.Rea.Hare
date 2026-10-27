using System;
using System.Collections.Generic;
using Unity.Hare.Content;
using Unity.Hare.Player;

namespace Unity.Hare.Session
{
    /// <summary>
    /// globals.gml's session state, engine-free: LIVES, COINS, SKILLPOINTS, KILLS, current_score and goal_score,
    /// difficulty, use_metric, master_volume, gamemode, the objective and its target, time_elapsed_seconds and
    /// time_limit_seconds, current_scenario and current_level, level_complete and paused. There is no global
    /// instance: whoever holds the game's authority owns one and every reader is handed it. State changes only
    /// through <see cref="Execute"/>, which runs a <see cref="SessionCommand"/> and returns the
    /// <see cref="SessionEvent"/>s it raised. A new <see cref="GameSession"/>, or <see cref="RestartGame"/>, is
    /// game_restart: GameMaker re-runs globals.gml, so every value is back at its start.
    /// </summary>
    public sealed class GameSession
    {
        readonly List<SessionEvent> events = new List<SessionEvent>();
        PlayerId issuer;
        float secondTimer;
        const float SecondTolerance = 1e-4f;
        int lastWarnedSecond;

        /// <param name="content">The tuning to start from; null reads <see cref="GameContent.Current"/>.</param>
        public GameSession(ContentRegistry content = null)
        {
            content ??= GameContent.Current;
            Tuning = content.Session;
            StartingLives = content.Health.StartingLives;
            StartingStationaryPercent = content.Arena.StartingStationaryPercent;
            Reset();
        }

        public SessionTuning Tuning { get; }
        /// <summary>LIVES at the start of a game (<see cref="HealthTuning.StartingLives"/>).</summary>
        public int StartingLives { get; }
        /// <summary>stationary_percent at the start of a game (<see cref="Arena.ArenaTuning.StartingStationaryPercent"/>).</summary>
        public int StartingStationaryPercent { get; }

        /// <summary>GML: LIVES.</summary>
        public int Lives { get; private set; }
        /// <summary>GML: COINS.</summary>
        public int Coins { get; private set; }
        /// <summary>GML: SKILLPOINTS.</summary>
        public int SkillPoints { get; private set; }
        /// <summary>GML: KILLS (shown by obj_level_results).</summary>
        public int Kills { get; private set; }
        /// <summary>GML: current_score.</summary>
        public int Score { get; private set; }
        /// <summary>GML: goal_score.</summary>
        public int GoalScore { get; private set; }
        /// <summary>GML: difficulty, generate_enemy_list's budget.</summary>
        public int Difficulty { get; private set; }
        /// <summary>GML: use_metric.</summary>
        public bool UseMetric { get; private set; }
        /// <summary>GML: master_volume, 0..1.</summary>
        public float MasterVolume { get; private set; }
        /// <summary>GML: gamemode.</summary>
        public GameMode Mode { get; private set; }
        /// <summary>GML: objective.</summary>
        public ObjectiveKind Objective { get; private set; }
        /// <summary>GML: target_name.</summary>
        public string TargetName { get; private set; }
        /// <summary>!instance_exists(target_id): the defeat objective's target is gone.</summary>
        public bool TargetDefeated { get; private set; }
        /// <summary>GML: time_limit_seconds.</summary>
        public int TimeLimitSeconds { get; private set; }
        /// <summary>GML: time_elapsed_seconds.</summary>
        public int TimeElapsedSeconds { get; private set; }
        /// <summary>GML: current_scenario; -1 when none is chosen.</summary>
        public int Scenario { get; private set; }
        /// <summary>GML: current_level.</summary>
        public int Level { get; private set; }
        /// <summary>GML: level_complete.</summary>
        public bool IsLevelComplete { get; private set; }
        /// <summary>GML: paused.</summary>
        public bool IsPaused { get; private set; }
        /// <summary>GML: strObjective, set_objective_string's text for the current objective.</summary>
        public string ObjectiveText { get; private set; }
        /// <summary>GML: level_name, the level script's "Meta Data" (obj_levelstart shows it).</summary>
        public string LevelName { get; private set; }
        /// <summary>
        /// GML: stationary_percent, a globalvar setup_level never resets: a level script that does not set it plays with
        /// whatever the level before it left (<see cref="Arena.LevelSetting.StationaryPercentAfter"/>, BIBLE V27).
        /// </summary>
        public int StationaryPercent { get; private set; }
        /// <summary>GML: debug, obj_game's F12 overlay (FPS, the ship, its rock, the track).</summary>
        public bool Debug { get; private set; }
        /// <summary>GML: track_name, the music play_random_music last chose.</summary>
        public string TrackName { get; private set; }

        /// <summary>obj_player's death wait: reset_player when true, game_restart when false.</summary>
        public bool HasLivesLeft => Lives > 0;

        /// <summary>Runs <paramref name="command"/> and returns whether it took effect and what it raised.</summary>
        public SessionResult Execute(SessionCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            events.Clear();
            issuer = command.Id;
            bool accepted = command.Apply(this);
            var raised = events.Count == 0 ? Array.Empty<SessionEvent>() : events.ToArray();
            events.Clear();
            return new SessionResult(accepted, raised);
        }

        void Emit(SessionEvent e)
        {
            e.Id = issuer;
            events.Add(e);
        }

        // ---- Rules the commands call ----

        /// <summary>game_restart: globals.gml again, and obj_game's own last_second.</summary>
        internal void Reset()
        {
            Lives = StartingLives;
            Coins = Tuning.StartingCoins;
            SkillPoints = Tuning.StartingSkillPoints;
            Kills = 0;
            Score = 0;
            GoalScore = 0;
            Difficulty = Tuning.StartingDifficulty;
            UseMetric = Tuning.StartingUseMetric;
            MasterVolume = Tuning.StartingMasterVolume;
            Mode = Tuning.StartingGameMode;
            Objective = Tuning.StartingObjective;
            TargetName = Tuning.StartingTargetName;
            TargetDefeated = false;
            TimeLimitSeconds = 0;
            TimeElapsedSeconds = 0;
            Scenario = Tuning.StartingScenario;
            Level = Tuning.StartingLevel;
            MissionLevel = 0;
            IsLevelComplete = false;
            IsPaused = false;
            ObjectiveText = "";
            LevelName = Tuning.StartingLevelName;
            StationaryPercent = StartingStationaryPercent;
            Debug = Tuning.StartingDebug;
            TrackName = Tuning.StartingTrackName;
            secondTimer = 0f;
            lastWarnedSecond = 0;
        }

        internal void Restart()
        {
            Reset();
            Emit(new SessionReset());
        }

        internal void AddScore(int points)
        {
            if (points == 0) return;
            Score += points;
            Emit(new ScoreChanged { Score = Score, Delta = points });
            CheckObjective();
        }

        /// <summary>
        /// player_hurt's death (LIVES -= 1) and the out-of-bounds rule (<c>if(LIVES &gt; 0) LIVES -= 1; else
        /// game_restart()</c>). False when there was no life to take and the game restarted.
        /// </summary>
        internal bool LoseLife()
        {
            if (Lives > 0)
            {
                Lives--;
                Emit(new LifeLost { Lives = Lives });
                return true;
            }

            Emit(new GameOver());
            Restart();
            return false;
        }

        /// <summary>
        /// A ship spending a life through its <see cref="SessionLives"/>: player_hurt's death, or the out-of-bounds rule
        /// once it has checked there is one to spend. <c>LIVES -= 1</c>, never below 0 (docs/BIBLE.md D14); the ship
        /// decides reset_player or game_restart itself. False when there was none to take.
        /// </summary>
        internal bool SpendLife()
        {
            if (Lives <= 0) return false;
            Lives--;
            Emit(new LifeLost { Lives = Lives });
            return true;
        }

        /// <summary>obj_coin's collision with the player.</summary>
        internal void CollectCoin(int value)
        {
            Coins += value;
            if (Coins >= Tuning.CoinsPerExtraLife)
            {
                Lives++;
                Coins = 0;
                Emit(new CoinsChanged { Coins = Coins });
                Emit(new ExtraLifeEarned { Lives = Lives });
            }
            else
            {
                Emit(new CoinsChanged { Coins = Coins });
            }
        }

        /// <summary>obj_upgrade_*'s click: <c>if(SKILLPOINTS &gt; 0) SKILLPOINTS -= 1</c>.</summary>
        internal bool SpendSkillPoint()
        {
            if (SkillPoints <= 0) return false;
            SkillPoints--;
            Emit(new SkillPointsChanged { SkillPoints = SkillPoints });
            return true;
        }

        internal void AddSkillPoints(int points)
        {
            if (points == 0) return;
            SkillPoints = Math.Max(0, SkillPoints + points);
            Emit(new SkillPointsChanged { SkillPoints = SkillPoints });
        }

        internal void RecordKill()
        {
            Kills++;
            Emit(new KillsChanged { Kills = Kills });
        }

        /// <summary>
        /// The level the map's chosen mission launched (U.HARE-T19, docs/BIBLE.md V42), 0 when the game did not start
        /// from the map. Completing that level in the campaign wins the mission (<see cref="MissionWon"/>).
        /// </summary>
        public int MissionLevel { get; private set; }

        internal void ChooseMode(GameMode mode, int scenario, int level, int difficulty, int missionLevel = 0)
        {
            MissionLevel = missionLevel;
            Mode = mode;
            Scenario = scenario;
            Level = level;
            Difficulty = difficulty;
            Emit(new GameModeChosen { Mode = mode, Scenario = scenario, Level = level, Difficulty = difficulty });
        }

        internal void SelectScenario(int scenario)
        {
            Scenario = scenario;
            Emit(new ScenarioChanged { Scenario = scenario });
        }

        /// <summary>setup_level's resets and the level script's objective block, then set_objective_string.</summary>
        internal void BeginLevel(LevelObjective objective, string levelName = null, int stationaryPercent = -1)
        {
            if (levelName != null) LevelName = levelName;
            if (stationaryPercent >= 0) StationaryPercent = stationaryPercent;
            SetPaused(false);
            IsLevelComplete = false;
            Objective = objective.Kind;
            GoalScore = objective.GoalScore;
            if (objective.Kind == ObjectiveKind.TimeLimit) TimeLimitSeconds = objective.TimeLimitSeconds;
            if (objective.TargetName != null) TargetName = objective.TargetName;
            TargetDefeated = false;
            if (objective.ResetClock) TimeElapsedSeconds = 0;

            if (objective.ResetScore && Score != 0)
            {
                int delta = -Score;
                Score = 0;
                Emit(new ScoreChanged { Score = 0, Delta = delta });
            }

            ObjectiveText = DescribeObjective(Objective, GoalScore, TimeLimitSeconds, TargetName);
            Emit(new ObjectiveSet { Kind = Objective, Text = ObjectiveText, LevelName = LevelName });
        }

        internal void DefeatTarget()
        {
            TargetDefeated = true;
            CheckObjective();
        }

        /// <summary>set_level_complete. False when the level was already complete.</summary>
        internal bool CompleteLevel()
        {
            if (IsLevelComplete) return false;
            IsLevelComplete = true;
            Emit(new LevelCompleted { Level = Level, Objective = Objective });
            if (Mode == GameMode.Campaign && Scenario > 0 && MissionLevel > 0 && Level == MissionLevel)
                Emit(new MissionWon { Scenario = Scenario, Level = Level });
            SetPaused(true);
            return true;
        }

        /// <summary>obj_player's exit off the top (zeroes the score first) or obj_fade_game's fade-in (does not).</summary>
        internal void AdvanceLevel(bool resetScore)
        {
            if (resetScore && Score != 0)
            {
                int delta = -Score;
                Score = 0;
                Emit(new ScoreChanged { Score = 0, Delta = delta });
            }

            if (resetScore) IsLevelComplete = false;
            Level++;
            Emit(new LevelAdvanced { Level = Level });
        }

        /// <summary>obj_healthbar's one-second alarm (it keeps cycling while paused, but only counts unpaused) and obj_game's Step check.</summary>
        internal void Tick(float deltaTime)
        {
            if (deltaTime < 0f) throw new ArgumentOutOfRangeException(nameof(deltaTime), deltaTime, "Must not be negative.");
            float second = GameClock.Seconds(Tuning.SecondSteps);
            secondTimer += deltaTime;
            // A room step is 1/room_speed s: 120 of them add up to a second give or take a float's last bit, which must
            // still count as obj_healthbar's alarm[0] = one_second firing on its 120th step.
            while (secondTimer >= second - SecondTolerance)
            {
                secondTimer -= second;
                if (IsPaused) continue;
                TimeElapsedSeconds++;
                Emit(new SecondElapsed { Seconds = TimeElapsedSeconds });
            }

            CheckObjective();
        }

        internal void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;
            Emit(new PausedChanged { Paused = paused });
        }

        /// <summary>obj_game's numpad minus (-1) and plus (+1): master_volume moves a step, held within 0..1.</summary>
        internal void StepMasterVolume(int direction) =>
            SetMasterVolume(MasterVolume + Math.Sign(direction) * Tuning.MasterVolumeStep);

        internal void SetMasterVolume(float volume)
        {
            // Rounded so a hundred 0.01 steps land on exactly 0 and 1 rather than drifting past them.
            float clamped = (float)Math.Round(Math.Max(0f, Math.Min(1f, volume)), 4);
            if (clamped == MasterVolume) return;
            MasterVolume = clamped;
            Emit(new MasterVolumeChanged { Volume = MasterVolume });
        }

        internal void SetDebug(bool debug)
        {
            if (Debug == debug) return;
            Debug = debug;
            Emit(new DebugChanged { Debug = debug });
        }

        internal void SetUseMetric(bool useMetric)
        {
            if (UseMetric == useMetric) return;
            UseMetric = useMetric;
            Emit(new UnitsChanged { UseMetric = useMetric });
        }

        /// <summary>obj_game's Step: has the objective been met?</summary>
        void CheckObjective()
        {
            switch (Objective)
            {
                case ObjectiveKind.Points:
                    if (Score >= GoalScore) CompleteLevel();
                    break;

                case ObjectiveKind.TimeLimit:
                    int left = TimeLimitSeconds - TimeElapsedSeconds;
                    if (left <= Tuning.TimeLimitWarningSeconds && left > 0 && TimeElapsedSeconds != lastWarnedSecond)
                    {
                        lastWarnedSecond = TimeElapsedSeconds;
                        Emit(new TimeLimitWarning { SecondsLeft = left });
                    }
                    if (TimeElapsedSeconds >= TimeLimitSeconds) CompleteLevel();
                    break;

                case ObjectiveKind.Defeat:
                    if (TargetDefeated) CompleteLevel();
                    break;

                // o_survive (the generator) has no win condition.
            }
        }

        // ---- Text ----

        /// <summary>set_objective_string's strObjective.</summary>
        public static string DescribeObjective(ObjectiveKind kind, int goalScore, int timeLimitSeconds, string targetName)
        {
            switch (kind)
            {
                case ObjectiveKind.Points: return "Accumulate " + goalScore + " points";
                case ObjectiveKind.TimeLimit: return "Survive " + DescribeTimeLimit(timeLimitSeconds);
                case ObjectiveKind.Defeat: return "Defeat " + targetName;
                case ObjectiveKind.Survive: return "Survive the Alien Onslaught!";
                default: return "";
            }
        }

        /// <summary>
        /// set_time_limit_string's written time: "1 minute, 30 seconds", "2 minutes", "45 seconds". The original
        /// also writes the ", " after whole minutes with no seconds ("1 minute, "); see docs/BIBLE.md D10.
        /// </summary>
        public static string DescribeTimeLimit(int seconds)
        {
            int mins = seconds / 60;
            int secs = seconds % 60;
            string text = "";
            if (mins == 1) text += mins + " minute";
            else if (mins > 1) text += mins + " minutes";
            if (mins > 0 && secs > 0) text += ", ";
            if (secs == 1) text += secs + " second";
            else if (secs > 1) text += secs + " seconds";
            return text;
        }
    }
}
