using System;

namespace Unity.Hare.Enemies
{
    /// <summary>Which piece of an enemy a body is.</summary>
    public enum BodyPart
    {
        /// <summary>The enemy itself (a worm's head counts).</summary>
        Main,
        /// <summary>obj_phalanx_shield.</summary>
        Shield,
        /// <summary>obj_mimic_illusion.</summary>
        Illusion,
        /// <summary>obj_worm_segment.</summary>
        Segment,
        /// <summary>obj_enemy_mine (not a par_enemy).</summary>
        Mine
    }

    /// <summary>What destroyed an enemy from the weapon's side (the laser, railgun and flame's with(inst) instance_destroy()).</summary>
    public enum BeamKind
    {
        Laser,
        Railgun,
        Flame
    }

    /// <summary>One shot of the ship's or an enemy's that reached an enemy.</summary>
    public readonly struct BulletShot
    {
        /// <summary>GML other.owner == ID: an enemy's round, which every "other.owner != ID" event ignores.</summary>
        public readonly bool FromEnemy;
        /// <summary>GML other.owner == player.id: the ship's own round, the only kind that scores.</summary>
        public readonly bool FromPlayer;
        public readonly Vec2 Position;
        public readonly float Direction;
        /// <summary>Speed in world units a second (the pushback is half a step's travel).</summary>
        public readonly float SpeedUnitsPerSecond;
        /// <summary>Identifies the round, so a shield bounces it once (GML last_impact).</summary>
        public readonly int Key;

        public BulletShot(bool fromEnemy, bool fromPlayer, Vec2 position, float direction, float speedUnitsPerSecond, int key = 0)
        {
            FromEnemy = fromEnemy;
            FromPlayer = fromPlayer;
            Position = position;
            Direction = direction;
            SpeedUnitsPerSecond = speedUnitsPerSecond;
            Key = key;
        }
    }

    /// <summary>A player ship the enemies can see: GML's global player, one per seat.</summary>
    public sealed class PlayerTarget
    {
        public PlayerTarget(PlayerId id) => Id = id;

        public PlayerId Id { get; }
        public Vec2 Position { get; internal set; }
        /// <summary>The ship's GML hspeed/vspeed, px/step (y up).</summary>
        public Vec2 VelocityPxPerStep { get; internal set; }
    }

    /// <summary>
    /// The shared par_enemy body of the gmlHARE enemies: it runs GML's step order (alarms, Step, then built-in motion
    /// along direction at speed px/step), looks at the nearest <see cref="PlayerTarget"/>, and answers every weapon
    /// with its own collision event. State changes only through <see cref="EnemyCommand"/>s run by the
    /// <see cref="EnemyWorld"/>; whatever a rule wants shown comes out as events.
    /// </summary>
    public abstract class EnemyBody
    {
        /// <summary>Collision par_enemy: x/y += lengthdir(2, away).</summary>
        public const float SeparationPxPerStep = 2f;
        /// <summary>image_alpha += 0.01 a step until opaque (enemies that fade in).</summary>
        public const float FadeInPerStep = 0.01f;

        protected const float Off = -1f;

        EnemyWorld world;
        float direction;
        float imageAngle;

        public int Id { get; private set; }
        public EnemyKind Kind { get; protected set; }
        public virtual BodyPart Part => BodyPart.Main;
        public bool IsDead { get; private set; }
        public RoomBounds Room { get; private set; }
        public Vec2 Position { get; private set; }

        /// <summary>GML direction, degrees in [0, 360) (GML wraps the built-in).</summary>
        public float Direction
        {
            get => direction;
            protected set => direction = GmlMath.Wrap(value);
        }

        /// <summary>GML speed, px/step; negative moves backwards like GML.</summary>
        public float SpeedPxPerStep { get; protected set; }

        /// <summary>GML image_angle, degrees in [0, 360); turns the hitbox with it.</summary>
        public float ImageAngle
        {
            get => imageAngle;
            protected set => imageAngle = GmlMath.Wrap(value);
        }

