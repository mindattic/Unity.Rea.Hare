using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Rea.Hare.Arena
{
    /// <summary>
    /// The objects a level script spawns and makes its <c>o_defeat</c> target (U.HARE-T57): level_08's
    /// <c>boss_scrapmental = instance_create(room_width/2, room_height/2, obj_scrapmental); target_id = boss_scrapmental</c>.
    /// Each is registered by its GML object name by whatever ports it (obj_scrapmental is U.HARE-T68's); the arena asks for
    /// <see cref="LevelObjectiveDefinition.TargetObject"/> at the room's centre and completes the level once that object is
    /// destroyed (<c>!instance_exists(target_id)</c>).
    /// </summary>
    public static class LevelTargets
    {
        static readonly Dictionary<string, Func<Vector2, GameObject>> factories = new Dictionary<string, Func<Vector2, GameObject>>(StringComparer.Ordinal);

        /// <summary>Lets the arena spawn <paramref name="gmlObject"/> as a level's target; <paramref name="create"/> makes it at a world point.</summary>
        public static void Register(string gmlObject, Func<Vector2, GameObject> create)
        {
            if (string.IsNullOrEmpty(gmlObject)) throw new ArgumentException("A GML object name is needed.", nameof(gmlObject));
            factories[gmlObject] = create ?? throw new ArgumentNullException(nameof(create));
        }

        public static bool Unregister(string gmlObject) => gmlObject != null && factories.Remove(gmlObject);

        public static bool IsRegistered(string gmlObject) => gmlObject != null && factories.ContainsKey(gmlObject);

        /// <summary>instance_create(x, y, <paramref name="gmlObject"/>), or null when nothing ports that object yet.</summary>
        public static GameObject Spawn(string gmlObject, Vector2 world) =>
            gmlObject != null && factories.TryGetValue(gmlObject, out var create) ? create(world) : null;
    }
}
