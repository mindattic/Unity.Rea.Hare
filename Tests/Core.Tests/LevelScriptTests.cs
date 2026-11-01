using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using StringAssert = NUnit.Framework.Legacy.StringAssert;
using Unity.Rea.Hare.Arena;
using Unity.Rea.Hare.Content;
using Unity.Rea.Hare.Enemies;
using Unity.Rea.Hare.Flow;
using Unity.Rea.Hare.Session;

namespace Unity.Rea.Hare.Tests.Core
{
    /// <summary>
    /// The level scripts as data (U.HARE-T20): level_01..level_10, level_default and level_enemygenerator's surfaces,
    /// enemies and objective, the load_level parts (level_setting_*, level_objective_*, level_composition_*), the shipped
    /// file against the compiled-in tables, and validation.
    /// </summary>
    public class LevelScriptTests
    {
        static readonly EnemyKind[] Kinds = EnemyComposition.PlacementOrder;

        static string Ranges(LevelEnemies enemies) =>
            string.Join(" ", Kinds.Select(k => $"{k}:{enemies[k].Min}-{enemies[k].Max}"));

        static string Expected(params (EnemyKind Kind, int Min, int Max)[] set)
        {
            var enemies = new LevelEnemies();
            foreach (var (kind, min, max) in set) enemies[kind] = new EnemyRange(min, max);
            return Ranges(enemies);
        }

        [Test]
        public void EachLevelHasTheOriginalsSurfaces()
        {
            // rock_min, rock_max, stationary_percent (-1: the script has no stationary_percent line).
            var expected = new Dictionary<int, (int, int, int)>
            {
                [1] = (15, 30, 50), [2] = (15, 30, 25), [3] = (15, 20, 15), [4] = (20, 20, -1), [5] = (0, 0, -1),
                [6] = (15, 30, 75), [7] = (15, 30, 30), [8] = (20, 24, 60), [9] = (15, 30, -1), [10] = (15, 30, -1),
            };
            var tuning = ContentRegistry.Default.Arena;
            foreach (var pair in expected)
            {
                var s = tuning.SettingFor(pair.Key);
                Assert.AreEqual(pair.Value, (s.RockMin, s.RockMax, s.StationaryPercent), s.Script);
            }
            Assert.AreEqual((15, 30, -1), (tuning.DefaultLevel.RockMin, tuning.DefaultLevel.RockMax, tuning.DefaultLevel.StationaryPercent));
            Assert.AreEqual((15, 30, 50), (tuning.GeneratorLevel.RockMin, tuning.GeneratorLevel.RockMax, tuning.GeneratorLevel.StationaryPercent));
        }

        [Test]
        public void EachLevelHasTheOriginalsEnemies()
        {
            var H = EnemyKind.Hydra; var S = EnemyKind.Sawblade; var M = EnemyKind.Meatball; var W = EnemyKind.Wraith;
            var P = EnemyKind.Phalanx; var Mi = EnemyKind.Mimic; var Wo = EnemyKind.Worm; var D = EnemyKind.Dagger; var T = EnemyKind.Trilobyte;
            var expected = new Dictionary<int, string>
            {
                [1] = Expected((H, 1, 3), (S, 1, 2), (Mi, 1, 3), (D, 1, 2)),
                [2] = Expected((S, 1, 2), (M, 2, 2), (P, 2, 3), (Mi, 3, 5)),
                [3] = Expected((Mi, 3, 5), (Wo, 2, 2), (D, 3, 4)),
                [4] = Expected((H, 8, 10), (S, 5, 7), (Mi, 10, 10)),
                [5] = Expected((H, 3, 4), (S, 2, 3), (M, 1, 1), (P, 1, 2), (Mi, 15, 20)),
                [6] = Expected((H, 1, 2), (S, 2, 3), (W, 2, 2), (Mi, 2, 3)),
                [7] = Expected((H, 1, 2), (S, 3, 4), (Mi, 3, 5), (D, 3, 4), (T, 10, 12)),
                [8] = Expected(),
                [9] = Expected((H, 8, 10), (S, 20, 20), (M, 1, 1)),
                [10] = Expected((H, 10, 12), (S, 20, 20), (M, 2, 3)),
            };
            var tuning = ContentRegistry.Default.Arena;
            foreach (var pair in expected)
                Assert.AreEqual(pair.Value, Ranges(tuning.SettingFor(pair.Key).Enemies), "level_" + pair.Key.ToString("00"));
            Assert.AreEqual(Expected((H, 3, 8), (S, 1, 6), (M, 1, 3)), Ranges(tuning.DefaultLevel.Enemies), "level_default");
            Assert.AreEqual(Expected(), Ranges(tuning.GeneratorLevel.Enemies), "level_enemygenerator");
            // A level past 10 runs level_default (setup_level's quick-start chain).
            Assert.AreSame(tuning.DefaultLevel, tuning.SettingFor(11));
        }

