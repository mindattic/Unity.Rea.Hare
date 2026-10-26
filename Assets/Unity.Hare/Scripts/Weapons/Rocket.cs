using UnityEngine;
using Unity.Hare.Player;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// Ports obj_rocket (gmlHARE). Launched at rest; after 50 steps it ignites at 1 px/step and
    /// gains 1 px/step every 4 steps up to 6. On its first step it locks onto the nearest
    /// par_enemy and, whenever that target is within 100 px, turns toward it at 3 degrees/step. It
    /// explodes (obj_rocket_explosion) on any <see cref="IWeaponTarget"/> unless the target is an
    /// <see cref="IRocketTarget"/> that lets it through or bounces it, is shot down by a
    /// <see cref="Bullet"/>, bounces off a shield wall
    /// along the wall's normal (once per wall, GML last_impact), and can hit its own ship only once
    /// it is older than room_speed (obj_player's obj_rocket collision checks other.age).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class Rocket : MonoBehaviour, IShieldable, IProjectileTarget
    {
        public const float IgniteDelaySteps = 50f;
        public const float AccelerateEverySteps = 4f;
        public const float MaxSpeedPxPerStep = 6f;
        public const float TurnRateDegreesPerStep = 3f;
        public const float LockOnRangePx = 100f;
        /// <summary>GML: other.age > room_speed before a rocket can hurt the ship.</summary>
        public const float SelfHitAgeSteps = 120f;
        /// <summary>
        /// Stands in for obj_rocket's room check (past buffer_zone outside the room) only when no play room is running;
        /// in one, the room's rule decides (U.HARE-T21).
        /// </summary>
        public const float MaxRangeUnits = 40f;

        Rigidbody2D body;
        Vector2 launchPosition;
        float accelerateTimer;
        bool lockAttempted;
        IEnemyTarget target;
        Object lastImpact;
        bool exploded;

        /// <summary>Age in seconds (GML age, in steps / 120).</summary>
        public float Age { get; private set; }
        /// <summary>Degrees, GML convention.</summary>
        public float Direction { get; private set; }
        public float SpeedPxPerStep { get; private set; }
        public bool IsActive { get; private set; }
        public IEnemyTarget Target => target;
        public Vector2 Position => transform.position;

        public void Init(Vector2 position, float directionDegrees)
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            var hitbox = GetComponent<CircleCollider2D>();
            hitbox.isTrigger = true;
            hitbox.radius = Gml.Px(6f);

            launchPosition = position;
            transform.position = position;
            SetDirection(directionDegrees);
            Age = 0f;
            SpeedPxPerStep = 0f;
            IsActive = false;
            accelerateTimer = 0f;
        }

        void SetDirection(float degrees)
        {
            Direction = Mathf.Repeat(degrees, 360f);
            transform.rotation = Quaternion.Euler(0f, 0f, Direction);
        }

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>One GML Step (plus its alarms), scaled to <paramref name="dt"/> seconds.</summary>
        public void Tick(float dt)
        {
            if (exploded) return;
            float steps = dt * Gml.StepsPerSecond;
            Age += dt;

            // Alarm 0: ignite.
            if (!IsActive && Age >= Gml.Steps(IgniteDelaySteps))
            {
                IsActive = true;
                SpeedPxPerStep = 1f;
                accelerateTimer = 0f;
            }
            // Alarm 1: every smoke_rate steps, speed up while moving.
            else if (IsActive)
            {
                accelerateTimer += steps;
                while (accelerateTimer >= AccelerateEverySteps)
                {
                    accelerateTimer -= AccelerateEverySteps;
                    if (SpeedPxPerStep < MaxSpeedPxPerStep) SpeedPxPerStep += 1f;
                }
            }

            Home(steps);

            Vector2 next = (Vector2)transform.position + Gml.Dir(Direction) * Gml.Px(SpeedPxPerStep * steps);
            if (Application.isPlaying && body != null) body.MovePosition(next);
            else transform.position = next;

            // GML: (x + buffer_zone < 0) || (x - buffer_zone > room_width) || ... instance_destroy().
            var outOfPlay = Unity.Hare.Arena.ArenaRoom.OutOfPlay(next);
            if (outOfPlay.HasValue ? outOfPlay.Value : (next - launchPosition).sqrMagnitude > MaxRangeUnits * MaxRangeUnits) Remove();
        }

        void Home(float steps)
        {
            if (!lockAttempted)
            {
                lockAttempted = true;
                target = FindNearestEnemy(transform.position);
            }
            if (target == null || !(target is Component c) || c == null) return;

            Vector2 to = (Vector2)c.transform.position - (Vector2)transform.position;
            if (to.magnitude >= Gml.Px(LockOnRangePx)) return;
            float wanted = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            SetDirection(Mathf.MoveTowardsAngle(Direction, wanted, TurnRateDegreesPerStep * steps));
        }

        /// <summary>GML instance_nearest(x, y, par_enemy).</summary>
        public static IEnemyTarget FindNearestEnemy(Vector2 from)
        {
            IEnemyTarget best = null;
            float bestDist = float.PositiveInfinity;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (!(mb is IEnemyTarget enemy)) continue;
                float d = ((Vector2)mb.transform.position - from).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = enemy; }
            }
            return best;
        }

        void OnTriggerEnter2D(Collider2D other) => Touch(other);
        void OnTriggerStay2D(Collider2D other) => Touch(other);

        /// <summary>Contact resolution, public so EditMode tests can drive it without physics callbacks.</summary>
        public void Touch(Collider2D other)
        {
            if (exploded || other == null) return;
            if (other.GetComponentInParent<ShieldWall>() != null) return; // ShieldWall calls OnShieldHit

            var health = other.GetComponentInParent<PlayerHealth>();
            if (health != null)
            {
                if (Age <= Gml.Steps(SelfHitAgeSteps)) return; // still clearing its own launcher
                health.TryTakeHit(reaction: HitReaction.SpinOrFling); // obj_player's obj_rocket event: spin, or fling off a rock
                Explode();
                return;
            }

            var rocketTarget = other.GetComponentInParent<IRocketTarget>();
            if (rocketTarget != null)
            {
                if (rocketTarget.OnRocketHit(this)) Explode();
                return;
            }

            var weaponTarget = other.GetComponentInParent<IWeaponTarget>();
            if (weaponTarget == null) return;
            weaponTarget.OnWeaponHit(new WeaponHit(WeaponHitKind.Rocket, transform.position, Gml.Dir(Direction)));
            Explode();
        }

        /// <summary>GML obj_rocket collision with obj_shieldwall: direction = normal_detect(...), once per wall.</summary>
        public void OnShieldHit(ShieldWall wall, Vector2 normal) => Deflect(wall, normal);

        /// <summary>
        /// GML direction = image_angle = normal_detect(...) off <paramref name="surface"/> (a shield
        /// wall or obj_phalanx_shield), once per surface (last_impact).
        /// </summary>
        public void Deflect(Object surface, Vector2 normal)
        {
            if (exploded || surface == lastImpact) return;
            lastImpact = surface;
            SetDirection(Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg);
        }

        /// <summary>GML obj_rocket collision with obj_bullet: the bullet and the rocket both go (no big explosion).</summary>
        public bool OnProjectileHit(Bullet bullet)
        {
            if (exploded) return false;
            Remove();
            return true;
        }

        /// <summary>Spawns obj_rocket_explosion here and removes the rocket.</summary>
        public RocketExplosion Explode()
        {
            if (exploded) return null;
            exploded = true;
            var go = new GameObject("RocketExplosion");
            var explosion = go.AddComponent<RocketExplosion>();
            explosion.Init(transform.position);
            Remove();
            return explosion;
        }

        void Remove()
        {
            exploded = true;
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }
    }
}
