using System;
using System.Collections.Generic;
using Unity.Rea.Hare.Content;
using Unity.Rea.Hare.Flow;

namespace Unity.Rea.Hare.Story
{
    /// <summary>
    /// One scenario of load_scenario: the level scripts its <c>current_level</c> 1, 2, ... run.
    /// </summary>
    [Serializable]
    public class ScenarioDefinition
    {
        /// <summary>GML: current_scenario.</summary>
        public int Scenario;
        /// <summary>The script for current_level 1, 2, ... (level_01 ... level_10).</summary>
        public string[] Levels = Array.Empty<string>();

        public ScenarioDefinition() { }

        public ScenarioDefinition(int scenario, params string[] levels)
        {
            Scenario = scenario;
            Levels = levels;
        }
    }

    /// <summary>
    /// The story, its typewriter and scroll, obj_conversation and load_scenario as data (the content registry's
    /// <c>story</c> section, AGENTS.md rule 4; U.HARE-T28). The text itself is the dialogue section's: <see cref="Script"/>
    /// names the story's lines, <see cref="ConversationDefinition.MapScript"/> the map's comm-box line. The defaults are the
    /// original's, read from HARE.gmx (obj_story, obj_typewriter_story, obj_scroll_world, obj_conversation,
    /// obj_textdone, obj_typewriter, load_scenario).
    /// </summary>
    [Serializable]
    public class StoryTuning
    {
        /// <summary>The dialogue lines obj_story builds its <c>typewriter.text</c> from, in order.</summary>
        public string Script = "story";
        /// <summary>obj_story's <c>typewriter = instance_create(30, 60, obj_typewriter_story)</c>.</summary>
        public TypewriterDefinition Typewriter = TypewriterDefinition.Story();
        /// <summary>obj_scroll_world's sprite (1066x1062, origin 533,531), rising from below the room.</summary>
        public string EarthSprite = "spr_earthball";
        /// <summary><c>y = room_height + sprite_get_height(spr_earthball)</c>.</summary>
        public int EarthHeight = 1062;
        /// <summary>GML: scroll_delay, the steps between two 1-pixel scrolls of the earth and background_y[0].</summary>
        public int ScrollDelaySteps = 3;
        /// <summary>The pixels each scroll moves the earth up and background_y[0] up.</summary>
        public int ScrollStep = 1;
        /// <summary>GML: auto_speed, degrees a step taken off image_angle (normalize_angle keeps it in 0..360).</summary>
        public double AutoSpeed = 0.01d;
        /// <summary>room_story's background 0, tiled.</summary>
        public string Background = "bg_nightsky";
        /// <summary>obj_story's key press and obj_scroll_world's end: <c>next_room = room_map</c> through obj_fade_story.</summary>
        public RoomId ExitRoom = RoomId.Map;
        /// <summary>obj_conversation.</summary>
        public ConversationDefinition Conversation = new ConversationDefinition();
        /// <summary>obj_textdone, the blinking marker a finished line shows in the story's and the comm box's corner.</summary>
        public TextDoneDefinition TextDone = new TextDoneDefinition();
        /// <summary>obj_typewriter, which only commented-out code (obj_intro, obj_drop_intro) makes.</summary>
        public TypewriterDefinition PlainTypewriter = TypewriterDefinition.Plain();
        /// <summary>load_scenario's switch: scenario 1 plays level_01..05, 2, 3 and 4 play level_06..10.</summary>
        public ScenarioDefinition[] Scenarios =
        {
            new ScenarioDefinition(1, "level_01", "level_02", "level_03", "level_04", "level_05"),
            new ScenarioDefinition(2, "level_06", "level_07", "level_08", "level_09", "level_10"),
            new ScenarioDefinition(3, "level_06", "level_07", "level_08", "level_09", "level_10"),
            new ScenarioDefinition(4, "level_06", "level_07", "level_08", "level_09", "level_10"),
        };
        /// <summary>load_scenario's <c>default:</c>, any other scenario (-1 before the map picks one).</summary>
        public string DefaultScenarioLevel = "level_default";

        /// <summary>
        /// load_scenario: the script <paramref name="scenario"/>'s <paramref name="level"/> runs, the default's for a
        /// scenario the switch does not list, or null when a listed scenario has no script for that level (its
        /// <c>if</c> chain matches nothing, so no level script runs).
        /// </summary>
        public string LoadScenario(int scenario, int level)
        {
            if (Scenarios != null)
                foreach (var s in Scenarios)
                {
                    if (s == null || s.Scenario != scenario) continue;
                    return s.Levels != null && level >= 1 && level <= s.Levels.Length ? s.Levels[level - 1] : null;
                }
            return DefaultScenarioLevel;
        }

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Script", Script);
            if (Typewriter == null) errors.Add(path + ".Typewriter: must not be null.");
            else Typewriter.Validate(errors, path + ".Typewriter");
            ValidationHelpers.Required(errors, path + ".EarthSprite", EarthSprite);
            ValidationHelpers.NonNegative(errors, path + ".EarthHeight", EarthHeight);
            ValidationHelpers.Positive(errors, path + ".ScrollDelaySteps", ScrollDelaySteps);
            ValidationHelpers.Positive(errors, path + ".ScrollStep", ScrollStep);
            if (!(AutoSpeed >= 0d)) errors.Add($"{path}.AutoSpeed: must not be negative (was {AutoSpeed}).");
            ValidationHelpers.Required(errors, path + ".Background", Background);
            if (!Enum.IsDefined(typeof(RoomId), ExitRoom)) errors.Add($"{path}.ExitRoom: not a room (was {(int)ExitRoom}).");
            if (Conversation == null) errors.Add(path + ".Conversation: must not be null.");
            else Conversation.Validate(errors, path + ".Conversation");
            if (TextDone == null) errors.Add(path + ".TextDone: must not be null.");
            else TextDone.Validate(errors, path + ".TextDone");
            if (PlainTypewriter == null) errors.Add(path + ".PlainTypewriter: must not be null.");
            else PlainTypewriter.Validate(errors, path + ".PlainTypewriter");
            if (Scenarios == null) errors.Add(path + ".Scenarios: must not be null.");
            else
            {
                var seen = new HashSet<int>();
                for (int i = 0; i < Scenarios.Length; i++)
                {
                    string entryPath = $"{path}.Scenarios[{i}]";
                    var s = Scenarios[i];
                    if (s == null) { errors.Add(entryPath + ": must not be null."); continue; }
                    if (!seen.Add(s.Scenario)) errors.Add($"{entryPath}.Scenario: {s.Scenario} is listed more than once.");
                    if (s.Levels == null) errors.Add(entryPath + ".Levels: must not be null.");
                    else for (int n = 0; n < s.Levels.Length; n++) ValidationHelpers.Required(errors, $"{entryPath}.Levels[{n}]", s.Levels[n]);
                }
            }
            ValidationHelpers.Required(errors, path + ".DefaultScenarioLevel", DefaultScenarioLevel);
        }
    }
}
