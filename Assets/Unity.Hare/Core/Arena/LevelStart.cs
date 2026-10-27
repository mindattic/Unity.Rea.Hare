using System;
using System.Collections.Generic;
using Unity.Hare.Flow;
using Unity.Hare.Session;

namespace Unity.Hare.Arena
{
    /// <summary>
    /// Which level script setup_level runs (U.HARE-T57): <c>if(gamemode == gm_quickstart)</c> level_01..level_10 for
    /// current_level 1..10 and level_default past them, <c>gm_generator</c> level_enemygenerator. <c>gm_campaign</c>'s
    /// load_scenario is U.HARE-T64's; until it lands the campaign plays the quick-start chain, which is what
    /// load_scenario's scenario 1 maps levels 1-5 to. A mission launched from room_map (U.HARE-T19, owner-directed,
    /// docs/BIBLE.md V42) is the campaign at its own level n, so this already plays level_0n for it.
    /// </summary>
    public static class LevelSetup
    {
        /// <summary>The script setup_level runs for <paramref name="level"/> in <paramref name="mode"/>.</summary>
        public static LevelSetting ScriptFor(ArenaTuning arena, GameMode mode, int level)
        {
            if (arena == null) throw new ArgumentNullException(nameof(arena));
            return mode == GameMode.Generator ? arena.GeneratorLevel : arena.SettingFor(level);
        }

        /// <summary>
        /// load_scenario (U.HARE-T28): the level setting <paramref name="scenario"/>'s <paramref name="level"/> runs
        /// (<see cref="Story.StoryTuning.LoadScenario"/>: scenario 1 level_01..05, 2 to 4 level_06..10, any other
        /// level_default), or null when the scenario lists no script for that level (no level script runs). setup_level
        /// does not call it yet: running the campaign through it is U.HARE-T64's.
        /// </summary>
        public static LevelSetting LoadScenario(ArenaTuning arena, Story.StoryTuning story, int scenario, int level)
        {
            if (arena == null) throw new ArgumentNullException(nameof(arena));
            if (story == null) throw new ArgumentNullException(nameof(story));
            string script = story.LoadScenario(scenario, level);
            if (script == null) return null;
            if (arena.Levels != null)
                foreach (var setting in arena.Levels)
                    if (setting != null && setting.Script == script) return setting;
            if (arena.DefaultLevel != null && arena.DefaultLevel.Script == script) return arena.DefaultLevel;
            throw new KeyNotFoundException($"load_scenario names {script}, which the arena section has no level for.");
        }

        /// <summary>
        /// The setting the play room <paramref name="room"/> sets up: the script's when it plays there; otherwise (a room
        /// opened on its own, which no level script sent the game to) the first level that plays in that room, as
        /// <see cref="ArenaTuning.SettingFor(int, RoomId)"/> has it.
        /// </summary>
        public static LevelSetting SettingFor(ArenaTuning arena, GameMode mode, int level, RoomId room)
        {
            var script = ScriptFor(arena, mode, level);
            return script.Room == room ? script : arena.SettingFor(level, room);
        }

        /// <summary>
        /// The level script's <c>if(room != room_x) room_goto(room_x)</c>: the room the script for
        /// <paramref name="level"/> sends the game to, or null when it already plays in <paramref name="room"/>.
        /// </summary>
        public static RoomId? RoomGoto(ArenaTuning arena, GameMode mode, int level, RoomId room)
        {
            var script = ScriptFor(arena, mode, level);
            return script.Room == room ? (RoomId?)null : script.Room;
        }
    }

    /// <summary>
    /// obj_levelstart (U.HARE-T57), the banner setup_level makes: level_name over strObjective in the middle of the GUI
    /// layer. Create plays sfx_switch (the view's), sets <c>alpha = 1</c>, <c>alarm[0] = 120</c> and
    /// <c>alarm[1] = rate</c> (2). Alarm 0 sets <c>fade = true</c>; Alarm 1, every <c>rate</c> steps, takes
    /// <c>0.01</c> off the alpha while it fades and destroys the banner once the alpha is no longer above 0. Neither alarm
    /// looks at paused. The alpha is a double, as GameMaker's reals are, so the hundred subtractions land where
    /// the original's do (a hair above 0, then one more step below it before the banner goes).
    /// </summary>
    public sealed class LevelStartBanner
    {
        readonly int rate;
        readonly double fadeStep;
        int fadeAlarm;
        int rateAlarm;

        public LevelStartBanner(string levelName, string objective, ArenaTuning tuning)
            : this(levelName, objective, tuning.LevelStartFadeDelaySteps, tuning.LevelStartFadeRateSteps, tuning.LevelStartFadeStep) { }

        public LevelStartBanner(string levelName, string objective, int fadeDelaySteps = 120, int rateSteps = 2, float fadeStepPerFiring = 0.01f)
        {
            if (fadeDelaySteps < 1) throw new ArgumentOutOfRangeException(nameof(fadeDelaySteps), fadeDelaySteps, "An alarm needs at least one step.");
            if (rateSteps < 1) throw new ArgumentOutOfRangeException(nameof(rateSteps), rateSteps, "An alarm needs at least one step.");
            LevelName = levelName ?? "";
            Objective = objective ?? "";
            rate = rateSteps;
            // 0.01f as the decimal it was written as, not the float's nearest binary neighbour.
            fadeStep = double.Parse(fadeStepPerFiring.ToString("R", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture);
            Alpha = 1d;
            fadeAlarm = fadeDelaySteps;
            rateAlarm = rate;
        }

        /// <summary>GML: level_name, as the Draw GUI reads it.</summary>
        public string LevelName { get; private set; }
        /// <summary>GML: strObjective, as the Draw GUI reads it.</summary>
        public string Objective { get; private set; }
        /// <summary>GML: alpha (it may end a hair below 0; <see cref="DrawAlpha"/> is what draw_set_alpha makes of it).</summary>
        public double Alpha { get; private set; }
        /// <summary>draw_set_alpha(alpha): held within 0..1.</summary>
        public float DrawAlpha => (float)Math.Max(0d, Math.Min(1d, Alpha));
        /// <summary>GML: fade, set by Alarm 0.</summary>
        public bool Fading { get; private set; }
        /// <summary>instance_destroy() has run.</summary>
        public bool Gone { get; private set; }
        /// <summary>Steps run since Create.</summary>
        public int Steps { get; private set; }

        /// <summary>The level script's level_name and set_objective_string, which run after the banner is made in the same step.</summary>
        public void SetText(string levelName, string objective)
        {
            LevelName = levelName ?? "";
            Objective = objective ?? "";
        }

        /// <summary>One step of the banner's alarms, in GameMaker's order (Alarm 0, then Alarm 1). False once it is gone.</summary>
        public bool Step()
        {
            if (Gone) return false;
            Steps++;
            if (fadeAlarm > 0 && --fadeAlarm == 0) Fading = true;
            if (rateAlarm > 0 && --rateAlarm == 0)
            {
                if (Fading)
                {
                    if (Alpha > 0d) Alpha -= fadeStep;
                    else
                    {
                        Gone = true;
                        return false;
                    }
                }
                rateAlarm = rate;
            }
            return true;
        }
    }
}