        public float ImageAlpha { get; protected set; } = 1f;

        /// <summary>Half the GML bounding box (unrotated), px, for distance_to_object/point.</summary>
        protected virtual Vec2 BBoxHalfPx => EnemyTable.Get(Kind).BBoxHalfPx;
        /// <summary>The bounding box centre's offset from the origin, px (y up).</summary>
        protected virtual Vec2 BBoxOffsetPx => EnemyTable.Get(Kind).BBoxOffsetPx;
        /// <summary>True for enemies with GML's Collision par_enemy push-apart event.</summary>
        public virtual bool PushesAway => EnemyTable.Get(Kind).PushesAway;
        /// <summary>False for the enemy mine: beams, flame and the ship's touch ignore it.</summary>
        public virtual bool IsParEnemy => true;

        protected IRandomSource Rng => world.Random;
        protected EnemyWorld World => world;

        /// <summary>The ship this enemy sees (GML player): the nearest seat, or null with none.</summary>
        protected PlayerTarget Target => world.NearestTarget(Position);
        protected bool HasTarget => Target != null;

        internal void Attach(EnemyWorld owner, int id, Vec2 position, RoomBounds room)
        {
            world = owner;
            Id = id;
            Position = position;
            Room = room;
        }

        // ---- Events ----

        protected T Emit<T>(T e) where T : EnemyEvent => world.Sink.Emit(Id, e);

        protected void Spawn(EnemyBody child) => world.AddChild(this, child);

        // ---- Tick ----

        /// <summary>One GML step (alarms, Step, then built-in motion), <paramref name="steps"/> GML steps long.</summary>
        internal void Tick(float steps)
        {
            if (IsDead) return;
            Alarms(steps);
            if (IsDead) return;
            StepEvent(steps);
            if (IsDead) return;
            Position += GmlMath.Dir(Direction) * GameClock.Px(SpeedPxPerStep * steps);
        }

        /// <summary>GML Create: the starting state, dealt from the world's random source.</summary>
        internal abstract void Create(EnemyOptions options);

        protected abstract void Alarms(float steps);
        protected abstract void StepEvent(float steps);

        /// <summary>Counts a GML alarm down; true (and unset) on the step it fires. Alarms fire on whole steps; the slack absorbs float drift.</summary>
        protected static bool Ring(ref float alarm, float steps)
        {
            if (alarm < 0f) return false;
            alarm -= steps;
            if (alarm > 1e-3f) return false;
            alarm = Off;
            return true;
        }

        // ---- GML geometry ----

        /// <summary>GML distance_to_point: from this enemy's bounding box (0 inside it), px.</summary>
        public float DistanceToPointPx(Vec2 point) =>
            GmlMath.BoxDistancePx(Position + BBoxOffsetPx * (1f / GameClock.PixelsPerUnit), BBoxHalfPx, point);

        /// <summary>GML distance_to_object(player), the ship taken as a point; infinite without a ship.</summary>
        public float DistanceToPlayerPx()
        {
            var t = Target;
            return t != null ? DistanceToPointPx(t.Position) : float.PositiveInfinity;
        }

        /// <summary>GML move_towards_point(p, speed): direction to <paramref name="point"/>, speed set.</summary>
        protected void MoveTowardsPoint(Vec2 point, float speedPxPerStep)
        {
            Direction = GmlMath.PointDirection(Position, point);
            SpeedPxPerStep = speedPxPerStep;
        }

        /// <summary>GML x &lt; -size || x &gt; room_width + size || ... (outside the room by more than size px).</summary>
        protected bool OutsideRoom(float sizePx)
        {
            float size = GameClock.Px(sizePx);
            return Position.X < Room.XMin - size || Position.X > Room.XMax + size || Position.Y < Room.YMin - size || Position.Y > Room.YMax + size;
        }

