using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.Hare.Content;
using Unity.Hare.Popups;

namespace Unity.Hare.Session
{
    /// <summary>
    /// The map missions the player has won (U.HARE-T19, owner-directed, docs/BIBLE.md V42), kept across runs so the
    /// boss's mission unlocks once every other is won, and the garage's bought pips (U.HARE-T24, BIBLE V49): what
    /// obj_upgrade_*'s <c>//Write to Ini here</c> never wrote. The original keeps nothing (it has no boss lock either).
    /// A small versioned JSON document, <c>{"Version": 2, "Won": [1, 2, 3], "Upgrades": ["vulcan.rate.1"]}</c>, which
    /// the runtime writes to <c>campaign.json</c> in the profile's Saves folder; version 1 (no upgrades) still reads.
    /// U.HARE-T65 folds it into the full campaign save.
    /// </summary>
    public sealed class CampaignProgress
    {
        /// <summary>The format this build writes; it reads 1 (wins only) and 2 (wins and upgrades).</summary>
        public const int CurrentVersion = 2;

        readonly SortedSet<int> won = new SortedSet<int>();
        readonly SortedSet<string> upgrades = new SortedSet<string>(StringComparer.Ordinal);

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
            builder.Append("]\n}\n");
            return builder.ToString();
        }

        /// <summary>
        /// Reads a document. Throws <see cref="FormatException"/> when it is not one: not JSON, not an object, a version
        /// this build does not know, a won list that is not whole scenarios of 1 or more, or an upgrade list that is not
        /// ids. A version 1 document has no upgrades.
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
            return progress;
        }
    }
}
