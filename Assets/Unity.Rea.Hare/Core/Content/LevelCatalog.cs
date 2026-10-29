using System;
using System.Collections.Generic;

namespace Unity.Hare.Content
{
    /// <summary>One of the campaign's ten rooms (docs/RECONSTRUCTION.md): its scenario, difficulty and composition.</summary>
    [Serializable]
    public class LevelDefinition
    {
        /// <summary>A short, unique, human-readable key (e.g. "scenario1-level1"), not a GML room name.</summary>
        public string Id = "";
        public int Scenario;
        public int Level;
        /// <summary>generate_enemy_list's difficulty, for generator-mode levels; 0 for a fixed composition.</summary>
        public int DifficultyPoints;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Id", Id);
            ValidationHelpers.NonNegative(errors, path + ".Scenario", Scenario);
            ValidationHelpers.NonNegative(errors, path + ".Level", Level);
            ValidationHelpers.NonNegative(errors, path + ".DifficultyPoints", DifficultyPoints);
        }
    }

    /// <summary>
    /// The campaign's level definitions (UH-US-O11's scenario-to-level mapping, UH-T64). Empty until the level-flow
    /// tickets (U.HARE-T50, U.HARE-T64) populate it from the reconstruction; the registry validates whatever is
    /// here today so that ticket can add entries to a data file instead of hardcoding them.
    /// </summary>
    [Serializable]
    public class LevelCatalog
    {
        public LevelDefinition[] Levels = Array.Empty<LevelDefinition>();

        internal void Validate(List<string> errors, string path)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Levels.Length; i++)
            {
                var level = Levels[i];
                string entryPath = $"{path}.Levels[{i}]";
                if (level == null) { errors.Add($"{entryPath}: must not be null."); continue; }
                level.Validate(errors, entryPath);
                if (!string.IsNullOrWhiteSpace(level.Id) && !seen.Add(level.Id))
                    errors.Add($"{entryPath}.Id: '{level.Id}' is used by more than one level.");
            }
        }
    }
}
