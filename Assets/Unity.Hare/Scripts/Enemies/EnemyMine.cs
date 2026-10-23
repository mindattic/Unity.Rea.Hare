using System;
using UnityEngine;
using Unity.Hare.Player;
using Unity.Hare.Weapons;
using Object = UnityEngine.Object;

namespace Unity.Hare.Enemies
{
    /// <summary>
    /// Ports obj_enemy_mine (gmlHARE, par_projectile, laid by <see cref="Trilobyte"/>): it sits still
    /// for 3 seconds, then arms (ef_ring, sfx_beep). Armed, it turns 3 degrees a step toward the
    /// ship and creeps at 1 px/step while the ship is within 200 px, and blows up as an
    /// obj_rocket_explosion within 24 px. Its fuse (20-30 whole seconds) detonates it anyway; it
    /// pings (ef_ring) every 5 seconds.
    ///
    /// Not a par_enemy: beams, flame and rocket explosions ignore it and the ship can touch it
    /// harmlessly, but any bullet (whoever fired it) or rocket sets it off where the shot struck,
    /// for 1 point when it was the ship's.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
    public class EnemyMine : MonoBehaviour, IProjectileTarget, IRocketTarget
    {
        public const float ArmSteps = 3 * 120f;
        public const int MinFuseSeconds = 20;
        public const int MaxFuseSeconds = 30;
        public const float PingEverySteps = 5 * 120f;
        public const float ChaseRangePx = 200f;
        public const float ChaseSpeedPxPerStep = 1f;
        public const float TurnDegreesPerStep = 3f;
        public const float DetonateRangePx = 24f;
        public const int Points = 1;
        /// <summary>spr_mine's bbox (18x11).</summary>
        public static readonly Vector2 SizePx = new Vector2(18f, 11f);

        const float Off = -1f;

        Rigidbody2D body;
        PlayerShipController player;
        float armAlarm;
        float fuseAlarm;
        float pingAlarm;
        bool gone;

        public bool IsActive { get; private set; }
        public bool IsGone => gone;
        public float ImageAngle { get; private set; }
        public float Direction { get; private set; }
        public float SpeedPxPerStep { get; private set; }
        /// <summary>GML owner: who laid it.</summary>
        public Object Owner { get; private set; }
        public float FuseStepsLeft => fuseAlarm;
        public PlayerShipController Player { get => player; set => player = value; }

        public event Action<EnemyMine> Armed;
        public event Action<EnemyMine> Pinged;
        /// <summary>Raised once as it goes, with how (and whether it scored).</summary>
        public event Action<EnemyMine, EnemyKill> Detonated;

        /// <param name="imageAngle">The layer's heading (the trilobyte sets mine.image_angle = direction); random when null.</param>
        public void Init(Vector2 position, Object owner = null, PlayerShipController player = null, float? imageAngle = null, int? fuseSeconds = null)
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            var box = GetComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(Gml.Px(SizePx.x), Gml.Px(SizePx.y));
            transform.position = new Vector3(position.x, position.y, transform.position.z);

            Owner = owner;
            this.player = player != null ? player : Object.FindAnyObjectByType<PlayerShipController>();
            SetImageAngle(imageAngle ?? UnityEngine.Random.Range(0, 360));
            Direction = 0f;
            SpeedPxPerStep = 0f;
            IsActive = false;
            gone = false;
            armAlarm = ArmSteps;
            fuseAlarm = 120f * (fuseSeconds ?? UnityEngine.Random.Range(MinFuseSeconds, MaxFuseSeconds + 1));
            pingAlarm = PingEverySteps;
        }

        void SetImageAngle(float degrees)
        {
            ImageAngle = Mathf.Repeat(degrees, 360f);
            transform.rotation = Quaternion.Euler(0f, 0f, ImageAngle);
        }

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        static bool Ring(ref float alarm, float steps)
        {
            if (alarm < 0f) return false;
            alarm -= steps;
            if (alarm > 1e-3f) return false;
            alarm = Off;
            return true;
        }

        public void Tick(float dt)
        {
            if (gone) return;
            if (player == null) player = Object.FindAnyObjectByType<PlayerShipController>();
            float steps = dt * Gml.StepsPerSecond;

            if (Ring(ref pingAlarm, steps))
            {
                Pinged?.Invoke(this);
                pingAlarm = PingEverySteps;
            }
            if (Ring(ref fuseAlarm, steps))
            {
                Detonate(EnemyDeathCause.Fuse, transform.position, false);
                return;
            }
            if (Ring(ref armAlarm, steps))
            {
                IsActive = true;
                Armed?.Invoke(this);
            }

            if (IsActive)
            {
                float distance = player != null
                    ? SetPieceEnemy.BoxDistancePx(transform.position, SizePx / 2f, player.transform.position)
                    : float.PositiveInfinity;
                if (distance < ChaseRangePx)
                {
                    float target = SetPieceEnemy.PointDirection(transform.position, player.transform.position);
                    bool clockwise = Dagger.CheckRotateClockwise(target, ImageAngle);
                    SetImageAngle(ImageAngle + (clockwise ? -TurnDegreesPerStep : TurnDegreesPerStep) * steps);
                    Direction = target;
                    SpeedPxPerStep = ChaseSpeedPxPerStep;
                }
                else SpeedPxPerStep = 0f;

                if (distance < DetonateRangePx)
                {
                    Detonate(EnemyDeathCause.Proximity, transform.position, false);
                    return;
                }
            }

            Vector2 next = (Vector2)transform.position + Gml.Dir(Direction) * Gml.Px(SpeedPxPerStep * steps);
            if (Application.isPlaying && body != null) body.MovePosition(next);
            else transform.position = new Vector3(next.x, next.y, transform.position.z);
        }

        /// <summary>The rocket explodes where it is and takes the mine with it.</summary>
        public bool OnRocketHit(Rocket rocket)
        {
            if (gone) return false;
            Remove(EnemyDeathCause.Rocket, true);
            return true;
        }

        /// <summary>Any bullet: an obj_rocket_explosion where the bullet was, both gone.</summary>
        public bool OnProjectileHit(Bullet bullet)
        {
            if (gone) return false;
            bool fromPlayer = bullet != null && !bullet.FromEnemy && bullet.Owner != null &&
                bullet.Owner.GetComponentInParent<PlayerShipController>() != null;
            Detonate(EnemyDeathCause.Bullet, bullet != null ? (Vector2)bullet.transform.position : (Vector2)transform.position, fromPlayer);
            return true;
        }

        void Detonate(EnemyDeathCause cause, Vector2 at, bool awardsScore)
        {
            new GameObject("RocketExplosion").AddComponent<RocketExplosion>().Init(at);
            Remove(cause, awardsScore);
        }

        void Remove(EnemyDeathCause cause, bool awardsScore)
        {
            if (gone) return;
            gone = true;
            Detonated?.Invoke(this, new EnemyKill(cause, transform.position, Points, awardsScore, false));
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }
    }
}