        [Test]
        public void EachLevelHasTheOriginalsObjective()
        {
            var tuning = ContentRegistry.Default.Arena;
            // (kind, goal_score, time_limit_seconds, target_name, target object, resets current_score, resets time_elapsed_seconds)
            var expected = new Dictionary<int, (ObjectiveKind, int, int, string, string, bool, bool)>
            {
                [1] = (ObjectiveKind.Points, 500, 0, "", "", true, true),
                [2] = (ObjectiveKind.Points, 500, 0, "", "", true, true),
                [3] = (ObjectiveKind.Points, 500, 0, "", "", true, true),
                [4] = (ObjectiveKind.Points, 500, 0, "", "", true, true),
                [5] = (ObjectiveKind.TimeLimit, 500, 90, "", "", true, true),
                [6] = (ObjectiveKind.TimeLimit, 500, 90, "", "", true, true),
                [7] = (ObjectiveKind.TimeLimit, 500, 90, "", "", true, true),
                [8] = (ObjectiveKind.Defeat, 500, 0, "Scrap Mental", "obj_scrapmental", true, true),
                [9] = (ObjectiveKind.Points, 500, 90, "", "", true, true),
                [10] = (ObjectiveKind.Points, 18000, 90, "", "", false, true),
            };
            foreach (var pair in expected)
            {
                var o = tuning.SettingFor(pair.Key).Objective;
                Assert.AreEqual(pair.Value, (o.Kind, o.GoalScore, o.TimeLimitSeconds, o.TargetName, o.TargetObject, o.ResetScore, o.ResetClock), "level_" + pair.Key.ToString("00"));
            }
            var d = tuning.DefaultLevel.Objective;
            Assert.AreEqual((ObjectiveKind.Points, 10000, true, false), (d.Kind, d.GoalScore, d.ResetScore, d.ResetClock), "level_default");
            var g = tuning.GeneratorLevel.Objective;
            Assert.AreEqual((ObjectiveKind.Survive, 0, true, true), (g.Kind, g.GoalScore, g.ResetScore, g.ResetClock), "level_enemygenerator");
        }

        [Test]
        public void TheObjectiveIsWhatTheSessionBeginsTheLevelWith()
        {
            var tuning = ContentRegistry.Default.Arena;
            var boss = tuning.SettingFor(8).Objective.ToObjective();
            Assert.AreEqual((ObjectiveKind.Defeat, 500, "Scrap Mental"), (boss.Kind, boss.GoalScore, boss.TargetName));
            var points = tuning.SettingFor(1).Objective.ToObjective();
            Assert.IsNull(points.TargetName, "a script without target_name keeps the session's");
            var last = tuning.SettingFor(10).Objective.ToObjective();
            Assert.AreEqual((18000, false, true), (last.GoalScore, last.ResetScore, last.ResetClock));

            var session = new GameSession(ContentRegistry.Default);
            var result = session.Execute(new BeginLevel(new PlayerId(1), tuning.SettingFor(5).Objective.ToObjective()));
            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(ObjectiveKind.TimeLimit, session.Objective);
            Assert.AreEqual(90, session.TimeLimitSeconds);
        }

