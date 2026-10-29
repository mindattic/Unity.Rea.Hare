using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Hare.Content;
using Unity.Hare.Popups;

namespace Unity.Hare.Session
{
    /// <summary>
    /// The campaign save (U.HARE-T65, a deliberate addition, docs/BIBLE.md V50): the map missions the player has won
    /// (U.HARE-T19, BIBLE V42), so the boss's mission unlocks once every other is won; the garage's bought pips
    /// (U.HARE-T24, BIBLE V49), what obj_upgrade_*'s <c>//Write to Ini here</c> never wrote, and with the wins the
    /// SKILLPOINTS left (<see cref="SkillPoints"/>); the COINS carried, the scenario and level the campaign run reached,
    /// and the best score finished on each campaign level. The original keeps all of this in globals only and loses it
    /// on restart. A small versioned JSON document,
    /// <c>{"Version": 3, "Won": [1, 2], "Upgrades": ["vulcan.rate.1"], "Coins": 42, "Scenario": -1, "Level": 3,
    /// "BestScores": {"1": 1200}}</c>, which the runtime writes to <c>campaign.json</c> in the profile's Saves folder;
    /// versions 1 (wins only) and 2 (wins and upgrades) still read, with no coins, the campaign at its start and no scores.
    /// </summary>
    public sealed class CampaignProgress
    {
        /// <summary>The format this build writes; it reads 1 (wins only), 2 (wins and upgrades) and 3 (the full campaign).</summary>
        public const int CurrentVersion = 3;

        /// <summary>globals.gml's current_scenario for "none chosen", and the campaign's start.</summary>
        public const int NoScenario = -1;

        readonly SortedSet<int> won = new SortedSet<int>();
        readonly SortedSet<string> upgrades = new SortedSet<string>(StringComparer.Ordinal);
        readonly SortedDictionary<int, int> bestScores = new SortedDictionary<int, int>();

        /// <summary>The COINS carried when the campaign was last saved (below the hundred that makes an extra life).</summary>
        public int Coins { get; private set; }

        /// <summary>current_scenario where the campaign run (from the story, not a map mission) last stood; -1 for none.</summary>
        public int Scenario { get; private set; } = NoScenario;

        /// <summary>current_level the campaign run (from the story, not a map mission) has reached; 1 at its start.</summary>
        public int Level { get; private set; } = 1;

        /// <summary>The best current_score finished on each campaign level, by level, in order.</summary>
        public IReadOnlyDictionary<int, int> BestScores => bestScores;

        /// <summary>The best score finished on <paramref name="level"/>, or 0 when it never was.</summary>
        public int BestScore(int level) => bestScores.TryGetValue(level, out int best) ? best : 0;

