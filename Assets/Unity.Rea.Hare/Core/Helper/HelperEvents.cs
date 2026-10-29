using System;
using System.Collections.Generic;
using Unity.Hare.Pickups;

namespace Unity.Hare.Helper
{
    /// <summary>Something the helper field did; presentation plays these (the sprite, effects, sounds) and never writes back.</summary>
    public abstract class HelperEvent
    {
        public HelperHawk Helper;
    }

    /// <summary>create_helper made an obj_helper at the ship: its Create plays sfx_hawk.</summary>
    public sealed class HelperCalled : HelperEvent { }

    /// <summary>The first free step (and the first after each return): <c>effect_create_below(ef_ring, x, y, 1, c_white)</c>.</summary>
    public sealed class HelperRinged : HelperEvent { public Vec2 At; }

    /// <summary>
    /// The Step's <c>effect_create_below(ef_smoke, x, y, 0, colour)</c>: c_gold every free step in view, c_orange every step it
    /// is still more than 400 px from the ship on its way back.
    /// </summary>
    public sealed class HelperSmoked : HelperEvent
    {
        public Vec2 At;
        /// <summary>True for the return's c_orange, false for the free flight's c_gold.</summary>
        public bool Returning;
    }

    /// <summary>Alarm 0: <c>effect_create_below(ef_smokeup, x - hspeed, y - vspeed, 0, c_white)</c> every 40 steps.</summary>
    public sealed class HelperTrailed : HelperEvent { public Vec2 At; }

    /// <summary>Alarm 1 while turning: <c>effect_create_below(ef_spark, x - hspeed + rnd_x, y - vspeed + rnd_y, 0, spark_color)</c>.</summary>
    public sealed class HelperSparked : HelperEvent
    {
        public Vec2 At;
        public SparkleColor Color;
    }

    /// <summary>It left the view (<c>!in_view(x, y)</c>): <c>last_speed = speed; state = "return"</c>.</summary>
    public sealed class HelperReturning : HelperEvent { }

    /// <summary>It came within 400 px of the ship: <c>state = "free"; firstPass = true; speed = last_speed</c>.</summary>
    public sealed class HelperRejoined : HelperEvent { }

    /// <summary>
    /// Delete: <c>instance_create(other.x, other.y, obj_rocket_explosion)</c> (other is the helper itself in a key event),
    /// <c>effect_create_above(ef_smokeup, x, y, 0, c_black)</c>, <c>effect_create_above(ef_firework, x, y, 0, c_orange)</c>
    /// and <c>instance_destroy()</c>.
    /// </summary>
    public sealed class HelperExploded : HelperEvent { public Vec2 At; }

    /// <summary>What a <see cref="HelperCommand"/> did: whether it took effect and the events it raised, in order.</summary>
    public readonly struct HelperResult
    {
        public HelperResult(bool accepted, IReadOnlyList<HelperEvent> events)
        {
            Accepted = accepted;
            Events = events ?? Array.Empty<HelperEvent>();
        }

        public bool Accepted { get; }
        public IReadOnlyList<HelperEvent> Events { get; }
    }
}
