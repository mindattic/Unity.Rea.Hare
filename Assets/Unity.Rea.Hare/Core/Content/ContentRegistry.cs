using System;
using System.Collections.Generic;
using Unity.Rea.Hare.Arena;
using Unity.Rea.Hare.Enemies;
using Unity.Rea.Hare.Flow;
using Unity.Rea.Hare.Helper;
using Unity.Rea.Hare.Online;
using Unity.Rea.Hare.Pickups;
using Unity.Rea.Hare.Popups;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Pointer;
using Unity.Rea.Hare.Rooms;
using Unity.Rea.Hare.Session;
using Unity.Rea.Hare.Story;
using Unity.Rea.Hare.Weapons;
using Unity.Rea.Hare.Weather;

namespace Unity.Rea.Hare.Content
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
        public StartupTuning Startup { get; }
        public ArenaTuning Arena { get; }
        public LookTuning Look { get; }
        public PickupTuning Pickups { get; }
        public WeatherTuning Weather { get; }
        /// <summary>obj_float_text, obj_tip and obj_alert (U.HARE-T27).</summary>
        public PopupTuning Popups { get; }
        /// <summary>obj_mouse: the reticle and pointer, and option_showcursor (U.HARE-T61).</summary>
        public MouseTuning Mouse { get; }
        /// <summary>The title: its menu and yes/no dialog (U.HARE-T26), its falling rabbit, stand and logo (U.HARE-T62).</summary>
        public TitleTuning Title { get; }
        /// <summary>room_intro: its falling rabbit, A and D, and the quick game it starts (U.HARE-T62).</summary>
        public IntroTuning Intro { get; }
        /// <summary>room_story's typewriter and scroll, obj_conversation, obj_typewriter and load_scenario (U.HARE-T28).</summary>
        public StoryTuning Story { get; }
        /// <summary>room_map: obj_map, obj_earthball, obj_destination, obj_white_dot, the panel and the Upgrades button (U.HARE-T19).</summary>
        public Map.MapTuning Map { get; }
        /// <summary>obj_helper and create_helper, the helper hawk (U.HARE-T66).</summary>
        public HelperTuning Helper { get; }

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
            Startup = new StartupTuning();
            Arena = new ArenaTuning();
            Look = new LookTuning();
            Pickups = new PickupTuning();
            Weather = new WeatherTuning();
            Popups = new PopupTuning();
            Mouse = new MouseTuning();
            Title = new TitleTuning();
            Intro = new IntroTuning();
            Story = new StoryTuning();
            Map = new Map.MapTuning();
            Helper = new HelperTuning();
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
        /// enemies, levels, dialogue, upgrades, session, online, rooms, startup, arena, look, pickups, weather, popups, mouse, title, intro, story, map, helper. A section or field the file omits keeps its default. Throws
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
                Bind(sections, "startup", registry.Startup, errors);
                Bind(sections, "arena", registry.Arena, errors);
                Bind(sections, "look", registry.Look, errors);
                Bind(sections, "pickups", registry.Pickups, errors);
                Bind(sections, "weather", registry.Weather, errors);
                Bind(sections, "popups", registry.Popups, errors);
                Bind(sections, "mouse", registry.Mouse, errors);
                Bind(sections, "title", registry.Title, errors);
                Bind(sections, "intro", registry.Intro, errors);
                Bind(sections, "story", registry.Story, errors);
                Bind(sections, "map", registry.Map, errors);
                Bind(sections, "helper", registry.Helper, errors);

                foreach (var key in sections.Keys)
                {
                    switch (key.ToLowerInvariant())
                    {
                        case "weapons": case "ammo": case "flight": case "health": case "weaponphysicals":
                        case "emitters": case "wormholes": case "enemies": case "levels": case "dialogue": case "upgrades": case "session": case "online": case "rooms": case "startup": case "arena": case "look": case "pickups": case "weather": case "popups": case "mouse": case "title": case "intro": case "story": case "map": case "helper":
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
            registry.Startup.Validate(errors, "startup");
            registry.Arena.Validate(errors, "arena");
            registry.Look.Validate(errors, "look");
            registry.Pickups.Validate(errors, "pickups");
            registry.Weather.Validate(errors, "weather");
            registry.Popups.Validate(errors, "popups");
            registry.Mouse.Validate(errors, "mouse");
            registry.Title.Validate(errors, "title");
            registry.Intro.Validate(errors, "intro");
            registry.Story.Validate(errors, "story");
            registry.Map.Validate(errors, "map");
            registry.Helper.Validate(errors, "helper");

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
