using System.Linq;
using UnityEngine;
using Unity.Hare.Player;
using Unity.Hare.Weapons;
using Object = UnityEngine.Object;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// The spawnable par_enemy objects, numbered as generate_enemy_list's index_* values
    /// (index_hydra = 1 ... index_trilobyte = 9).
    /// </summary>
    public enum EnemyKind
    {
        Hydra = 1,
        Sawblade = 2,
        Dagger = 3,
        Mimic = 4,
        Wraith = 5,
        Meatball = 6,
        Phalanx = 7,
        Worm = 8,
        Trilobyte = 9
    }

    /// <summary>
    /// GML instance_create(x, y, instance) and instance_number(instance) for an
    /// <see cref="EnemyKind"/>, plus the per-kind vortex scales the spawners use.
    /// </summary>
    public static class EnemySpawner
    {
        /// <summary>GML instance_create(x, y, instance); new_instance.ID = new_instance is implicit.</summary>
        public static Component Spawn(EnemyKind kind, Vector2 position, Rect? room = null, PlayerShipController player = null)
        {
            var go = new GameObject(kind.ToString());
            switch (kind)
            {
                case EnemyKind.Dagger:
                {
                    var dagger = go.AddComponent<Dagger>();
                    dagger.Init(position, room);
                    return dagger;
                }
                case EnemyKind.Sawblade: { var e = go.AddComponent<Sawblade>(); e.Init(position, room, player); return e; }
                case EnemyKind.Mimic: { var e = go.AddComponent<Mimic>(); e.Init(position, room, player); return e; }
                case EnemyKind.Wraith: { var e = go.AddComponent<Wraith>(); e.Init(position, room, player); return e; }
                case EnemyKind.Meatball: { var e = go.AddComponent<Meatball>(); e.Init(position, room, player); return e; }
                case EnemyKind.Phalanx: { var e = go.AddComponent<Phalanx>(); e.Init(position, room, player); return e; }
                case EnemyKind.Worm: { var e = go.AddComponent<Worm>(); e.Init(position, room, player); return e; }
                case EnemyKind.Trilobyte: { var e = go.AddComponent<Trilobyte>(); e.Init(position, room, player); return e; }
                default: { var e = go.AddComponent<Hydra>(); e.Init(position, room, player); return e; }
            }
        }

        /// <summary>GML instance_number(instance): live instances of that object (a worm counts its head only).</summary>
        public static int Count(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Dagger: return Object.FindObjectsByType<Dagger>(FindObjectsSortMode.None).Count(d => d != null && !d.IsDead);
                case EnemyKind.Sawblade: return Live<Sawblade>();
                case EnemyKind.Mimic: return Live<Mimic>();
                case EnemyKind.Wraith: return Live<Wraith>();
                case EnemyKind.Meatball: return Live<Meatball>();
                case EnemyKind.Phalanx: return Live<Phalanx>();
                case EnemyKind.Worm: return Live<Worm>();
                case EnemyKind.Trilobyte: return Live<Trilobyte>();
                default: return Live<Hydra>();
            }
        }

        static int Live<T>() where T : SetPieceEnemy =>
            Object.FindObjectsByType<T>(FindObjectsSortMode.None).Count(e => e != null && !e.IsDead);

        /// <summary>setup_level's emitter.scale for each par_enemy_emitter it places.</summary>
        public static float EmitterScale(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Meatball: return 0.70f;
                case EnemyKind.Wraith:
                case EnemyKind.Phalanx: return 0.35f;
                case EnemyKind.Worm: return 0.50f;
                default: return 0.25f; // hydra, sawblade, dagger, trilobyte (mimics get no emitter)
            }
        }

        /// <summary>create_wormhole's wormhole.scale for each kind.</summary>
        public static float WormholeScale(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Dagger:
                case EnemyKind.Phalanx:
                case EnemyKind.Wraith: return 0.35f;
                case EnemyKind.Worm: return 0.5f;
                case EnemyKind.Mimic:
                case EnemyKind.Meatball: return 1.0f;
                default: return 0.25f; // hydra, sawblade, trilobyte
            }
        }

        /// <summary>
        /// A whole-pixel point at least <paramref name="bufferPx"/> inside <paramref name="room"/>:
        /// GML x = irandom_range(buffer, room_width - buffer), y likewise (y grows downward).
        /// </summary>
        public static Vector2 RandomPointInside(Rect room, float bufferPx)
        {
            float widthPx = room.width * Gml.PixelsPerUnit;
            float heightPx = room.height * Gml.PixelsPerUnit;
            int x = IRandom(Mathf.RoundToInt(bufferPx), Mathf.RoundToInt(widthPx - bufferPx));
            int y = IRandom(Mathf.RoundToInt(bufferPx), Mathf.RoundToInt(heightPx - bufferPx));
            return new Vector2(room.xMin + Gml.Px(x), room.yMax - Gml.Px(y));
        }

        /// <summary>GML irandom_range(a, b), inclusive; tolerates a &gt; b like GML.</summary>
        public static int IRandom(int a, int b) => a <= b ? Random.Range(a, b + 1) : Random.Range(b, a + 1);
    }
}
