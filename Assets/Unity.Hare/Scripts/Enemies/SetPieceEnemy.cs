using System;
using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Player;
using Unity.Hare.Weapons;
using Object = UnityEngine.Object;

namespace Unity.Hare.Enemies
{
    /// <summary>What removed a set-piece enemy.</summary>
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
        Proximity
    }

    /// <summary>One set-piece enemy death, raised by <see cref="SetPieceEnemy.Killed"/>.</summary>
    public readonly struct EnemyKill
    {
        public readonly EnemyDeathCause Cause;
        public readonly Vector2 Position;
        /// <summary>The GML current_score += value (and its float text) when <see cref="AwardsScore"/>.</summary>
        public readonly int Points;
        public readonly bool AwardsScore;
        /// <summary>GML random_pickup(x, y) (UH-T23 spawns the carrot or coin).</summary>
        public readonly bool DropsPickup;

        public EnemyKill(EnemyDeathCause cause, Vector2 position, int points, bool awardsScore, bool dropsPickup)
        {
            Cause = cause;
            Position = position;
            Points = points;
            AwardsScore = awardsScore;
            DropsPickup = dropsPickup;
        }
    }

    /// <summary>
    /// The shared par_enemy body of the gmlHARE set-piece enemies (obj_hydra, obj_phalanx and its
    /// shield, obj_meatball, obj_wraith, obj_sawblade, obj_worm and its segments, obj_trilobyte,
    /// obj_mimic and its illusion). It runs GML's step order (alarms, Step, then built-in motion
    /// along direction at speed px/step), knows the player (GML's global player instance), and
    /// routes every weapon to the enemy's own collision event: bullets to
    /// <see cref="BulletHit"/>, rockets to <see cref="RocketHit"/>, rocket explosions to
    /// <see cref="RocketExplosionHit"/>. The laser, railgun and flame destroy any par_enemy outright
    /// from their side (with(inst) instance_destroy()), so those kills give no points, pickup or
    /// gibs. Touching the ship hurts it (obj_player's par_enemy collision).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class SetPieceEnemy : MonoBehaviour, IEnemyTarget, IProjectileTarget, IRocketTarget
    {
        /// <summary>Collision par_enemy: x/y += lengthdir(2, away).</summary>
        public const float SeparationPxPerStep = 2f;
        /// <summary>image_alpha += 0.01 a step until opaque (enemies that fade in).</summary>
        public const float FadeInPerStep = 0.01f;

        protected const float Off = -1f;

        Rect room;
        Rigidbody2D body;
        PlayerShipController player;
        bool dead;
        float direction;
        float imageAngle;

        public Rect Room => room;
        public bool IsDead => dead;
        public Vector2 Position => transform.position;

        /// <summary>GML direction, degrees in [0, 360) (GML wraps the built-in).</summary>
        public float Direction
        {
            get => direction;
            protected set => direction = Mathf.Repeat(value, 360f);
        }

        /// <summary>GML speed, px/step; negative moves backwards like GML.</summary>
        public float SpeedPxPerStep { get; protected set; }

        /// <summary>GML image_angle, degrees in [0, 360); turns the hitbox with it.</summary>
        public float ImageAngle
        {
            get => imageAngle;
            protected set
            {
                imageAngle = Mathf.Repeat(value, 360f);
                transform.rotation = Quaternion.Euler(0f, 0f, imageAngle);
            }
        }

        public float ImageAlpha { get; protected set; } = 1f;

        /// <summary>GML player. Found in the scene when not given.</summary>
        public PlayerShipController Player
        {
            get => player;
            set => player = value;
        }

        /// <summary>Raised once, just before the enemy is removed.</summary>
        public event Action<SetPieceEnemy, EnemyKill> Killed;

        /// <summary>Half the GML bounding box (unrotated), in px, for distance_to_object/point.</summary>
        protected abstract Vector2 BBoxHalfPx { get; }
        /// <summary>The bounding box centre's offset from the origin, in px (Unity axes, y up).</summary>
        protected virtual Vector2 BBoxOffsetPx => Vector2.zero;
        /// <summary>True for enemies with GML's Collision par_enemy push-apart event.</summary>
        protected virtual bool PushesAwayFromEnemies => false;

        /// <summary>Shared GML Create: kinematic trigger body, hurtful contact, position, room, player.</summary>
        protected void Setup(Vector2 position, Rect? room, PlayerShipController player)
        {
            this.room = room ?? Gml.Room;
            this.player = player != null ? player : FindPlayer();
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            if (GetComponent<PlayerDamageSource>() == null) gameObject.AddComponent<PlayerDamageSource>();
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            dead = false;
        }

        protected CircleCollider2D UseCircle(float radiusPx, Vector2 offsetPx = default)
        {
            var c = GetComponent<CircleCollider2D>();
            if (c == null) c = gameObject.AddComponent<CircleCollider2D>();
            c.isTrigger = true;
            c.radius = Gml.Px(radiusPx);
            c.offset = new Vector2(Gml.Px(offsetPx.x), Gml.Px(offsetPx.y));
            return c;
        }

        protected BoxCollider2D UseBox(Vector2 sizePx, Vector2 offsetPx = default)
        {
            var c = GetComponent<BoxCollider2D>();
            if (c == null) c = gameObject.AddComponent<BoxCollider2D>();
            c.isTrigger = true;
            c.size = new Vector2(Gml.Px(sizePx.x), Gml.Px(sizePx.y));
            c.offset = new Vector2(Gml.Px(offsetPx.x), Gml.Px(offsetPx.y));
            return c;
        }

        /// <summary>An ellipse mask (GML colkind 2) as a capsule.</summary>
        protected CapsuleCollider2D UseEllipse(Vector2 sizePx, Vector2 offsetPx = default)
        {
            var c = GetComponent<CapsuleCollider2D>();
            if (c == null) c = gameObject.AddComponent<CapsuleCollider2D>();
            c.isTrigger = true;
            c.direction = sizePx.x > sizePx.y ? CapsuleDirection2D.Horizontal : CapsuleDirection2D.Vertical;
            c.size = new Vector2(Gml.Px(sizePx.x), Gml.Px(sizePx.y));
            c.offset = new Vector2(Gml.Px(offsetPx.x), Gml.Px(offsetPx.y));
            return c;
        }

        static PlayerShipController FindPlayer() => Object.FindAnyObjectByType<PlayerShipController>();

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>One GML step (alarms, Step, then built-in motion), scaled to <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            if (dead) return;
            if (player == null) player = FindPlayer();
            float steps = dt * Gml.StepsPerSecond;
            Alarms(steps);
            if (dead) return;
            StepEvent(steps);
            if (dead) return;

            Vector2 next = Position + Gml.Dir(Direction) * Gml.Px(SpeedPxPerStep * steps);
            if (Application.isPlaying && body != null) body.MovePosition(next);
            else transform.position = new Vector3(next.x, next.y, transform.position.z);
        }

        protected abstract void Alarms(float steps);
        protected abstract void StepEvent(float steps);

        /// <summary>Counts a GML alarm down; true (and unset) on the step it fires.</summary>
        protected static bool Ring(ref float alarm, float steps)
        {
            if (alarm < 0f) return false;
            alarm -= steps;
            if (alarm > 1e-3f) return false;
            alarm = Off;
            return true;
        }

        /// <summary>GML irandom_range(a, b), inclusive.</summary>
        protected static int IRandom(int min, int max) => UnityEngine.Random.Range(min, max + 1);

        // ---- GML geometry ----

        protected bool HasPlayer => player != null;
        protected Vector2 PlayerPosition => player.transform.position;

        /// <summary>The ship's GML hspeed/vspeed, px/step.</summary>
        protected Vector2 PlayerVelocityPxPerStep
        {
            get
            {
                var rb = player != null ? player.GetComponent<Rigidbody2D>() : null;
                return rb != null ? rb.linearVelocity * Gml.PixelsPerUnit / Gml.StepsPerSecond : Vector2.zero;
            }
        }

        /// <summary>GML distance_to_point: from this enemy's bounding box (0 inside it), in px.</summary>
        public float DistanceToPointPx(Vector2 point) => BoxDistancePx(Position + PxVec(BBoxOffsetPx), BBoxHalfPx, point);

        /// <summary>GML distance_to_object(player), with the ship taken as a point; infinite without a ship.</summary>
        public float DistanceToPlayerPx() => HasPlayer ? DistanceToPointPx(PlayerPosition) : float.PositiveInfinity;

        static Vector2 PxVec(Vector2 px) => new Vector2(Gml.Px(px.x), Gml.Px(px.y));

        /// <summary>Px gap between an axis-aligned box (centre in world units, half size in px) and a point.</summary>
        public static float BoxDistancePx(Vector2 centre, Vector2 halfPx, Vector2 point)
        {
            Vector2 d = (point - centre) * Gml.PixelsPerUnit;
            float dx = Mathf.Max(0f, Mathf.Abs(d.x) - halfPx.x);
            float dy = Mathf.Max(0f, Mathf.Abs(d.y) - halfPx.y);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>GML point_direction(a, b): degrees in [0, 360), 0 for the same point.</summary>
        public static float PointDirection(Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            if (d.sqrMagnitude <= Mathf.Epsilon) return 0f;
            return Mathf.Repeat(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, 360f);
        }

        /// <summary>GML move_towards_point(p, speed): direction to <paramref name="point"/>, speed set.</summary>
        protected void MoveTowardsPoint(Vector2 point, float speedPxPerStep)
        {
            Direction = PointDirection(Position, point);
            SpeedPxPerStep = speedPxPerStep;
        }

        /// <summary>GML x &lt; -size || x &gt; room_width + size || ... (outside the room by more than size px).</summary>
        protected bool OutsideRoom(float sizePx)
        {
            float size = Gml.Px(sizePx);
            Vector2 p = Position;
            return p.x < room.xMin - size || p.x > room.xMax + size || p.y < room.yMin - size || p.y > room.yMax + size;
        }

        protected void FadeIn(float steps)
        {
            if (ImageAlpha < 1f) ImageAlpha = Mathf.Min(1f, ImageAlpha + FadeInPerStep * steps);
        }

        /// <summary>GML x/y = ...: a jump, not motion.</summary>
        protected void Teleport(Vector2 position) => transform.position = new Vector3(position.x, position.y, transform.position.z);

        /// <summary>An obj_bullet fired by this enemy (owner = ID).</summary>
        protected Bullet Fire(Vector2 origin, float direction, float speedPxPerStep) =>
            EnemyRounds.Spawn(gameObject, origin, direction, speedPxPerStep);

        // ---- Collisions ----

        /// <summary>The ship's own rounds (GML other.owner == player.id): the ones that score.</summary>
        protected static bool IsPlayerRound(Bullet bullet) =>
            bullet != null && !bullet.FromEnemy && bullet.Owner != null && bullet.Owner.GetComponentInParent<PlayerShipController>() != null;

        /// <summary>GML other.owner == ID: an enemy's round, which every "other.owner != ID" event ignores.</summary>
        protected static bool IsEnemyRound(Bullet bullet) => bullet != null && bullet.FromEnemy;

        protected static Vector2 PositionOf(Bullet bullet, Vector2 fallback) => bullet != null ? (Vector2)bullet.transform.position : fallback;

        protected static Vector2 PositionOf(Rocket rocket, Vector2 fallback) => rocket != null ? (Vector2)rocket.transform.position : fallback;

        /// <summary>GML tiny_explosion = instance_create(other.x, other.y, obj_tinyexplosion) for a bullet.</summary>
        protected TinyExplosion TinyExplosionAt(Bullet bullet) => TinyExplosion.Spawn(PositionOf(bullet, Position));

        /// <summary>GML tiny_explosion = instance_create(other.x, other.y, obj_tinyexplosion) for a rocket.</summary>
        protected TinyExplosion TinyExplosionAt(Rocket rocket) => TinyExplosion.Spawn(PositionOf(rocket, Position));

        /// <summary>The kill branch's ef_smokeup (c_black) and ef_firework (c_orange) at x, y.</summary>
        protected void DeathBurst(int size) => DeathEffects.Burst(Position, size);

        /// <summary>Beams and flame destroy the instance; rockets and explosions run its own events.</summary>
        public void OnWeaponHit(in WeaponHit hit)
        {
            if (dead) return;
            switch (hit.Kind)
            {
                case WeaponHitKind.Laser: Die(EnemyDeathCause.Laser, 0, false, false); break;
                case WeaponHitKind.Railgun: Die(EnemyDeathCause.Railgun, 0, false, false); break;
                case WeaponHitKind.Flame: Die(EnemyDeathCause.Flame, 0, false, false); break;
                case WeaponHitKind.Rocket: RocketHit(null); break;
                case WeaponHitKind.RocketExplosion: RocketExplosionHit(); break;
            }
        }

        public bool OnRocketHit(Rocket rocket) => !dead && RocketHit(rocket);

        public bool OnProjectileHit(Bullet bullet) => !dead && BulletHit(bullet);

        /// <summary>GML Collision obj_rocket. True when the rocket explodes here.</summary>
        protected abstract bool RocketHit(Rocket rocket);

        /// <summary>GML Collision obj_bullet. True when the bullet is destroyed.</summary>
        protected abstract bool BulletHit(Bullet bullet);

        /// <summary>GML Collision obj_rocket_explosion; most enemies have none.</summary>
        protected virtual void RocketExplosionHit() { }

        void OnTriggerStay2D(Collider2D other)
        {
            if (dead || !PushesAwayFromEnemies || other == null) return;
            var enemy = other.GetComponentInParent<IEnemyTarget>() as Component;
            if (enemy == null || enemy.gameObject == gameObject) return;
            PushAwayFrom(enemy.transform.position, Time.fixedDeltaTime * Gml.StepsPerSecond);
        }

        /// <summary>GML collision with par_enemy: x/y += lengthdir(2, point_direction(other, self)).</summary>
        public void PushAwayFrom(Vector2 other, float steps = 1f)
        {
            Vector2 away = Position - other;
            Vector2 dir = away.sqrMagnitude > Mathf.Epsilon ? away.normalized : Vector2.right;
            Teleport(Position + dir * Gml.Px(SeparationPxPerStep * steps));
        }

        protected void Die(EnemyDeathCause cause, int points, bool awardsScore, bool dropsPickup)
        {
            if (dead) return;
            dead = true;
            Killed?.Invoke(this, new EnemyKill(cause, Position, points, awardsScore, dropsPickup));
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }
    }

    /// <summary>obj_bullet as the enemies fire it: owner = ID, hurting the ship on contact.</summary>
    public static class EnemyRounds
    {
        /// <summary>spr_bullet's half width.</summary>
        public const float RadiusPx = 4f;
        /// <summary>GML globals buffer_zone: bullets die this far outside the room.</summary>
        public const float BufferZonePx = 400f;

        /// <summary>Every enemy round, as it is fired (sounds and sprites hang off this).</summary>
        public static event Action<Bullet> Fired;

        public static Bullet Spawn(GameObject owner, Vector2 origin, float direction, float speedPxPerStep)
        {
            var go = new GameObject("EnemyBullet");
            go.transform.position = new Vector3(origin.x, origin.y, 0f);
            var bullet = go.AddComponent<Bullet>();
            // The room check becomes a lifetime long enough to cross the room plus buffer_zone.
            float reachPx = new Vector2(Gml.RoomWidthPx, Gml.RoomHeightPx).magnitude + 2f * BufferZonePx;
            float speed = Mathf.Max(0.01f, Mathf.Abs(speedPxPerStep));
            bullet.Init(owner, direction, Gml.Px(speedPxPerStep) * Gml.StepsPerSecond, 1, Gml.Steps(reachPx / speed), Gml.Px(RadiusPx));
            bullet.MarkFromEnemy();
            go.AddComponent<PlayerDamageSource>().DestroyOnHit = true;
            Fired?.Invoke(bullet);
            return bullet;
        }
    }
}
