using System;
using System.Collections.Generic;
using Unity.Hare.Content;

namespace Unity.Hare.Session
{
    /// <summary>globals.gml's game modes: gm_quickstart, gm_campaign, gm_generator (their GML values).</summary>
    public enum GameMode
    {
        Quickstart = 1,
        Campaign = 2,
        Generator = 3,
    }

    /// <summary>globals.gml's objective kinds: o_points, o_timelimit, o_defeat, o_survive (their GML values).</summary>
    public enum ObjectiveKind
    {
        Points = 1,
        TimeLimit = 2,
        Defeat = 3,
        Survive = 4,
    }

    /// <summary>
    /// What a level script sets up before play (level_01..level_10, level_default, level_enemygenerator): the
    /// objective, its target (goal_score, time_limit_seconds, target_name) and whether it zeroes current_score.
    /// </summary>
    public sealed class LevelObjective
    {
        public LevelObjective(ObjectiveKind kind, int goalScore = 0, int timeLimitSeconds = 0, string targetName = null, bool resetScore = true, bool resetClock = true)
        {
            if (!Enum.IsDefined(typeof(ObjectiveKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not an objective kind.");
            if (goalScore < 0) throw new ArgumentOutOfRangeException(nameof(goalScore), goalScore, "Must not be negative.");
            if (timeLimitSeconds < 0) throw new ArgumentOutOfRangeException(nameof(timeLimitSeconds), timeLimitSeconds, "Must not be negative.");
            Kind = kind;
            GoalScore = goalScore;
            TimeLimitSeconds = timeLimitSeconds;
            TargetName = targetName;
            ResetScore = resetScore;
            ResetClock = resetClock;
        }

        /// <summary>GML: objective.</summary>
        public ObjectiveKind Kind { get; }
        /// <summary>GML: goal_score. Every level script sets one, whatever its objective.</summary>
        public int GoalScore { get; }
        /// <summary>GML: time_limit_seconds; only an <see cref="ObjectiveKind.TimeLimit"/> objective reads it.</summary>
        public int TimeLimitSeconds { get; }
        /// <summary>GML: target_name, for <see cref="ObjectiveKind.Defeat"/>; null keeps the session's current one.</summary>
        public string TargetName { get; }
        /// <summary>
        /// Whether the script runs <c>current_score = 0</c>. Every level script does except level_10, which keeps
        /// whatever score the level before it left (zero when reached by flying off the top, since obj_player's exit
        /// zeroes it first).
        /// </summary>
        public bool ResetScore { get; }
        /// <summary>
        /// Whether the script runs <c>time_elapsed_seconds = 0</c>. level_01..level_10 and level_enemygenerator do;
        /// level_default and the level_objective_* scripts do not.
        /// </summary>
        public bool ResetClock { get; }

        /// <summary>level_01..04, level_09, level_default and the objective scripts.</summary>
        public static LevelObjective Points(int goalScore, bool resetScore = true) =>
            new LevelObjective(ObjectiveKind.Points, goalScore, resetScore: resetScore);

        /// <summary>level_05..07: survive <paramref name="seconds"/> (goal_score is still set, and unused).</summary>
        public static LevelObjective TimeLimit(int seconds, int goalScore = 0) =>
            new LevelObjective(ObjectiveKind.TimeLimit, goalScore, seconds);

        /// <summary>level_08: destroy the boss called <paramref name="targetName"/>.</summary>
        public static LevelObjective Defeat(string targetName, int goalScore = 0) =>
            new LevelObjective(ObjectiveKind.Defeat, goalScore, targetName: targetName);

        /// <summary>level_enemygenerator: no win condition.</summary>
        public static LevelObjective Survive() => new LevelObjective(ObjectiveKind.Survive);
    }

    /// <summary>
    /// globals.gml's session numbers as data (AGENTS.md rule 4): what a new game (and game_restart) starts with,
    /// and the rule numbers the session's commands use. LIVES comes from <see cref="Player.HealthTuning.StartingLives"/>
    /// so the starting lives are one figure, not two.
    /// </summary>
    [Serializable]
    public class SessionTuning
    {
        /// <summary>COINS = 0.</summary>
        public int StartingCoins = 0;
        /// <summary>SKILLPOINTS = 5.</summary>
        public int StartingSkillPoints = 5;
        /// <summary>difficulty = 100 (generate_enemy_list's budget in generator mode).</summary>
        public int StartingDifficulty = 100;
        /// <summary>master_volume = 1.</summary>
        public float StartingMasterVolume = 1f;
        /// <summary>use_metric = false (the speedometer shows imperial units).</summary>
        public bool StartingUseMetric = false;
        /// <summary>gamemode = gm_quickstart.</summary>
        public GameMode StartingGameMode = GameMode.Quickstart;
        /// <summary>current_scenario = -1 (none chosen).</summary>
        public int StartingScenario = -1;
        /// <summary>current_level = 1.</summary>
        public int StartingLevel = 1;
        /// <summary>objective = o_points.</summary>
        public ObjectiveKind StartingObjective = ObjectiveKind.Points;
        /// <summary>target_name = "Boss".</summary>
        public string StartingTargetName = "Boss";
        /// <summary>level_name = "Welcome to H.O.P" (U.HARE-T57): what obj_levelstart shows until a level script names its level.</summary>
        public string StartingLevelName = "Welcome to H.O.P";
        /// <summary>debug = false (U.HARE-T59): obj_game's debug overlay starts hidden; F12 toggles it.</summary>
        public bool StartingDebug = false;
        /// <summary>track_name = "Pinball Spring": what the debug overlay's "Current Track" shows until play_random_music runs.</summary>
        public string StartingTrackName = "Pinball Spring";

        /// <summary>obj_coin: at COINS &gt;= 100, LIVES += 1 and COINS = 0.</summary>
        public int CoinsPerExtraLife = 100;
        /// <summary>obj_game's numpad minus and plus move master_volume by 0.01.</summary>
        public float MasterVolumeStep = 0.01f;
        /// <summary>obj_game's F11 key press (a debug key, U.HARE-T60): current_score += 100.</summary>
        public int DebugScorePoints = 100;
        /// <summary>obj_game's Step beeps once a second while 10 or fewer seconds of a time limit are left.</summary>
        public int TimeLimitWarningSeconds = 10;
        /// <summary>obj_healthbar's one_second = room_speed: time_elapsed_seconds counts once per this many steps.</summary>
        public float SecondSteps = GameClock.StepsPerSecond;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.NonNegative(errors, path + ".StartingCoins", StartingCoins);
            ValidationHelpers.NonNegative(errors, path + ".StartingSkillPoints", StartingSkillPoints);
            ValidationHelpers.NonNegative(errors, path + ".StartingDifficulty", StartingDifficulty);
            if (StartingMasterVolume < 0f || StartingMasterVolume > 1f)
                errors.Add($"{path}.StartingMasterVolume: must be between 0 and 1 (was {StartingMasterVolume}).");
            if (!Enum.IsDefined(typeof(GameMode), StartingGameMode))
                errors.Add($"{path}.StartingGameMode: not a game mode (was {(int)StartingGameMode}).");
            if (StartingScenario < -1) errors.Add($"{path}.StartingScenario: must be -1 (none) or a scenario (was {StartingScenario}).");
            ValidationHelpers.Positive(errors, path + ".StartingLevel", StartingLevel);
            if (!Enum.IsDefined(typeof(ObjectiveKind), StartingObjective))
                errors.Add($"{path}.StartingObjective: not an objective kind (was {(int)StartingObjective}).");
            if (StartingLevelName == null) errors.Add(path + ".StartingLevelName: must not be null.");
            if (StartingTrackName == null) errors.Add(path + ".StartingTrackName: must not be null.");
            ValidationHelpers.Positive(errors, path + ".CoinsPerExtraLife", CoinsPerExtraLife);
            ValidationHelpers.Positive(errors, path + ".MasterVolumeStep", MasterVolumeStep);
            ValidationHelpers.NonNegative(errors, path + ".DebugScorePoints", DebugScorePoints);
            ValidationHelpers.NonNegative(errors, path + ".TimeLimitWarningSeconds", TimeLimitWarningSeconds);
            ValidationHelpers.Positive(errors, path + ".SecondSteps", SecondSteps);
        }
    }
}