        /// <summary>
        /// Records <paramref name="score"/> finished on campaign <paramref name="level"/>; true when it beats the best
        /// kept (a first finish counts, even at 0).
        /// </summary>
        public bool RecordScore(int level, int score)
        {
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), level, "Levels start at 1.");
            if (bestScores.TryGetValue(level, out int best) && best >= score) return false;
            bestScores[level] = score;
            return true;
        }

        /// <summary>Records the COINS carried; true when they changed. Never below zero.</summary>
        public bool RecordCoins(int coins)
        {
            coins = Math.Max(0, coins);
            if (coins == Coins) return false;
            Coins = coins;
            return true;
        }

        /// <summary>Records where the campaign run stands (current_scenario, current_level); true when it moved.</summary>
        public bool RecordReached(int scenario, int level)
        {
            if (scenario < NoScenario || scenario == 0) throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "-1 (none) or a scenario.");
            if (level < 1) throw new ArgumentOutOfRangeException(nameof(level), level, "Levels start at 1.");
            if (scenario == Scenario && level == Level) return false;
            Scenario = scenario;
            Level = level;
            return true;
        }

        /// <summary>The won missions' scenarios, in order.</summary>
        public IReadOnlyCollection<int> Won => won;

        /// <summary>The ids of the garage pips bought (<see cref="UpgradeCost.Id"/>), in order.</summary>
        public IReadOnlyCollection<string> Upgrades => upgrades;

        public bool Owns(string upgradeId) => upgradeId != null && upgrades.Contains(upgradeId);

        /// <summary>Records <paramref name="upgradeId"/> bought; false when it already was (a pip is bought once).</summary>
        public bool RecordPurchase(string upgradeId)
        {
            if (string.IsNullOrWhiteSpace(upgradeId)) throw new ArgumentException("An upgrade needs an id.", nameof(upgradeId));
            return upgrades.Add(upgradeId);
        }

        /// <summary>
        /// The SKILLPOINTS left to spend: globals.gml's <paramref name="starting"/> (5), plus
        /// <see cref="UpgradeCatalog.SkillPointsPerMissionWon"/> for every mission won (each counts once, on its first
        /// win), less what the bought pips cost. Never below zero.
        /// </summary>
        public int SkillPoints(int starting, UpgradeCatalog catalog)
        {
            int earned = starting + (catalog != null ? won.Count * catalog.SkillPointsPerMissionWon : 0);
            int spent = catalog != null ? catalog.SpentOn(upgrades) : 0;
            return Math.Max(0, earned - spent);
        }

        public bool HasWon(int scenario) => won.Contains(scenario);

        /// <summary>Records <paramref name="scenario"/> won; false when it already was (nothing to save).</summary>
        public bool RecordWin(int scenario)
        {
            if (scenario < 1) throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Scenarios start at 1.");
            return won.Add(scenario);
        }

        /// <summary>Whether every mission of <paramref name="missions"/> but the boss's is won (the boss is unlocked).</summary>
        public bool BossUnlocked(IEnumerable<AlertMission> missions)
        {
            if (missions == null) return true;
            foreach (var m in missions)
                if (m != null && !m.Boss && !won.Contains(m.ScenarioId)) return false;
            return true;
        }

        /// <summary>The document.</summary>
        public string ToJson()
        {
            var builder = new StringBuilder();
            builder.Append("{\n  \"Version\": ").Append(CurrentVersion.ToString(CultureInfo.InvariantCulture)).Append(",\n  \"Won\": [");
            bool first = true;
            foreach (int scenario in won)
            {
                if (!first) builder.Append(", ");
                builder.Append(scenario.ToString(CultureInfo.InvariantCulture));
                first = false;
            }
            builder.Append("],\n  \"Upgrades\": [");
            first = true;
            foreach (string id in upgrades)
            {
                if (!first) builder.Append(", ");
                builder.Append('"').Append(id.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
                first = false;
            }
            builder.Append("],\n  \"Coins\": ").Append(Coins.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\n  \"Scenario\": ").Append(Scenario.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\n  \"Level\": ").Append(Level.ToString(CultureInfo.InvariantCulture));
            builder.Append(",\n  \"BestScores\": {");
            first = true;
            foreach (var pair in bestScores)
            {
                if (!first) builder.Append(", ");
                builder.Append('"').Append(pair.Key.ToString(CultureInfo.InvariantCulture)).Append("\": ")
                       .Append(pair.Value.ToString(CultureInfo.InvariantCulture));
                first = false;
            }
            builder.Append("}\n}\n");
            return builder.ToString();
        }

        /// <summary>
        /// Reads a document. Throws <see cref="FormatException"/> when it is not one: not JSON, not an object, a version
        /// this build does not know, a won list that is not whole scenarios of 1 or more, an upgrade list that is not
        /// ids, or (version 3) coins, a scenario, a level or best scores that are not whole numbers in range. A version 1
        /// document has no upgrades; versions 1 and 2 have no coins, the campaign at its start and no best scores.
        /// </summary>
        public static CampaignProgress FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("campaign: the document is empty.");
            if (!(MiniJson.Parse(json) is Dictionary<string, object> root)) throw new FormatException("campaign: the top level must be an object.");
            if (!root.TryGetValue("Version", out object version) || !(version is double v) || v != Math.Floor(v))
                throw new FormatException("campaign: Version must be a whole number.");
            if (v < 1 || v > CurrentVersion) throw new FormatException($"campaign: Version {v} is not one this build reads (1..{CurrentVersion}).");
            var progress = new CampaignProgress();
            if (root.TryGetValue("Won", out object list) && list != null)
            {
                if (!(list is List<object> items)) throw new FormatException("campaign: Won must be a list.");
                foreach (var item in items)
                {
                    if (!(item is double d) || d != Math.Floor(d) || d < 1 || d > int.MaxValue)
                        throw new FormatException($"campaign: Won holds {item ?? "null"}, not a scenario.");
                    progress.won.Add((int)d);
                }
            }
            if (v >= 2 && root.TryGetValue("Upgrades", out object bought) && bought != null)
            {
                if (!(bought is List<object> ids)) throw new FormatException("campaign: Upgrades must be a list.");
                foreach (var id in ids)
                {
                    if (!(id is string s) || string.IsNullOrWhiteSpace(s))
                        throw new FormatException($"campaign: Upgrades holds {id ?? "null"}, not an upgrade id.");
                    progress.upgrades.Add(s);
                }
            }
            if (v >= 3)
            {
                progress.Coins = WholeNumber(root, "Coins", 0, 0, int.MaxValue);
                progress.Scenario = WholeNumber(root, "Scenario", NoScenario, NoScenario, int.MaxValue);
                if (progress.Scenario == 0) throw new FormatException("campaign: Scenario must be -1 (none) or a scenario.");
                progress.Level = WholeNumber(root, "Level", 1, 1, int.MaxValue);
                if (root.TryGetValue("BestScores", out object scores) && scores != null)
                {
                    if (!(scores is Dictionary<string, object> table)) throw new FormatException("campaign: BestScores must be an object.");
                    foreach (var pair in table)
                    {
                        if (!int.TryParse(pair.Key, NumberStyles.None, CultureInfo.InvariantCulture, out int level) || level < 1)
                            throw new FormatException($"campaign: BestScores names {pair.Key}, not a level.");
                        if (!(pair.Value is double d) || d != Math.Floor(d) || d < int.MinValue || d > int.MaxValue)
                            throw new FormatException($"campaign: BestScores holds {pair.Value ?? "null"} for level {level}, not a score.");
                        progress.bestScores[level] = (int)d;
                    }
                }
            }
            return progress;
        }

        /// <summary><paramref name="key"/>'s whole number within <paramref name="min"/>..<paramref name="max"/>, or <paramref name="fallback"/> when absent.</summary>
        static int WholeNumber(Dictionary<string, object> root, string key, int fallback, int min, int max)
        {
            if (!root.TryGetValue(key, out object value) || value == null) return fallback;
            if (!(value is double d) || d != Math.Floor(d) || d < min || d > max)
                throw new FormatException($"campaign: {key} holds {value}, not a whole number from {min}.");
            return (int)d;
        }
    }
}
