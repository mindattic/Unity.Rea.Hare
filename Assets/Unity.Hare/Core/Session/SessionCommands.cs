using System;

namespace Unity.Hare.Session
{
    /// <summary>
    /// The only way the session changes. A command carries the id of the seat it acts for (no global current
    /// player) and runs in <see cref="GameSession.Execute"/>, which returns whether it took effect and the events it
    /// raised. Views, menus and the room flow translate what happened into these and play the events.
    /// </summary>
    public abstract class SessionCommand
    {
        protected SessionCommand(PlayerId id) => Id = id;

        public PlayerId Id { get; }

        /// <summary>Does the work; false when the command was refused (the rules said no).</summary>
        internal abstract bool Apply(GameSession session);
    }

    // ---- The game ----

    /// <summary>game_restart: every global back at its globals.gml value (<see cref="SessionReset"/>).</summary>
    public sealed class RestartGame : SessionCommand
    {
        public RestartGame(PlayerId id) : base(id) { }
        internal override bool Apply(GameSession session) { session.Restart(); return true; }
    }

    /// <summary>obj_btn_level01..08, obj_btn_shaft, obj_drop_intro: quickstart at <c>level</c>, no scenario.</summary>
    public sealed class ChooseQuickstart : SessionCommand
    {
        readonly int level;

