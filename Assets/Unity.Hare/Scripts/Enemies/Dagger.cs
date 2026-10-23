using System;
using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Enemies
{
    /// <summary>What obj_dagger is in when it moves (GML mode).</summary>
    public enum DaggerMode
    {
        /// <summary>Stopped, turning its blade 0.5 degrees/step toward its new heading.</summary>
        Rotate,
        /// <summary>Lined up: accelerating along its heading, trailing smoke.</summary>
        Fly,
        /// <summary>Braking to a halt before picking a new heading.</summary>
        Stop
    }

    /// <summary>What killed a dagger.</summary>
    public enum DaggerDeathCause
    {
        Bullet,
        Rocket,
        RocketExplosion,
        ShieldWall,
        Laser,
        Railgun,
        Flame
    }

    /// <summary>One dagger death, raised by <see cref="Dagger.Killed"/>.</summary>
    public readonly struct DaggerKill
    {
        public readonly DaggerDeathCause Cause;
        public readonly Vector2 Position;
        /// <summary>GML current_score += 10 with its "10" float text.</summary>
        public readonly bool AwardsScore;
        /// <summary>GML random_pickup(x, y) (UH-T23 spawns the carrot or coin).</summary>
        public readonly bool DropsPickup;

        public DaggerKill(DaggerDeathCause cause, Vector2 position, bool awardsScore, bool dropsPickup)
        {
            Cause = cause;
            Position = position;
            AwardsScore = awardsScore;
            DropsPickup = dropsPickup;
        }
    }

    /// <summary>
    /// Ports obj_dagger (gmlHARE, a par_enemy; it has no ai_* script, its flight lives in its own
    /// events). It hunts nothing: it spawns at rest facing a random whole degree with a random
    /// heading (direction) and cycles three modes. Rotate turns image_angle 0.5 degrees a step
    /// toward the heading (check_rotate_clockwise) until it is within 2 degrees (a_near_b), then
    /// it launches (ef_ring) into Fly for irandom_range(120, 240) steps (Alarm 1), gaining 1 px/step
    /// every 8 steps (Alarm 0) up to 8 and puffing smoke every 20 steps (Alarm 2); Stop then sheds
    /// 1 px/step every 8 steps and, once still, picks a new random heading and rotates again. Its
    /// blade never turns while flying, so it flies along the heading it lined up with. Once it is
    /// more than its sprite width (96 px) outside the room it reverses (direction -= 180, and the
    /// blade snaps to it). spr_dagger or spr_dagger2 is picked at random.
    ///
    /// It dies to any bullet (and takes the bullet with it), any rocket, any rocket explosion, the
    /// shield wall, the laser, the railgun and flame. Its own collision events (bullet, rocket,
    /// rocket explosion, shield wall) give 10 points and roll random_pickup; the laser, railgun and
    /// flame destroy it from their side, so those kills give neither. Touching the ship hurts it
    /// (obj_player's par_enemy collision), and overlapping another par_enemy pushes it 2 px a step
    /// straight away from that enemy.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class Dagger : MonoBehaviour, IEnemyTarget, IProjectileTarget, IShieldable
    {
        /// <summary>GML rate: Alarm 0 changes speed by 1 px/step this often.</summary>
        public const float RateSteps = 8f;
        public const float MaxSpeedPxPerStep = 8f;
        public const float TurnRateDegreesPerStep = 0.5f;
        /// <summary>a_near_b(image_angle, direction, 2).</summary>
        public const float AlignToleranceDegrees = 2f;
        /// <summary>alarm[1] = irandom_range(120, 240): how long it flies before braking.</summary>
        public const int MinFlySteps = 120;
        public const int MaxFlySteps = 240;
        /// <summary>Alarm 2: a smoke puff this often while flying.</summary>
        public const float SmokeEverySteps = 20f;
        /// <summary>size = sprite_get_width(spr_dagger).</summary>
        public const float SizePx = 96f;
        /// <summary>Collision par_enemy: x/y += lengthdir(2, away).</summary>
        /// <summary>The size of the ef_smokeup and ef_firework its bullet and rocket kills create.</summary>
        public const int DeathEffectSize = DeathEffects.Small;
        public const float SeparationPxPerStep = 2f;
        public const int Points = 10;
        /// <summary>spr_dagger's bounding box (18..94 x 26..69 of a 96x96 sprite centred at 48,48).</summary>
        public static readonly Vector2 HitboxSizePx = new Vector2(76f, 43f);
        public static readonly Vector2 HitboxOffsetPx = new Vector2(8f, 0f);

        const float Off = -1f;

        Rect room;
        Rigidbody2D body;
        float speedAlarm;
        float stopAlarm = Off;
        float smokeAlarm = Off;
        bool dead;

        public DaggerMode Mode { get; private set; }
        /// <summary>GML direction, degrees: the heading it flies along.</summary>
        public float Direction { get; private set; }
        /// <summary>GML image_angle, degrees: where the blade points.</summary>
        public float ImageAngle { get; private set; }
        /// <summary>GML speed, px/step.</summary>
        public float SpeedPxPerStep { get; private set; }
        /// <summary>1 = spr_dagger, 2 = spr_dagger2.</summary>
        public int Variant { get; private set; }
        public Rect Room => room;
        public bool IsDead => dead;
        /// <summary>Steps left in Fly before Alarm 1 switches to Stop, or negative when unset.</summary>
        public float FlyStepsLeft => stopAlarm;

        /// <summary>Lined up and launched (GML effect_create_above(ef_ring)).</summary>
        public event Action<Dagger> Launched;
        /// <summary>Alarm 2 while flying: effect_create_below(ef_smokeup) just behind it.</summary>
        public event Action<Dagger, Vector2> SmokePuff;
        /// <summary>Raised once, just before the dagger is removed.</summary>
        public event Action<Dagger, DaggerKill> Killed;

        /// <summary>
        /// GML Create at <paramref name="position"/>. <paramref name="direction"/> and
        /// <paramref name="imageAngle"/> override the random irandom_range(0, 359) values (tests pin
        /// them); <paramref name="room"/> defaults to <see cref="Gml.Room"/>.
        /// </summary>
        public void Init(Vector2 position, Rect? room = null, float? direction = null, float? imageAngle = null)
        {
            this.room = room ?? Gml.Room;
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            var hitbox = GetComponent<BoxCollider2D>();
            hitbox.isTrigger = true;
            hitbox.size = new Vector2(Gml.Px(HitboxSizePx.x), Gml.Px(HitboxSizePx.y));
            hitbox.offset = new Vector2(Gml.Px(HitboxOffsetPx.x), Gml.Px(HitboxOffsetPx.y));
            if (GetComponent<PlayerDamageSource>() == null) gameObject.AddComponent<PlayerDamageSource>();

            transform.position = new Vector3(position.x, position.y, transform.position.z);
            SpeedPxPerStep = 0f;
            Direction = Mathf.Repeat(direction ?? UnityEngine.Random.Range(0, 360), 360f);
            SetImageAngle(imageAngle ?? UnityEngine.Random.Range(0, 360));
            Mode = DaggerMode.Rotate;
            speedAlarm = RateSteps;
            stopAlarm = Off;
            smokeAlarm = Off;
            Variant = UnityEngine.Random.Range(1, 3);
            dead = false;
        }

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>One GML step (alarms, Step, then built-in motion), scaled to <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            if (dead) return;
            float steps = dt * Gml.StepsPerSecond;
            RunAlarms(steps);
            StepEvent(steps);

            Vector2 next = (Vector2)transform.position + Gml.Dir(Direction) * Gml.Px(SpeedPxPerStep * steps);
            if (Application.isPlaying && body != null) body.MovePosition(next);
            else transform.position = new Vector3(next.x, next.y, transform.position.z);
        }

        // Alarms fire on whole steps; the slack absorbs float drift.
        static bool Due(float alarm) => alarm <= 1e-3f;

        void RunAlarms(float steps)
        {
            // Alarm 0: accelerate while flying, brake while stopping; always re-armed.
            speedAlarm -= steps;
            while (Due(speedAlarm))
            {
                if (Mode == DaggerMode.Fly) { if (SpeedPxPerStep < MaxSpeedPxPerStep) SpeedPxPerStep += 1f; }
                else if (Mode == DaggerMode.Stop) { if (SpeedPxPerStep > 0f) SpeedPxPerStep -= 1f; }
                speedAlarm += RateSteps;
            }

            // Alarm 1: stop flying.
            if (stopAlarm >= 0f)
            {
                stopAlarm -= steps;
                if (Due(stopAlarm))
                {
                    stopAlarm = Off;
                    Mode = DaggerMode.Stop;
                }
            }

            // Alarm 2: smoke trail while flying, re-armed every 20 steps until Stop clears it.
            if (smokeAlarm >= 0f)
            {
                smokeAlarm -= steps;
                while (Due(smokeAlarm))
                {
                    if (Mode == DaggerMode.Fly)
                        SmokePuff?.Invoke(this, (Vector2)transform.position - Gml.Dir(Direction) * Gml.Px(SpeedPxPerStep));
                    smokeAlarm += SmokeEverySteps;
                }
            }
        }

        void StepEvent(float steps)
        {
            float size = Gml.Px(SizePx);
            Vector2 p = transform.position;
            if (p.x < room.xMin - size || p.x > room.xMax + size || p.y < room.yMin - size || p.y > room.yMax + size)
            {
                Direction = Mathf.Repeat(Direction - 180f, 360f);
                SetImageAngle(Direction);
            }

            switch (Mode)
            {
                case DaggerMode.Rotate:
                    if (!ANearB(ImageAngle, Direction, AlignToleranceDegrees))
                    {
                        float turn = TurnRateDegreesPerStep * steps;
                        SetImageAngle(CheckRotateClockwise(ImageAngle, Direction) ? ImageAngle + turn : ImageAngle - turn);
                    }
                    else
                    {
                        Mode = DaggerMode.Fly;
                        Launched?.Invoke(this);
                        stopAlarm = UnityEngine.Random.Range(MinFlySteps, MaxFlySteps + 1);
                        smokeAlarm = SmokeEverySteps;
                    }
                    break;

                case DaggerMode.Stop:
                    if (SpeedPxPerStep <= 0f)
                    {
                        SpeedPxPerStep = 0f;
                        Direction = UnityEngine.Random.Range(0, 360);
                        Mode = DaggerMode.Rotate;
                        smokeAlarm = Off; // alarm[2] = 0 never fires
                    }
                    break;
            }
        }

        void SetImageAngle(float degrees)
        {
            ImageAngle = Mathf.Repeat(degrees, 360f);
            transform.rotation = Quaternion.Euler(0f, 0f, ImageAngle);
        }

        /// <summary>GML a_near_b: plain numeric closeness, no wrap-around.</summary>
        public static bool ANearB(float a, float b, float range) => a <= b + range && a >= b - range;

        /// <summary>
        /// GML check_rotate_clockwise(target, angle), called as (image_angle, direction): true when
        /// image_angle should grow to reach direction the short way round. (GML angles grow
        /// counter-clockwise on screen; the name is the original's.)
        /// </summary>
        public static bool CheckRotateClockwise(float target, float angle)
        {
            float turn;
            if (target > angle) turn = target > angle + 180f ? -(360f - target + angle) : target - angle;
            else turn = angle > target + 180f ? 360f - angle + target : target - angle;
            return turn < 0f;
        }

        // ---- Collisions ----

        /// <summary>Laser, railgun and flame destroy it outright; a rocket or its explosion runs the dagger's own event.</summary>
        public void OnWeaponHit(in WeaponHit hit)
        {
            if (dead) return;
            switch (hit.Kind)
            {
                case WeaponHitKind.Rocket:
                    DeathEffects.Burst(transform.position, DeathEffectSize);
                    Die(DaggerDeathCause.Rocket, true, true);
                    break;
                case WeaponHitKind.RocketExplosion: Die(DaggerDeathCause.RocketExplosion, true, true); break;
                case WeaponHitKind.Laser: Die(DaggerDeathCause.Laser, false, false); break;
                case WeaponHitKind.Railgun: Die(DaggerDeathCause.Railgun, false, false); break;
                default: Die(DaggerDeathCause.Flame, false, false); break;
            }
        }

        /// <summary>
        /// GML collision with obj_bullet: both go; points only for the ship's own rounds. Enemy
        /// rounds (other.owner == ID) pass through.
        /// </summary>
        public bool OnProjectileHit(Bullet bullet)
        {
            if (dead || (bullet != null && bullet.FromEnemy)) return false;
            bool fromPlayer = bullet != null && bullet.Owner != null && bullet.Owner.GetComponentInParent<PlayerShipController>() != null;
            TinyExplosion.Spawn(bullet != null ? (Vector2)bullet.transform.position : (Vector2)transform.position);
            DeathEffects.Burst(transform.position, DeathEffectSize);
            Die(DaggerDeathCause.Bullet, fromPlayer, true);
            return true;
        }

        /// <summary>GML collision with obj_shieldwall: dies with a tiny explosion.</summary>
        public void OnShieldHit(ShieldWall wall, Vector2 normal)
        {
            if (dead) return;
            TinyExplosion.Spawn(transform.position);
            Die(DaggerDeathCause.ShieldWall, true, true);
        }

        void OnTriggerStay2D(Collider2D other)
        {
            if (dead || other == null) return;
            var enemy = other.GetComponentInParent<IEnemyTarget>() as Component;
            if (enemy == null || enemy.gameObject == gameObject) return;
            PushAwayFrom(enemy.transform.position, Time.fixedDeltaTime * Gml.StepsPerSecond);
        }

        /// <summary>GML collision with par_enemy: x/y += lengthdir(2, point_direction(other, self)).</summary>
        public void PushAwayFrom(Vector2 other, float steps = 1f)
        {
            Vector2 away = (Vector2)transform.position - other;
            // point_direction of a zero vector is 0 degrees.
            Vector2 dir = away.sqrMagnitude > Mathf.Epsilon ? away.normalized : Vector2.right;
            Vector2 next = (Vector2)transform.position + dir * Gml.Px(SeparationPxPerStep * steps);
            transform.position = new Vector3(next.x, next.y, transform.position.z);
        }

        void Die(DaggerDeathCause cause, bool awardsScore, bool dropsPickup)
        {
            if (dead) return;
            dead = true;
            Killed?.Invoke(this, new DaggerKill(cause, transform.position, awardsScore, dropsPickup));
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }
    }
}
