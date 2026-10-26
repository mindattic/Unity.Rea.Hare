using System;
using System.Collections.Generic;
using Unity.Hare.Content;
using Unity.Hare.Enemies;
using Unity.Hare.Flow;
using Unity.Hare.Session;

namespace Unity.Hare.Arena
{
    /// <summary>
    /// A level script's "Enemies" block (U.HARE-T20): each kind's <c>X_min</c> / <c>X_max</c>, all 0 after setup_level's
    /// reset, so a script that leaves a kind out (level_04 has no worm, dagger or trilobyte lines) places none of it.
    /// <see cref="ToComposition"/> hands them to setup_level's "Generate X" blocks (<see cref="EnemyComposition"/>).
    /// </summary>
    [Serializable]
    public class LevelEnemies
    {
        public int HydraMin;
        public int HydraMax;
        public int SawbladeMin;
        public int SawbladeMax;
        public int MeatballMin;
        public int MeatballMax;
        public int WraithMin;
        public int WraithMax;
        public int PhalanxMin;
        public int PhalanxMax;
        public int MimicMin;
        public int MimicMax;
        public int WormMin;
        public int WormMax;
        public int DaggerMin;
        public int DaggerMax;
        public int TrilobyteMin;
        public int TrilobyteMax;

        /// <summary>One kind's range.</summary>
        public EnemyRange this[EnemyKind kind]
        {
            get
            {
                switch (kind)
                {
                    case EnemyKind.Hydra: return new EnemyRange(HydraMin, HydraMax);
                    case EnemyKind.Sawblade: return new EnemyRange(SawbladeMin, SawbladeMax);
                    case EnemyKind.Meatball: return new EnemyRange(MeatballMin, MeatballMax);
                    case EnemyKind.Wraith: return new EnemyRange(WraithMin, WraithMax);
                    case EnemyKind.Phalanx: return new EnemyRange(PhalanxMin, PhalanxMax);
                    case EnemyKind.Mimic: return new EnemyRange(MimicMin, MimicMax);
                    case EnemyKind.Worm: return new EnemyRange(WormMin, WormMax);
                    case EnemyKind.Dagger: return new EnemyRange(DaggerMin, DaggerMax);
                    case EnemyKind.Trilobyte: return new EnemyRange(TrilobyteMin, TrilobyteMax);
                    default: return default;
                }
            }
            set
            {
                switch (kind)
                {
                    case EnemyKind.Hydra: HydraMin = value.Min; HydraMax = value.Max; break;
                    case EnemyKind.Sawblade: SawbladeMin = value.Min; SawbladeMax = value.Max; break;
                    case EnemyKind.Meatball: MeatballMin = value.Min; MeatballMax = value.Max; break;
                    case EnemyKind.Wraith: WraithMin = value.Min; WraithMax = value.Max; break;
                    case EnemyKind.Phalanx: PhalanxMin = value.Min; PhalanxMax = value.Max; break;
                    case EnemyKind.Mimic: MimicMin = value.Min; MimicMax = value.Max; break;
                    case EnemyKind.Worm: WormMin = value.Min; WormMax = value.Max; break;
                    case EnemyKind.Dagger: DaggerMin = value.Min; DaggerMax = value.Max; break;
                    case EnemyKind.Trilobyte: TrilobyteMin = value.Min; TrilobyteMax = value.Max; break;
                    default: throw new ArgumentOutOfRangeException(nameof(kind), kind, "setup_level places no such kind.");
                }
            }
        }

        /// <summary>Fluent setter: <c>new LevelEnemies().With(EnemyKind.Hydra, 1, 3)</c>.</summary>
        public LevelEnemies With(EnemyKind kind, int min, int max)
        {
            this[kind] = new EnemyRange(min, max);
            return this;
        }

        /// <summary>A copy (the compiled-in tables share nothing with what a caller changes).</summary>
        public LevelEnemies Clone() => (LevelEnemies)MemberwiseClone();

        /// <summary>The ranges as setup_level's Generate blocks take them.</summary>
        public EnemyComposition ToComposition()
        {
            var composition = new EnemyComposition();
            foreach (var kind in EnemyComposition.PlacementOrder) composition[kind] = this[kind];
            return composition;
        }

        internal void Validate(List<string> errors, string path)
        {
            foreach (var kind in EnemyComposition.PlacementOrder)
            {
                var range = this[kind];
                ValidationHelpers.NonNegative(errors, $"{path}.{kind}Min", range.Min);
                if (range.Max < range.Min) errors.Add($"{path}.{kind}Max: must be at least {kind}Min ({range.Min}) (was {range.Max}).");
            }
        }
    }