        [Test]
        public void TheEnemiesBecomeSetupLevelsComposition()
        {
            var composition = ContentRegistry.Default.Arena.SettingFor(7).ToComposition();
            Assert.AreEqual((10, 12), (composition[EnemyKind.Trilobyte].Min, composition[EnemyKind.Trilobyte].Max));
            Assert.AreEqual((3, 4), (composition[EnemyKind.Dagger].Min, composition[EnemyKind.Dagger].Max));
            Assert.AreEqual((0, 0), (composition[EnemyKind.Worm].Min, composition[EnemyKind.Worm].Max));
        }

        [Test]
        public void StationaryPercentCarriesOverFromTheLevelBefore()
        {
            // globals.gml starts it at 50 and setup_level never resets it: level_04 after level_03 plays at 15.
            var tuning = ContentRegistry.Default.Arena;
            int percent = tuning.StartingStationaryPercent;
            Assert.AreEqual(50, percent);
            var seen = new List<int>();
            for (int level = 1; level <= 10; level++)
            {
                percent = tuning.SettingFor(level).StationaryPercentAfter(percent);
                seen.Add(percent);
            }
            CollectionAssert.AreEqual(new[] { 50, 25, 15, 15, 15, 75, 30, 60, 60, 60 }, seen);
            Assert.AreEqual(60, tuning.DefaultLevel.StationaryPercentAfter(60));
        }

        [Test]
        public void LoadLevelPutsItsThreePartsTogether()
        {
            var tuning = ContentRegistry.Default.Arena;
            var level = tuning.LoadLevel(0, 0, 1);
            Assert.AreEqual((RoomId.GameScreen, "bg_worldcurve", false, false), (level.Room, level.Background, level.LoopHorizontal, level.LoopVertical));
            Assert.AreEqual("Welcome to H.O.P", level.Name);
            Assert.AreEqual((ObjectiveKind.Points, 500, true, false), (level.Objective.Kind, level.Objective.GoalScore, level.Objective.ResetScore, level.Objective.ResetClock));
            CollectionAssert.AreEqual(new[] { "Go get'em Hutch!#The planet is counting on you!" }, level.Objective.Dialog);
            Assert.AreEqual((20, 25, LevelSetting.KeepsStationaryPercent), (level.RockMin, level.RockMax, level.StationaryPercent));
            Assert.AreEqual(Expected((EnemyKind.Sawblade, 6, 8), (EnemyKind.Meatball, 1, 2)), Ranges(level.Enemies));

            var second = tuning.LoadLevel(1, 1, 0);
            Assert.AreEqual(("Level 02", false), (second.Name, second.Objective.ResetScore));
            Assert.AreEqual(Expected((EnemyKind.Hydra, 3, 5), (EnemyKind.Sawblade, 2, 3), (EnemyKind.Mimic, 1, 3), (EnemyKind.Dagger, 2, 3)), Ranges(second.Enemies));

            // load_level knows only 0 and 1: any other number leaves that half as it was.
            var before = tuning.SettingFor(6);
            var kept = tuning.LoadLevel(5, 5, 5, before);
            Assert.AreEqual(("Wraiths", "bg_nightsky", true), (kept.Name, kept.Background, kept.Tiled));
            Assert.AreSame(before.Objective, kept.Objective);
            Assert.AreEqual(Expected(), Ranges(kept.Enemies), "setup_level zeroed the composition before load_level");
        }