        protected void FadeIn(float steps)
        {
            if (ImageAlpha < 1f) ImageAlpha = Math.Min(1f, ImageAlpha + FadeInPerStep * steps);
        }

        /// <summary>GML x/y = ...: a jump, not motion.</summary>
        protected internal void Teleport(Vec2 position) => Position = position;

        /// <summary>An obj_bullet fired by this enemy (owner = ID).</summary>
        protected void Fire(Vec2 origin, float direction, float speedPxPerStep, int turret = -1) =>
            Emit(new RoundFired { Origin = origin, Direction = direction, SpeedPxPerStep = speedPxPerStep, Turret = turret });

        protected void TinyExplosionAt(Vec2 at) => Emit(new TinyExplosionRaised { At = at });

        /// <summary>The kill branch's ef_smokeup (c_black) and ef_firework (c_orange) at x, y.</summary>
        protected void DeathBurst() => Emit(new DeathBurstRaised { At = Position, Size = EnemyTable.Get(Kind).DeathBurstSize });

        protected void Debris(DebrisSpec spec) => Emit(new DebrisRaised { Spec = spec });

        // ---- Collisions ----

        /// <summary>GML Collision obj_bullet. True when the bullet is destroyed.</summary>
        internal bool Strike(in BulletShot shot) => !IsDead && BulletHit(shot);

        /// <summary>GML Collision obj_rocket. True when the rocket explodes here.</summary>
        internal bool StrikeRocket(Vec2 rocketAt) => !IsDead && RocketHit(rocketAt);

        /// <summary>GML Collision obj_rocket_explosion; most enemies have none.</summary>
        internal void StrikeExplosion()
        {
            if (!IsDead) RocketExplosionHit();
        }

        /// <summary>The laser, railgun and flame destroy any par_enemy outright, so those kills give no points, pickup or gibs.</summary>
        internal bool Strike(BeamKind beam)
        {
            if (IsDead || !IsParEnemy) return false;
            Die(beam == BeamKind.Laser ? EnemyDeathCause.Laser : beam == BeamKind.Railgun ? EnemyDeathCause.Railgun : EnemyDeathCause.Flame, 0, false, false);
            return true;
        }

        /// <summary>GML Collision obj_shieldwall, for the enemies that have one.</summary>
        internal bool StrikeShield()
        {
            if (IsDead || !HandlesShield) return false;
            ShieldHit();
            return true;
        }

        protected virtual bool HandlesShield => false;
        protected virtual void ShieldHit() { }
        protected abstract bool RocketHit(Vec2 rocketAt);
        protected abstract bool BulletHit(in BulletShot shot);
        protected virtual void RocketExplosionHit() { }

        /// <summary>The direction this body bounced the latest shot to, when it did (a phalanx shield); the world clears it per command.</summary>
        public float? Deflection { get; protected set; }

        /// <summary>GML collision with par_enemy: x/y += lengthdir(2, point_direction(other, self)).</summary>
        internal void PushAwayFrom(Vec2 other, float steps)
        {
            if (IsDead) return;
            Vec2 away = Position - other;
            Vec2 dir = away.SqrMagnitude > 1e-12f ? away.Normalized : Vec2.Right;
            Teleport(Position + dir * GameClock.Px(SeparationPxPerStep * steps));
        }

        internal void ClearDeflection() => Deflection = null;

        protected void Die(EnemyDeathCause cause, int points, bool awardsScore, bool dropsPickup)
        {
            if (IsDead) return;
            IsDead = true;
            var kill = new EnemyKill(cause, Position, points, awardsScore, dropsPickup);
            world.Retire(this);
            Emit(new EnemyKilled { Kill = kill });
        }

        /// <summary>Takes the body out of the world without a death (its view is gone).</summary>
        internal void Remove()
        {
            if (IsDead) return;
            IsDead = true;
            world.Retire(this);
        }
    }
}
