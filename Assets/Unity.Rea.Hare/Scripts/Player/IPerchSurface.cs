using UnityEngine;

namespace Unity.Rea.Hare.Player
{
    /// <summary>
    /// Something the ship lands on (U.HARE-T53): obj_player's par_rock and par_structure collision events. A rock
    /// (<see cref="Rocks.Rock"/>) is ridden, turning and drifting with the ship on it; the structures (U.HARE-T56)
    /// implement it with <see cref="SurfaceKind.Structure"/> and no motion.
    /// </summary>
    public interface IPerchSurface
    {
        SurfaceKind Kind { get; }

        /// <summary>GML other.id: unique among the surfaces in play.</summary>
        int SurfaceId { get; }

        /// <summary>The surface's x, y (its centre for a rock), world units.</summary>
        Vector2 Position { get; }

        /// <summary>GML spin, degrees a step (0 for a structure).</summary>
        float SpinPerStep { get; }

        /// <summary>GML speed, px a step (0 for a structure).</summary>
        float SpeedPxPerStep { get; }

        /// <summary>Destroyed or leaving play.</summary>
        bool IsGone { get; }

        /// <summary>The surface's collision mask: what the ship lands on and is moved out of.</summary>
        Collider2D Mask { get; }
    }
}
