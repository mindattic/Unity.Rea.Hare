using UnityEngine;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// Ports obj_wormhole and create_wormhole (gmlHARE): a one-shot vortex for a single enemy of
    /// the generator's list. create_wormhole sets its scale per kind (see
    /// <see cref="EnemySpawner.WormholeScale"/>) and drops it at a random point at least sprite
    /// width x scale inside the room. Create waits one_tick (room_speed * 2 = 240 steps) idle; open
    /// grows for 240 steps; spawn releases the enemy unconditionally (no cap, no pause check) and
    /// waits 240; close shrinks to nothing and destroys the wormhole.
    /// </summary>
    public class Wormhole : Vortex
    {
        /// <summary>one_tick = room_speed * 2.</summary>
        public const int OneTickSteps = 240;

        /// <summary>The enemy this wormhole released, once it has.</summary>
        public Component Spawned { get; private set; }

        /// <summary>
        /// create_wormhole(kind). The original sized its placement margin with the caller's
        /// (undefined) scale; this uses the wormhole's own, which is what it meant.
        /// </summary>
        public static Wormhole Create(EnemyKind kind, Rect? room = null, PlayerShipController player = null, Vector2? position = null)
        {
            var wormhole = new GameObject("Wormhole").AddComponent<Wormhole>();
            wormhole.Init(kind, room, player, position);
            return wormhole;
        }

        public void Init(EnemyKind kind, Rect? room = null, PlayerShipController player = null, Vector2? position = null)
        {
            Instance = kind;
            Scale = EnemySpawner.WormholeScale(kind);
            Player = player;
            SetupVortex(room);
            Vector2 p = position ?? EnemySpawner.RandomPointInside(Room, SpriteWidthPx * Scale);
            transform.position = new Vector3(p.x, p.y, transform.position.z);
            SetAlarm(OneTickSteps);
        }

        protected override void StepEvent(float steps)
        {
            switch (Mode)
            {
                case VortexMode.Open:
                    if (FirstPass)
                    {
                        FirstPass = false;
                        SetAlarm(OneTickSteps);
                    }
                    Grow(steps);
                    break;

                case VortexMode.Spawn:
                    if (FirstPass)
                    {
                        FirstPass = false;
                        Spawned = Release();
                        SetAlarm(OneTickSteps);
                    }
                    break;

                case VortexMode.Close:
                    FirstPass = false;
                    if (Shrink(steps)) Remove();
                    break;
            }
        }
    }
}
