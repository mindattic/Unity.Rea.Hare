using System.Collections.Generic;

namespace Unity.Hare.Enemies
{
    /// <summary>What removed an enemy.</summary>
    public enum EnemyDeathCause
    {
        Bullet,
        Rocket,
        RocketExplosion,
        ShieldWall,
        Laser,
        Railgun,
        Flame,
        /// <summary>The part's owner is gone (GML: if(!instance_exists(parent)) instance_destroy()).</summary>
        Orphaned,
        /// <summary>obj_enemy_mine's own alarm[1] detonation.</summary>
        Fuse,
        /// <summary>obj_enemy_mine within 24 px of the ship.</summary>
        Proximity,
        /// <summary>Taken out of the world without dying (its view was destroyed).</summary>
        Removed
    }

    /// <summary>One enemy death.</summary>
    public readonly struct EnemyKill
    {
        public readonly EnemyDeathCause Cause;
        public readonly Vec2 Position;
        /// <summary>The GML current_score += value (and its float text) when <see cref="AwardsScore"/>.</summary>
        public readonly int Points;
        public readonly bool AwardsScore;
        /// <summary>GML random_pickup(x, y) (the pickups ticket spawns the carrot or coin).</summary>
        public readonly bool DropsPickup;

        public EnemyKill(EnemyDeathCause cause, Vec2 position, int points, bool awardsScore, bool dropsPickup)
        {
            Cause = cause;
            Position = position;
            Points = points;
            AwardsScore = awardsScore;
            DropsPickup = dropsPickup;
        }
    }

    /// <summary>The purely visual leftovers of the set-piece enemies.</summary>
    public enum DebrisKind
    {
        /// <summary>obj_meatball_gib: spr_meatball_gib1..6.</summary>
        MeatballGib,
        /// <summary>obj_worm_gib: spr_worm_gib1..8.</summary>
        WormGib,
        /// <summary>obj_worm_head_gib: spr_worm_head.</summary>
        WormHeadGib,
        /// <summary>obj_wraith_blur: an afterimage of the wraith's current sprite.</summary>
        WraithBlur
    }

    /// <summary>One piece of fading debris as its Create dealt it (so a seed reproduces the scatter).</summary>
    public readonly struct DebrisSpec
    {
        public readonly DebrisKind Kind;
        public readonly Vec2 Position;
        public readonly int Variant;
        public readonly float Direction, SpeedPxPerStep, ImageAngle, SpinPerStep, Alpha, FadePerStep;
        public readonly int Facing;

        public DebrisSpec(DebrisKind kind, Vec2 position, int variant, float direction, float speedPxPerStep, float imageAngle,
            float spinPerStep, float alpha, float fadePerStep, int facing = 1)
        {
            Kind = kind;
            Position = position;
            Variant = variant;
            Direction = direction;
            SpeedPxPerStep = speedPxPerStep;
            ImageAngle = imageAngle;
            SpinPerStep = spinPerStep;
            Alpha = alpha;
            FadePerStep = fadePerStep;
            Facing = facing;
        }
    }

    /// <summary>
    /// Something that happened in the enemy world, returned by every command. Presentation plays these (spawn a
    /// bullet, an effect, a sound, a view for a new enemy) and never writes back.
    /// </summary>
    public abstract class EnemyEvent
    {
        /// <summary>The enemy (or vortex) it came from.</summary>
        public int SourceId { get; internal set; }
    }

    /// <summary>A body joined the world (a part made with its owner, a vortex's release, a laid mine).</summary>
    public sealed class EnemySpawned : EnemyEvent { public EnemyBody Body; }

    /// <summary>An enemy died or was removed; it is already out of the world.</summary>
    public sealed class EnemyKilled : EnemyEvent { public EnemyKill Kill; }

    /// <summary>An enemy fired an obj_bullet (owner = ID): the view spawns the round. <see cref="Turret"/> is the hydra's, else -1.</summary>
    public sealed class RoundFired : EnemyEvent { public Vec2 Origin; public float Direction; public float SpeedPxPerStep; public int Turret = -1; }

    /// <summary>GML tiny_explosion (obj_tinyexplosion) at a point.</summary>
    public sealed class TinyExplosionRaised : EnemyEvent { public Vec2 At; }

    /// <summary>The kill branch's ef_smokeup and ef_firework at a point, sized 0 small, 1 medium, 2 large.</summary>
    public sealed class DeathBurstRaised : EnemyEvent { public Vec2 At; public int Size; }

    /// <summary>obj_grenade_explosion at a point.</summary>
    public sealed class GrenadeExplosionRaised : EnemyEvent { public Vec2 At; }

    /// <summary>obj_rocket_explosion at a point (a mimic's bullet kill, a mine going off).</summary>
    public sealed class RocketExplosionRaised : EnemyEvent { public Vec2 At; }

    /// <summary>A gib or afterimage was thrown.</summary>
    public sealed class DebrisRaised : EnemyEvent { public DebrisSpec Spec; }

    /// <summary>A dagger lined up and launched (ef_ring).</summary>
    public sealed class DaggerLaunched : EnemyEvent { }

    /// <summary>A dagger's smoke trail puff (ef_smokeup, below).</summary>
    public sealed class SmokePuffRaised : EnemyEvent { public Vec2 At; }

    /// <summary>A meatball's pulse (ef_ring, ef_smokeup) after spraying <see cref="Bullets"/> rounds.</summary>
    public sealed class MeatballSprayed : EnemyEvent { public int Bullets; }

    /// <summary>A worm's volley of <see cref="Slivers"/> slivers.</summary>
    public sealed class WormSpat : EnemyEvent { public int Slivers; }

    /// <summary>A wraith reappeared beside the ship at the end of a charge.</summary>
    public sealed class WraithReappeared : EnemyEvent { }

    /// <summary>A trilobyte's ef_smoke puff.</summary>
    public sealed class TrilobyteSmoked : EnemyEvent { }

    /// <summary>A mine armed (ef_ring, sfx_beep).</summary>
    public sealed class MineArmed : EnemyEvent { }

    /// <summary>A mine pinged (ef_ring).</summary>
    public sealed class MinePinged : EnemyEvent { }

    /// <summary>An emitter or wormhole joined the world.</summary>
    public sealed class VortexSpawned : EnemyEvent { public VortexBody Vortex; }

    /// <summary>A vortex released an enemy (ef_ring and ef_smoke at the vortex).</summary>
    public sealed class VortexReleased : EnemyEvent { public EnemyBody Enemy; public Vec2 At; }

    /// <summary>A wormhole emitter opened a wormhole for the next enemy of its list.</summary>
    public sealed class WormholeOpened : EnemyEvent { public WormholeBody Wormhole; }

    /// <summary>The generator's list is spent (set_level_complete).</summary>
    public sealed class LevelCompleted : EnemyEvent { }

    /// <summary>A vortex finished closing and is gone (a wormhole after its one enemy).</summary>
    public sealed class VortexClosed : EnemyEvent { }

    /// <summary>Collects the events one command raises, stamping each with its source.</summary>
    public sealed class EnemyEventSink
    {
        public List<EnemyEvent> Events { get; } = new List<EnemyEvent>();

        public T Emit<T>(int sourceId, T e) where T : EnemyEvent
        {
            e.SourceId = sourceId;
            Events.Add(e);
            return e;
        }
    }
}