        [Test]
        public void TheShippedLevels_MatchTheDefaults()
        {
            var fromFile = ContentRegistry.FromJson(File.ReadAllText(ShippedGameContentJsonPath())).Arena;
            var defaults = ContentRegistry.Default.Arena;
            Assert.AreEqual(defaults.StartingStationaryPercent, fromFile.StartingStationaryPercent);
            var all = defaults.Levels.Concat(new[] { defaults.DefaultLevel, defaults.GeneratorLevel }).ToList();
            var loaded = fromFile.Levels.Concat(new[] { fromFile.DefaultLevel, fromFile.GeneratorLevel }).ToList();
            CollectionAssert.AreEqual(all.Select(Describe).ToList(), loaded.Select(Describe).ToList());
            CollectionAssert.AreEqual(defaults.ObjectiveParts.Select(Describe).ToList(), fromFile.ObjectiveParts.Select(Describe).ToList());
            CollectionAssert.AreEqual(defaults.CompositionParts.Select(c => $"{c.Script} {c.RockMin}-{c.RockMax} {c.StructureMin}-{c.StructureMax} {Ranges(c.Enemies)}").ToList(),
                fromFile.CompositionParts.Select(c => $"{c.Script} {c.RockMin}-{c.RockMax} {c.StructureMin}-{c.StructureMax} {Ranges(c.Enemies)}").ToList());
            CollectionAssert.AreEqual(defaults.SettingParts.Select(s => (s.Script, s.Room, s.Background, s.HSpeed, s.VSpeed, s.LoopHorizontal, s.LoopVertical, s.RandomizeOnLoop)).ToList(),
                fromFile.SettingParts.Select(s => (s.Script, s.Room, s.Background, s.HSpeed, s.VSpeed, s.LoopHorizontal, s.LoopVertical, s.RandomizeOnLoop)).ToList());
        }

        static string Describe(LevelSetting l) =>
            $"{l.Script} rocks {l.RockMin}-{l.RockMax} still {l.StationaryPercent} {Ranges(l.Enemies)} {Describe(l.Objective)}";

        static string Describe(LevelObjectiveDefinition o) =>
            $"{o.Kind} {o.GoalScore} {o.TimeLimitSeconds} '{o.TargetName}' '{o.TargetObject}' {o.ResetScore} {o.ResetClock} '{o.Name}' [{string.Join("|", o.Dialog)}]";

        [Test]
        public void BadLevelData_FailsValidation()
        {
            var e = Assert.Throws<ContentValidationException>(() => ContentRegistry.FromJson(
                "{ \"arena\": { \"StartingStationaryPercent\": 101, \"Levels\": [ " +
                "{ \"Level\": 1, \"Script\": \"a\", \"RockMin\": 5, \"RockMax\": 2, \"StationaryPercent\": 120, \"Enemies\": { \"HydraMin\": 3, \"HydraMax\": 1, \"WormMin\": -1 } }, " +
                "{ \"Level\": 2, \"Script\": \"b\", \"Objective\": { \"Kind\": \"TimeLimit\", \"GoalScore\": -5 } }, " +
                "{ \"Level\": 3, \"Script\": \"c\", \"Objective\": { \"Kind\": \"Defeat\" } } ], " +
                "\"CompositionParts\": [ { \"Script\": \"\", \"RockMin\": 3, \"RockMax\": 1 } ] } }"));
            string m = e.Message;
            StringAssert.Contains("arena.StartingStationaryPercent", m);
            StringAssert.Contains("arena.Levels[0].RockMax", m);
            StringAssert.Contains("arena.Levels[0].StationaryPercent", m);
            StringAssert.Contains("arena.Levels[0].Enemies.HydraMax", m);
            StringAssert.Contains("arena.Levels[0].Enemies.WormMin", m);
            StringAssert.Contains("arena.Levels[1].Objective.TimeLimitSeconds", m);
            StringAssert.Contains("arena.Levels[1].Objective.GoalScore", m);
            StringAssert.Contains("arena.Levels[2].Objective.TargetObject", m);
            StringAssert.Contains("arena.CompositionParts[0].Script", m);
            StringAssert.Contains("arena.CompositionParts[0].RockMax", m);
        }

        static string ShippedGameContentJsonPath([CallerFilePath] string here = "")
        {
            string repoRoot = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here), "..", ".."));
            return Path.Combine(repoRoot, "Assets", "Unity.Rea.Hare", "Resources", "Hare", "Content", "GameContent.json");
        }
    }
}