    /// <summary>
    /// A level script's "Objective" block as data (U.HARE-T20): <c>objective</c>, <c>goal_score</c>,
    /// <c>time_limit_seconds</c>, <c>target_name</c> and which of <c>current_score = 0</c> / <c>time_elapsed_seconds = 0</c>
    /// it runs; <see cref="ToObjective"/> is what the session's <see cref="BeginLevel"/> takes. level_08's "Spawn Boss"
    /// block (<c>instance_create(room_width/2, room_height/2, obj_scrapmental)</c>, then <c>target_id</c> = it) is
    /// <see cref="TargetObject"/>. The level_objective_* scripts also set <c>level_name</c> and <c>level_dialog</c>
    /// (<see cref="Name"/>, <see cref="Dialog"/>); the level scripts leave both empty.
    /// </summary>
    [Serializable]
    public class LevelObjectiveDefinition
    {
        /// <summary>GML: objective.</summary>
        public ObjectiveKind Kind = ObjectiveKind.Points;
        /// <summary>GML: goal_score.</summary>
        public int GoalScore;
        /// <summary>GML: time_limit_seconds; 0 when the script does not set it. Only <see cref="ObjectiveKind.TimeLimit"/> reads it.</summary>
        public int TimeLimitSeconds;
        /// <summary>GML: target_name; empty when the script does not set it (the session keeps its own).</summary>
        public string TargetName = "";
        /// <summary>The object the script creates at the room's centre and makes target_id; empty for none.</summary>
        public string TargetObject = "";
        /// <summary>Whether the script runs <c>current_score = 0</c>.</summary>
        public bool ResetScore = true;
        /// <summary>Whether the script runs <c>time_elapsed_seconds = 0</c>.</summary>
        public bool ResetClock = true;
        /// <summary>GML: level_name, set by a level_objective_* script; empty when the objective leaves it alone.</summary>
        public string Name = "";
        /// <summary>GML: level_dialog[0..level_dialog_count-1] (GameMaker's '#' line breaks kept), set by a level_objective_* script.</summary>
        public string[] Dialog = Array.Empty<string>();

        public LevelObjectiveDefinition() { }

        public LevelObjectiveDefinition(ObjectiveKind kind, int goalScore, int timeLimitSeconds = 0, string targetName = "", string targetObject = "",
            bool resetScore = true, bool resetClock = true, string name = "", string[] dialog = null)
        {
            Kind = kind;
            GoalScore = goalScore;
            TimeLimitSeconds = timeLimitSeconds;
            TargetName = targetName ?? "";
            TargetObject = targetObject ?? "";
            ResetScore = resetScore;
            ResetClock = resetClock;
            Name = name ?? "";
            Dialog = dialog ?? Array.Empty<string>();
        }

        /// <summary>The objective the session begins the level with.</summary>
        public LevelObjective ToObjective() =>
            new LevelObjective(Kind, GoalScore, TimeLimitSeconds, string.IsNullOrEmpty(TargetName) ? null : TargetName, ResetScore, ResetClock);

        internal void Validate(List<string> errors, string path)
        {
            if (!Enum.IsDefined(typeof(ObjectiveKind), Kind)) errors.Add($"{path}.Kind: not an objective kind (was {(int)Kind}).");
            ValidationHelpers.NonNegative(errors, path + ".GoalScore", GoalScore);
            ValidationHelpers.NonNegative(errors, path + ".TimeLimitSeconds", TimeLimitSeconds);
            if (Kind == ObjectiveKind.TimeLimit && TimeLimitSeconds <= 0)
                errors.Add($"{path}.TimeLimitSeconds: a TimeLimit objective needs a limit (was {TimeLimitSeconds}).");
            if (Kind == ObjectiveKind.Defeat && string.IsNullOrWhiteSpace(TargetObject))
                errors.Add($"{path}.TargetObject: a Defeat objective needs the object it spawns and targets.");
            if (TargetName == null) errors.Add(path + ".TargetName: must not be null (empty for none).");
            if (TargetObject == null) errors.Add(path + ".TargetObject: must not be null (empty for none).");
            if (Name == null) errors.Add(path + ".Name: must not be null (empty for none).");
            if (Dialog == null) errors.Add(path + ".Dialog: must not be null (an empty list for none).");
            else
                for (int i = 0; i < Dialog.Length; i++)
                    if (Dialog[i] == null) errors.Add($"{path}.Dialog[{i}]: must not be null.");
        }
    }

    /// <summary>
    /// level_composition_00 / level_composition_01 (U.HARE-T20): a level's "Surfaces" and "Enemies" blocks on their own,
    /// for load_level. They set no stationary_percent and no trilobytes.
    /// </summary>
    [Serializable]
    public class LevelComposition
    {
        public string Script = "";
        public int RockMin;
        public int RockMax;
        public int StructureMin;
        public int StructureMax;
        public LevelEnemies Enemies = new LevelEnemies();

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Script", Script);
            ValidationHelpers.NonNegative(errors, path + ".RockMin", RockMin);
            if (RockMax < RockMin) errors.Add($"{path}.RockMax: must be at least RockMin ({RockMin}) (was {RockMax}).");
            ValidationHelpers.NonNegative(errors, path + ".StructureMin", StructureMin);
            if (StructureMax < StructureMin) errors.Add($"{path}.StructureMax: must be at least StructureMin ({StructureMin}) (was {StructureMax}).");
            if (Enemies == null) errors.Add(path + ".Enemies: must not be null.");
            else Enemies.Validate(errors, path + ".Enemies");
        }
    }

    /// <summary>
    /// level_setting_00 / level_setting_01 (U.HARE-T20): a level's setting on its own, for load_level: <c>next_room</c>,
    /// background slot 0 and its speeds, and the loop flags. They leave level_name and slot 1 alone.
    /// </summary>
    [Serializable]
    public class LevelSettingPart
    {
        public string Script = "";
        /// <summary>GML: next_room.</summary>
        public RoomId Room = RoomId.GameScreen;
        public string Background = "";
        public int HSpeed;
        public int VSpeed;
        public bool LoopHorizontal;
        public bool LoopVertical;
        public bool RandomizeOnLoop;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Script", Script);
            if (!Enum.IsDefined(typeof(RoomId), Room)) errors.Add($"{path}.Room: not a room (was {(int)Room}).");
        }
    }
}
