using System;
using System.Collections.Generic;
using Unity.Hare.Enemies;
using Unity.Hare.Flow;
using Unity.Hare.Online;
using Unity.Hare.Player;
using Unity.Hare.Session;
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
        public SessionTuning Session { get; }
        public OnlineTuning Online { get; }
        public RoomTable Rooms { get; }

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
            Session = new SessionTuning();
            Online = new OnlineTuning();
            Rooms = new RoomTable();
        }

        /// <summary>
        /// The compiled-in numbers (the original's, as reconstructed), freshly built and validated on every read.
        /// Every section's fields are public and mutable (so the binder can overlay a data file onto them), so this
        /// is a factory, not a cached singleton: two reads never share an instance, and nothing that mutates one
        /// ship's tuning (directly, or by holding a reference into it) can leak into another ship, a later test, or
        /// the "compiled-in" baseline itself.
        /// </summary>
        public static ContentRegistry Default => BuildAndValidate(null);

        /// <summary>
        /// Parses <paramref name="json"/> (see <see cref="MiniJson"/>) and overlays it on the compiled-in defaults,
        /// one section per top-level key: weapons, ammo, flight, health, weaponPhysicals, emitters, wormholes,
        /// enemies, levels, dialogue, upgrades, session, online, rooms. A section or field the file omits keeps its default. Throws
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
                Bind(sections, "session", registry.Session, errors);
                Bind(sections, "online", registry.Online, errors);
                Bind(sections, "rooms", registry.Rooms, errors);

                foreach (var key in sections.Keys)
                {
                    switch (key.ToLowerInvariant())
                    {
                        case "weapons": case "ammo": case "flight": case "health": case "weaponphysicals":
                        case "emitters": case "wormholes": case "enemies": case "levels": case "dialogue": case "upgrades": case "session": case "online": case "rooms":
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
            registry.Session.Validate(errors, "session");
            registry.Online.Validate(errors, "online");
            registry.Rooms.Validate(errors, "rooms");

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
