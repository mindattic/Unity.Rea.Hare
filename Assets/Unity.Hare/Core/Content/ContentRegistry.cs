using System;
using System.Collections.Generic;
using Unity.Hare.Enemies;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Content
{
    /// <summary>
    /// The game's tuning and content, as data (AGENTS.md rule 4): weapon stats, ammo pools, enemy stats and point
    /// values, emitter and wormhole timings, level definitions, dialogue/story text and upgrade costs. Every
    /// section is a plain class of public fields with its own compiled-in defaults (so Core keeps working with no
    /// data file at all, e.g. under `dotnet test`); <see cref="FromJson"/> overlays a data file's content on top of
    /// those defaults and validates the result, raising one <see cref="ContentValidationException"/> that lists
    /// every problem a bad file has, not just the first.
    /// </summary>
    public sealed class ContentRegistry
    {
        public WeaponTable Weapons { get; }
        public AmmoTuning Ammo { get; }
        public FlightTuning Flight { get; }
        public HealthTuning Health { get; }
        public WeaponPhysicalTable WeaponPhysicals { get; }
        public EmitterTiming Emitters { get; }
        public WormholeTiming Wormholes { get; }
        public EnemyCatalog Enemies { get; }
        public LevelCatalog Levels { get; }
        public DialogueCatalog Dialogue { get; }
        public UpgradeCatalog Upgrades { get; }

        ContentRegistry()
        {
            Weapons = new WeaponTable();
            Ammo = new AmmoTuning();
            Flight = new FlightTuning();
            Health = new HealthTuning();
            WeaponPhysicals = new WeaponPhysicalTable();
            Emitters = new EmitterTiming();
            Wormholes = new WormholeTiming();
            Enemies = new EnemyCatalog();
            Levels = new LevelCatalog();
            Dialogue = new DialogueCatalog();
            Upgrades = new UpgradeCatalog();
        }

        /// <summary>The compiled-in numbers (the original's, as reconstructed), validated once, for use when no data file is loaded.</summary>
        public static ContentRegistry Default { get; } = BuildAndValidate(null);

        /// <summary>
        /// Parses <paramref name="json"/> (see <see cref="MiniJson"/>) and overlays it on the compiled-in defaults,
        /// one section per top-level key: weapons, ammo, flight, health, weaponPhysicals, emitters, wormholes,
        /// enemies, levels, dialogue, upgrades. A section or field the file omits keeps its default. Throws
        /// <see cref="ContentValidationException"/>, listing every problem, if the file is malformed or any value
        /// fails validation.
        /// </summary>
        public static ContentRegistry FromJson(string json)
        {
            object root;
            try
            {
                root = MiniJson.Parse(json);
            }
            catch (FormatException e)
            {
                throw new ContentValidationException(new[] { $"content: not valid JSON ({e.Message})" });
            }

            if (!(root is Dictionary<string, object> sections))
                throw new ContentValidationException(new[] { "content: the top level of the file must be an object." });

            return BuildAndValidate(sections);
        }

        static ContentRegistry BuildAndValidate(Dictionary<string, object> sections)
        {
            var registry = new ContentRegistry();
            var errors = new List<string>();

            if (sections != null)
            {
                Bind(sections, "weapons", registry.Weapons, errors);
                Bind(sections, "ammo", registry.Ammo, errors);
                Bind(sections, "flight", registry.Flight, errors);
                Bind(sections, "health", registry.Health, errors);
                Bind(sections, "weaponPhysicals", registry.WeaponPhysicals, errors);
                Bind(sections, "emitters", registry.Emitters, errors);
                Bind(sections, "wormholes", registry.Wormholes, errors);
                Bind(sections, "enemies", registry.Enemies, errors);
                Bind(sections, "levels", registry.Levels, errors);
                Bind(sections, "dialogue", registry.Dialogue, errors);
                Bind(sections, "upgrades", registry.Upgrades, errors);

                foreach (var key in sections.Keys)
                {
                    switch (key.ToLowerInvariant())
                    {
                        case "weapons": case "ammo": case "flight": case "health": case "weaponphysicals":
                        case "emitters": case "wormholes": case "enemies": case "levels": case "dialogue": case "upgrades":
                            break;
                        default:
                            errors.Add($"content.{key}: not a recognised section.");
                            break;
                    }
                }
            }

            registry.Weapons.Validate(errors, "weapons");
            registry.Ammo.Validate(errors, "ammo");
            registry.Flight.Validate(errors, "flight");
            registry.Health.Validate(errors, "health");
            registry.WeaponPhysicals.Validate(errors, "weaponPhysicals");
            registry.Emitters.Validate(errors, "emitters");
            registry.Wormholes.Validate(errors, "wormholes");
            registry.Enemies.Validate(errors, "enemies");
            registry.Levels.Validate(errors, "levels");
            registry.Dialogue.Validate(errors, "dialogue");
            registry.Upgrades.Validate(errors, "upgrades");

            if (errors.Count > 0) throw new ContentValidationException(errors);
            return registry;
        }

        /// <summary><paramref name="sections"/> is keyed case-insensitively (<see cref="MiniJson"/>'s own comparer).</summary>
        static void Bind(Dictionary<string, object> sections, string key, object target, List<string> errors)
        {
            if (sections.TryGetValue(key, out object value)) ContentBinder.Populate(target, value, key, errors);
        }
    }
}
