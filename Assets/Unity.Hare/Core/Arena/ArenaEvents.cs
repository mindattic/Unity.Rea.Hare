using System;

namespace Unity.Hare.Arena
{
    /// <summary>Something that happened in the arena, returned by every <see cref="ArenaCommand"/>.</summary>
    public abstract class ArenaEvent
    {
        /// <summary>The seat whose command raised it.</summary>
        public PlayerId Id { get; internal set; }
    }

    /// <summary>view_visible[1] was set to <see cref="Visible"/> (obj_game's Create, or the M key).</summary>
    public sealed class MinimapToggled : ArenaEvent { public bool Visible; }

    /// <summary>
    /// The player crossed a looping edge and reappears at (<see cref="X"/>, <see cref="Y"/>), room pixels; with
    /// <see cref="RandomizePositions"/> (randomize_on_loop) random_positions follows.
    /// </summary>
    public sealed class PlayerWrapped : ArenaEvent
    {
        public float X;
        public float Y;
        /// <summary>The loop_horizontal rule (else loop_vertical).</summary>
        public bool Horizontal;
        public bool RandomizePositions;
    }

    /// <summary>
    /// The player flew past the buffer zone of an edge that does not loop: it loses a life (game_restart with none left)
    /// and reset_player puts it at (<see cref="X"/>, <see cref="Y"/>), room pixels.
    /// </summary>
    public sealed class PlayerOutOfBounds : ArenaEvent
    {
        public float X;
        public float Y;
    }

    /// <summary>
    /// obj_surface_reflection's Alarm 0: an ef_firework of <see cref="Size"/> in <see cref="Color"/> (#RRGGBB) at
    /// (<see cref="X"/>, <see cref="Y"/>), room pixels, below everything (effect_create_below).
    /// </summary>
    public sealed class SurfaceSplashed : ArenaEvent
    {
        public float X;
        public float Y;
        public int Size;
        public string Color = "";
    }

    /// <summary>
    /// obj_surface_reflection's Step: the ship went below the water line, so it is put back on it at
    /// (<see cref="X"/>, <see cref="Y"/>), room pixels, with direction and image_angle <see cref="Direction"/> (0 or 180).
    /// </summary>
    public sealed class ShipSkimmedSurface : ArenaEvent
    {
        public float X;
        public float Y;
        public float Direction;
    }

    /// <summary>random_positions: one new position per object, room pixels, in the order they were counted.</summary>
    public sealed class PositionsScattered : ArenaEvent { public Vec2[] Points = Array.Empty<Vec2>(); }
}
