using System;
using System.Collections.Generic;

namespace Unity.Rea.Hare.Pickups
{
    /// <summary>Something the pickup field did; presentation plays these (sprites, effects, sounds) and never writes back.</summary>
    public abstract class PickupEvent { }

    /// <summary>A carrot or coin was created (random_pickup, or a level placing one).</summary>
    public sealed class PickupDropped : PickupEvent { public Pickup Pickup; }

    /// <summary>
    /// A ship's mask touched a pickup and it was taken (the pickup's collision event with obj_player). The carrot heals
    /// the ship that took it, the coin adds its <see cref="Value"/> to COINS; both leave an ef_ring and play a sound.
    /// </summary>
    public sealed class PickupCollected : PickupEvent
    {
        public Pickup Pickup;
        public PlayerId By;
    }

    /// <summary>A pickup left without being taken: a coin fell past the room's bottom, or the level was set up again.</summary>
    public sealed class PickupRemoved : PickupEvent { public Pickup Pickup; }

    /// <summary>obj_carrot's Alarm 1: <c>effect_create_below(ef_ring, x, y, 0, c_red)</c> every 120 steps.</summary>
    public sealed class CarrotPinged : PickupEvent
    {
        public Pickup Pickup;
        public Vec2 At;
    }

    /// <summary>obj_coin's Alarm 0: <c>effect_create_below(ef_spark, x - hspeed + rnd_x, y - vspeed + rnd_y, 0, spark_color)</c>.</summary>
    public sealed class CoinSparkled : PickupEvent
    {
        public Pickup Pickup;
        public Vec2 At;
        public SparkleColor Color;
    }

    /// <summary>A chip or casing was thrown.</summary>
    public sealed class ScrapThrown : PickupEvent { public Scrap Scrap; }

    /// <summary>A chip or casing fell out of the room (or its view went away).</summary>
    public sealed class ScrapRemoved : PickupEvent { public Scrap Scrap; }

    /// <summary>What a <see cref="PickupCommand"/> did: whether it took effect and the events it raised, in order.</summary>
    public readonly struct PickupResult
    {
        public PickupResult(bool accepted, IReadOnlyList<PickupEvent> events)
        {
            Accepted = accepted;
            Events = events ?? Array.Empty<PickupEvent>();
        }

        public bool Accepted { get; }
        public IReadOnlyList<PickupEvent> Events { get; }
    }
}
