using UnityEngine;
using Unity.Hare.Player;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// The view on <see cref="WormholeBody"/> (obj_wormhole and create_wormhole, gmlHARE): a one-shot vortex for a single
    /// enemy of the generator's list. Core waits, opens, releases the enemy unconditionally and closes; this draws it
    /// and is removed when Core says it has shrunk to nothing.
    /// </summary>
    public class Wormhole : Vortex
    {
        /// <summary>one_tick = room_speed * 2.</summary>
        public static int OneTickSteps => WormholeBody.OneTickSteps;

        /// <summary>The enemy this wormhole released, once it has.</summary>
        public Component Spawned => ((WormholeBody)Core).Spawned == null ? null : EnemyDirector.ViewOf<Component>(((WormholeBody)Core).Spawned.Id);

        /// <summary>create_wormhole(kind).</summary>
        public static Wormhole Create(EnemyKind kind, Rect? room = null, PlayerShipController player = null, Vector2? position = null)
        {
            var wormhole = new GameObject("Wormhole").AddComponent<Wormhole>();
            wormhole.Init(kind, room, player, position);
            return wormhole;
        }

        public void Init(EnemyKind kind, Rect? room = null, PlayerShipController player = null, Vector2? position = null)
        {
            var result = EnemyDirector.Execute(new AddWormhole(kind, room?.ToBounds(), position?.ToVec2()), this);
            Attach(result.Vortex, player);
        }
    }
}