        public ChooseQuickstart(PlayerId id, int level) : base(id)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), level, "Levels start at 1.");
            this.level = level;
        }

        internal override bool Apply(GameSession session)
        {
            session.ChooseMode(GameMode.Quickstart, -1, level, session.Difficulty);
            return true;
        }
    }

    /// <summary>obj_btn_campaign, obj_scroll_world, obj_story: the campaign from level 1, no scenario chosen yet.</summary>
    public sealed class ChooseCampaign : SessionCommand
    {
        public ChooseCampaign(PlayerId id) : base(id) { }

        internal override bool Apply(GameSession session)
        {
            session.ChooseMode(GameMode.Campaign, -1, 1, session.Difficulty);
            return true;
        }
    }

    /// <summary>
    /// obj_btn_diff0 and obj_btn_diff100 (and obj_btn_level09): the enemy generator at <c>difficulty</c>. The
    /// buttons set only gamemode and difficulty, so the scenario and level stay where they were.
    /// </summary>
    public sealed class ChooseGenerator : SessionCommand
    {
        readonly int? difficulty;

        /// <param name="difficulty">The new budget; null keeps the current one (obj_btn_level09 sets none).</param>
        public ChooseGenerator(PlayerId id, int? difficulty) : base(id)
        {
            if (difficulty < 0) throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, "Must not be negative.");
            this.difficulty = difficulty;
        }

        internal override bool Apply(GameSession session)
        {
            session.ChooseMode(GameMode.Generator, session.Scenario, session.Level, difficulty ?? session.Difficulty);
            return true;
        }
    }

    /// <summary>obj_destination: current_scenario = the destination's scenario_id (-1 when it is cleared).</summary>
    public sealed class SelectScenario : SessionCommand
    {
        readonly int scenario;

        public SelectScenario(PlayerId id, int scenario) : base(id)
        {
            if (scenario < -1) throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "-1 (none) or a scenario.");
            this.scenario = scenario;
        }

        internal override bool Apply(GameSession session) { session.SelectScenario(scenario); return true; }
    }

    // ---- The level ----

    /// <summary>setup_level and the level script's objective block (<see cref="ObjectiveSet"/>).</summary>
    public sealed class BeginLevel : SessionCommand
    {
        readonly LevelObjective objective;
        readonly string levelName;
        readonly int stationaryPercent;

        /// <param name="levelName">The script's level_name; null leaves it.</param>
        /// <param name="stationaryPercent">The script's stationary_percent; -1 (a script without the line) leaves it.</param>
        public BeginLevel(PlayerId id, LevelObjective objective, string levelName = null, int stationaryPercent = -1) : base(id)
        {
            this.objective = objective ?? throw new ArgumentNullException(nameof(objective));
            if (stationaryPercent < -1 || stationaryPercent > 100)
                throw new ArgumentOutOfRangeException(nameof(stationaryPercent), stationaryPercent, "-1 (left alone) or 0-100.");
            this.levelName = levelName;
            this.stationaryPercent = stationaryPercent;
        }

        internal override bool Apply(GameSession session) { session.BeginLevel(objective, levelName, stationaryPercent); return true; }
    }

    /// <summary>One step of the session's clock (obj_healthbar's second alarm) and obj_game's objective check.</summary>
    public sealed class TickSession : SessionCommand
    {
        readonly float dt;
        public TickSession(PlayerId id, float dt) : base(id) => this.dt = dt;
        internal override bool Apply(GameSession session) { session.Tick(dt); return true; }
    }

    /// <summary>The defeat objective's target was destroyed (instance_exists(target_id) went false).</summary>
    public sealed class DefeatTarget : SessionCommand
    {
        public DefeatTarget(PlayerId id) : base(id) { }
        internal override bool Apply(GameSession session) { session.DefeatTarget(); return true; }
    }

    /// <summary>set_level_complete. Refused when the level is already complete.</summary>
    public sealed class CompleteLevel : SessionCommand
    {
        public CompleteLevel(PlayerId id) : base(id) { }
        internal override bool Apply(GameSession session) => session.CompleteLevel();
    }

    /// <summary>
    /// current_level += 1. obj_player flying off the top zeroes current_score and level_complete first
    /// (<c>resetScore</c> true); obj_fade_game's fade-in does not.
    /// </summary>
    public sealed class AdvanceLevel : SessionCommand
    {
        readonly bool resetScore;
        public AdvanceLevel(PlayerId id, bool resetScore) : base(id) => this.resetScore = resetScore;
        internal override bool Apply(GameSession session) { session.AdvanceLevel(resetScore); return true; }
    }

    /// <summary>The global paused flag.</summary>
    public sealed class SetPaused : SessionCommand
    {
        readonly bool paused;
        public SetPaused(PlayerId id, bool paused) : base(id) => this.paused = paused;
        internal override bool Apply(GameSession session) { session.SetPaused(paused); return true; }
    }

    /// <summary>
    /// obj_game's P key press (U.HARE-T60): <c>if(!level_complete) paused = !paused</c>. Refused once the level is
    /// complete, so the results screen's pause (set_level_complete) holds until its Enter.
    /// </summary>
    public sealed class TogglePause : SessionCommand
    {
        public TogglePause(PlayerId id) : base(id) { }

        internal override bool Apply(GameSession session)
        {
            if (session.IsLevelComplete) return false;
            session.SetPaused(!session.IsPaused);
            return true;
        }
    }

    // ---- Score, lives and the counters ----

    /// <summary><c>current_score += points</c> (an enemy, a rock, the boss, a shrapnel piece).</summary>
    public sealed class AddScore : SessionCommand
    {
        readonly int points;
        public AddScore(PlayerId id, int points) : base(id) => this.points = points;
        internal override bool Apply(GameSession session) { session.AddScore(points); return true; }
    }

    /// <summary>
    /// A life goes: player_hurt at 0 HP, or the ship leaving a non-looping room. Refused when LIVES is already 0,
    /// in which case the out-of-bounds rule runs game_restart (<see cref="GameOver"/>, then <see cref="SessionReset"/>).
    /// </summary>
    public sealed class LoseLife : SessionCommand
    {
        public LoseLife(PlayerId id) : base(id) { }
        internal override bool Apply(GameSession session) => session.LoseLife();
    }

    /// <summary>
    /// A ship's death (or flight out of the arena) spent a life: <c>LIVES -= 1</c> (<see cref="LifeLost"/>), never below
    /// 0. Unlike <see cref="LoseLife"/> it never restarts the game: the ship's death wait does that. Refused at 0.
    /// </summary>
    public sealed class SpendLife : SessionCommand
    {
        public SpendLife(PlayerId id) : base(id) { }
        internal override bool Apply(GameSession session) => session.SpendLife();
    }

    /// <summary>obj_coin touched the ship: COINS += value, and the hundredth coin is an extra life.</summary>
    public sealed class CollectCoin : SessionCommand
    {
        readonly int value;

        public CollectCoin(PlayerId id, int value) : base(id)
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), value, "Must not be negative.");
            this.value = value;
        }

        internal override bool Apply(GameSession session) { session.CollectCoin(value); return true; }
    }

    /// <summary>An obj_upgrade_* bought: refused with no skill points left.</summary>
    public sealed class SpendSkillPoint : SessionCommand
    {
        public SpendSkillPoint(PlayerId id) : base(id) { }
        internal override bool Apply(GameSession session) => session.SpendSkillPoint();
    }

    /// <summary>Skill points awarded (or taken back), never below zero.</summary>
    public sealed class AddSkillPoints : SessionCommand
    {
        readonly int points;
        public AddSkillPoints(PlayerId id, int points) : base(id) => this.points = points;
        internal override bool Apply(GameSession session) { session.AddSkillPoints(points); return true; }
    }

    /// <summary>An enemy was killed: KILLS += 1 (docs/BIBLE.md D11).</summary>
    public sealed class RecordKill : SessionCommand
    {
        public RecordKill(PlayerId id) : base(id) { }
        internal override bool Apply(GameSession session) { session.RecordKill(); return true; }
    }

    // ---- Settings ----

    /// <summary>obj_game's numpad minus (<c>direction</c> -1) and plus (+1): master_volume by one step.</summary>
    public sealed class StepMasterVolume : SessionCommand
    {
        readonly int direction;
        public StepMasterVolume(PlayerId id, int direction) : base(id) => this.direction = direction;
        internal override bool Apply(GameSession session) { session.StepMasterVolume(direction); return true; }
    }

    /// <summary>Sets master_volume directly, held within 0..1.</summary>
    public sealed class SetMasterVolume : SessionCommand
    {
        readonly float volume;
        public SetMasterVolume(PlayerId id, float volume) : base(id) => this.volume = volume;
        internal override bool Apply(GameSession session) { session.SetMasterVolume(volume); return true; }
    }

    /// <summary>obj_game's U key: <c>use_metric = !use_metric</c>.</summary>
    public sealed class ToggleUnits : SessionCommand
    {
        public ToggleUnits(PlayerId id) : base(id) { }
        internal override bool Apply(GameSession session) { session.SetUseMetric(!session.UseMetric); return true; }
    }

    /// <summary>Sets use_metric directly (the profile's saved units, put on a new session).</summary>
    public sealed class SetUnits : SessionCommand
    {
        readonly bool useMetric;
        public SetUnits(PlayerId id, bool useMetric) : base(id) => this.useMetric = useMetric;
        internal override bool Apply(GameSession session) { session.SetUseMetric(useMetric); return true; }
    }

    /// <summary>obj_game's F12 key press: <c>debug = !debug</c> (U.HARE-T59).</summary>
    public sealed class ToggleDebug : SessionCommand
    {
        public ToggleDebug(PlayerId id) : base(id) { }
        internal override bool Apply(GameSession session) { session.SetDebug(!session.Debug); return true; }
    }
}
