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
    /// boss's mission unlocks once every other is won. The original keeps nothing (it has no boss lock either). A
    /// small versioned JSON document, <c>{"Version": 1, "Won": [1, 2, 3]}</c>, which the runtime writes to
    /// <c>campaign.json</c> in the profile's Saves folder; U.HARE-T65 folds it into the full campaign save.
    /// </summary>
    public sealed class CampaignProgress
    {
        /// <summary>The format this build writes and reads.</summary>
        public const int CurrentVersion = 1;

        readonly SortedSet<int> won = new SortedSet<int>();

        /// <summary>The won missions' scenarios, in order.</summary>
        public IReadOnlyCollection<int> Won => won;

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
            builder.Append("]\n}\n");
            return builder.ToString();
        }

        /// <summary>
        /// Reads a document. Throws <see cref="FormatException"/> when it is not one: not JSON, not an object, a version
        /// this build does not know, or a won list that is not whole scenarios of 1 or more.
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
            return progress;
        }
    }
}
