using System;
using System.Collections.Generic;
using Unity.Rea.Hare.Flow;
using Unity.Rea.Hare.Session;

namespace Unity.Rea.Hare.Arena
{
    /// <summary>
    /// Which level script setup_level runs (U.HARE-T57, U.HARE-T64): <c>if(gamemode == gm_quickstart)</c> level_01..level_10
    /// for current_level 1..10 and level_default past them, <c>gm_generator</c> level_enemygenerator, and
    /// <c>gm_campaign</c> load_scenario (scenario 1 levels 1-5 play level_01..05, scenarios 2-4 play level_06..10, any
    /// other scenario level_default). A mission launched from room_map (U.HARE-T19, owner-directed, docs/BIBLE.md V42)
    /// is the campaign at its own level n and plays level_0n, whatever its marker's scenario number: the owner gave every
    /// one of the original's ten levels a marker of its own, where load_scenario's table gave scenarios 2-4 the same five.
    /// </summary>
    public static class LevelSetup
    {
        /// <summary>
        /// The script setup_level runs for <paramref name="level"/> in <paramref name="mode"/> when nothing else is known
        /// of the session: the campaign is taken as a map mission at that level (level_0n).
        /// </summary>
        public static LevelSetting ScriptFor(ArenaTuning arena, GameMode mode, int level)
        {
            if (arena == null) throw new ArgumentNullException(nameof(arena));
            return mode == GameMode.Generator ? arena.GeneratorLevel : arena.SettingFor(level);
        }

        /// <summary>
        /// setup_level's <c>if(gamemode == ...)</c> for the whole session: quick start and the generator as
        /// <see cref="ScriptFor(ArenaTuning, GameMode, int)"/>; the campaign plays the map mission's own level when one was
        /// launched (<paramref name="missionLevel"/> &gt; 0), and otherwise load_scenario(<paramref name="scenario"/>,
        /// <paramref name="level"/>). Where load_scenario's <c>if</c> chain matches nothing the original runs no level
        /// script and plays on with the last level's setting and zeroed enemies; with no setting to keep here it plays
        /// level_default, load_scenario's own fallback.
        /// </summary>
        public static LevelSetting ScriptFor(ArenaTuning arena, Story.StoryTuning story, GameMode mode, int scenario, int level, int missionLevel)
        {
            if (arena == null) throw new ArgumentNullException(nameof(arena));
            if (mode != GameMode.Campaign || missionLevel > 0 || story == null) return ScriptFor(arena, mode, level);
            return LoadScenario(arena, story, scenario, level) ?? arena.DefaultLevel;
        }

        /// <summary><see cref="ScriptFor(ArenaTuning, Story.StoryTuning, GameMode, int, int, int)"/> for <paramref name="session"/> at <paramref name="level"/>.</summary>
        public static LevelSetting ScriptFor(ArenaTuning arena, Story.StoryTuning story, GameSession session, int level)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            return ScriptFor(arena, story, session.Mode, session.Scenario, level, session.MissionLevel);
        }

        /// <summary>
        /// The room a menu's way into play (a title button's fade to room_gamescreen, room_intro's room_goto) actually
        /// lands in: <paramref name="requested"/> itself unless it is a play room, and then the room the session's level
        /// script plays in. In the original obj_game's Create runs setup_level in room_gamescreen and the level script's
        /// <c>if(room != room_x) room_goto(room_x)</c> moves on at once (level_04 to room_shaft, level_05 to
        /// room_sidescroll); going there directly is the same game without the throwaway room (U.HARE-T64).
        /// </summary>
        public static RoomId EntryRoom(ArenaTuning arena, Story.StoryTuning story, GameSession session, RoomId requested)
        {
            if (arena == null) throw new ArgumentNullException(nameof(arena));
            if (session == null || arena.RoomFor(requested) == null) return requested;
            return ScriptFor(arena, story, session, session.Level).Room;
        }

        /// <summary>
        /// The room a finished level leaves for instead of the next level, or null to go on (U.HARE-T64, owner-directed,
        /// docs/BIBLE.md V42): a map mission's level, won, goes back to room_map, so winning one mission never runs on into
        /// the next level (which, after level_07, would be the locked boss's level_08). Quick start, the generator and a
        /// campaign not launched from the map go on as the original does.
        /// </summary>
        public static RoomId? RoomAfterLevel(GameSession session, RoomId mapRoom = RoomId.Map)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            return session.EndsMission ? mapRoom : (RoomId?)null;
        }

        /// <summary>
        /// load_scenario (U.HARE-T28): the level setting <paramref name="scenario"/>'s <paramref name="level"/> runs
        /// (<see cref="Story.StoryTuning.LoadScenario"/>: scenario 1 level_01..05, 2 to 4 level_06..10, any other
        /// level_default), or null when the scenario lists no script for that level (no level script runs). setup_level
        /// runs it for a campaign not launched from the map (U.HARE-T64).
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
        public static LevelSetting SettingFor(ArenaTuning arena, GameMode mode, int level, RoomId room) =>
            InRoom(arena, ScriptFor(arena, mode, level), level, room);

        /// <summary><see cref="SettingFor(ArenaTuning, GameMode, int, RoomId)"/> for the whole session (load_scenario in the campaign).</summary>
        public static LevelSetting SettingFor(ArenaTuning arena, Story.StoryTuning story, GameSession session, int level, RoomId room) =>
            InRoom(arena, ScriptFor(arena, story, session, level), level, room);

        static LevelSetting InRoom(ArenaTuning arena, LevelSetting script, int level, RoomId room) =>
            script.Room == room ? script : arena.SettingFor(level, room);

        /// <summary>
        /// The level script's <c>if(room != room_x) room_goto(room_x)</c>: the room the script for
        /// <paramref name="level"/> sends the game to, or null when it already plays in <paramref name="room"/>.
        /// </summary>
        public static RoomId? RoomGoto(ArenaTuning arena, GameMode mode, int level, RoomId room) =>
            Goto(ScriptFor(arena, mode, level), room);

        /// <summary><see cref="RoomGoto(ArenaTuning, GameMode, int, RoomId)"/> for the whole session (load_scenario in the campaign).</summary>
        public static RoomId? RoomGoto(ArenaTuning arena, Story.StoryTuning story, GameSession session, int level, RoomId room) =>
            Goto(ScriptFor(arena, story, session, level), room);

        static RoomId? Goto(LevelSetting script, RoomId room) => script.Room == room ? (RoomId?)null : script.Room;
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
