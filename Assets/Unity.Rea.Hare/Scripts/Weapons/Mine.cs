using System;
using UnityEngine;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// Ports obj_mine (gmlHARE, a par_projectile). Dropped facing a random whole degree, it drifts
    /// that way at 1 px/step. After room_speed (Alarm 0) it arms: it beeps, and from then on each
    /// step it looks for the nearest par_enemy; within 300 px (distance_to_object, edge to edge) it
    /// turns its sprite 3 degrees/step toward it (check_rotate_clockwise) and closes at 2 px/step
    /// (move_towards_point), otherwise it holds still. It detonates into an obj_rocket_explosion
    /// once an enemy is under 24 px away, or on its own after room_speed * irandom_range(8, 10)
    /// (Alarm 1). Any <see cref="Bullet"/> that touches it destroys both, as obj_mine's obj_bullet
    /// collision does (its owner check is commented out in the source, so the ship's own rounds
    /// count too). spr_mine is 18x11 px, so its edge sits about 9 px from its centre.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class Mine : MonoBehaviour, IProjectileTarget
    {
        public const float DriftSpeedPxPerStep = 1f;
        public const float ChaseSpeedPxPerStep = 2f;
        public const float TurnRateDegreesPerStep = 3f;
        public const float RangePx = 300f;
        public const float DetonateDistancePx = 24f;
        public const float ActivateSteps = 120f;              // room_speed * 1
        public const int MinDetonateSeconds = 8;              // room_speed * irandom_range(8, 10)
        public const int MaxDetonateSeconds = 10;
        /// <summary>GML delay = 30: shoot_mine re-arms the launcher after this many steps.</summary>
        public const float RearmSteps = WeaponTable.DefaultMineIntervalSteps; // the launcher's interval is weapon data
        public const float RadiusPx = 9f;
        /// <summary>obj_mine's sprite_index and depth.</summary>
        public const string SpriteName = "spr_mine";
        public const int Depth = -3;

        Rigidbody2D body;
        bool gone;

        /// <summary>The ship that dropped it (GML mine.owner = player.id).</summary>
        public GameObject Owner { get; private set; }
        /// <summary>Seconds since it was dropped.</summary>
        public float Age => AgeSteps / Gml.StepsPerSecond;
        /// <summary>GML steps since it was dropped; the alarms compare against this.</summary>
        public float AgeSteps { get; private set; }
        /// <summary>GML active: armed and hunting.</summary>
        public bool IsActive { get; private set; }
        /// <summary>GML direction, degrees.</summary>
        public float Direction { get; private set; }
        /// <summary>GML image_angle, degrees: the sprite's facing, turned toward a target 3 degrees a step.</summary>
        public float ImageAngle { get; private set; }
        /// <summary>GML speed, px/step.</summary>
        public float SpeedPxPerStep { get; private set; }
        /// <summary>Seconds after dropping that Alarm 1 detonates it.</summary>
        public float DetonateAfter { get; private set; }
        public IEnemyTarget Target { get; private set; }
        public bool IsGone => gone;

        /// <summary>Alarm 0: armed (GML effect_create_below(ef_ring) and sfx_beep).</summary>
        public event Action<Mine> Activated;

        /// <summary>
        /// Sets the mine up at <paramref name="position"/>. <paramref name="facing"/> overrides the
        /// random irandom_range(0, 359) heading and <paramref name="detonateSeconds"/> the random
        /// 8..10 s fuse (tests pin them; play leaves them null).
        /// </summary>
        public void Init(GameObject owner, Vector2 position, float? facing = null, int? detonateSeconds = null)
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            var hitbox = GetComponent<CircleCollider2D>();
            hitbox.isTrigger = true;
            hitbox.radius = Gml.Px(RadiusPx);

            Owner = owner;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            ImageAngle = facing ?? UnityEngine.Random.Range(0, 360);
            Direction = ImageAngle;
            transform.rotation = Quaternion.Euler(0f, 0f, ImageAngle);
            SpeedPxPerStep = DriftSpeedPxPerStep;
            int seconds = detonateSeconds ?? UnityEngine.Random.Range(MinDetonateSeconds, MaxDetonateSeconds + 1);
            DetonateAfter = seconds;
            AgeSteps = 0f;
            IsActive = false;
            Target = null;
            gone = false;
        }

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>
        /// One GML step (alarms first, then Step), scaled to <paramref name="dt"/> seconds. While <see cref="Gml.Paused"/>
        /// the mine holds still and its clock stops (obj_mine's Step and both alarms are inside <c>if(!paused)</c>), so it
        /// arms and its fuse runs out once play resumes rather than never (docs/BIBLE.md D22).
        /// </summary>
        public void Tick(float dt)
        {
            if (gone || Gml.Paused) return;
            float steps = dt * Gml.StepsPerSecond;
            AgeSteps += steps;

            // Alarms fire on whole steps; half a step of slack absorbs float drift over a long fuse.
            // Alarm 1: the fuse runs out.
            if (AgeSteps >= DetonateAfter * Gml.StepsPerSecond - 0.5f)
            {
                Explode();
                return;
            }

            // Alarm 0: arm.
            if (!IsActive && AgeSteps >= ActivateSteps - 0.5f)
            {
                IsActive = true;
                Activated?.Invoke(this);
            }

            if (IsActive && Hunt(steps)) return;

            Vector2 next = (Vector2)transform.position + Gml.Dir(Direction) * Gml.Px(SpeedPxPerStep * steps);
            if (Application.isPlaying && body != null) body.MovePosition(next);
            else transform.position = new Vector3(next.x, next.y, transform.position.z);
        }

        /// <summary>The armed Step: chase the nearest enemy in range, or hold still. True if it detonated.</summary>
        bool Hunt(float steps)
        {
            Target = Rocket.FindNearestEnemy(transform.position);
            var enemy = Target as Component;
            if (enemy == null)
            {
                Target = null;
                SpeedPxPerStep = 0f;
                return false;
            }

            float distance = DistanceToObject(enemy);
            if (distance <= Gml.Px(RangePx))
            {
                Vector2 to = (Vector2)enemy.transform.position - (Vector2)transform.position;
                float wanted = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
                ImageAngle = Mathf.Repeat(Mathf.MoveTowardsAngle(ImageAngle, wanted, TurnRateDegreesPerStep * steps), 360f);
                transform.rotation = Quaternion.Euler(0f, 0f, ImageAngle);
                Direction = Mathf.Repeat(wanted, 360f);
                SpeedPxPerStep = ChaseSpeedPxPerStep;
            }
            else
            {
                SpeedPxPerStep = 0f;
            }

            if (distance < Gml.Px(DetonateDistancePx))
            {
                Explode();
                return true;
            }
            return false;
        }

        /// <summary>GML distance_to_object: the gap between the mine's edge and the enemy's collider (or centre).</summary>
        float DistanceToObject(Component enemy)
        {
            Vector2 here = transform.position;
            Vector2 nearest = enemy.transform.position;
            var collider = enemy.GetComponent<Collider2D>();
            if (collider != null)
            {
                Physics2D.SyncTransforms();
                nearest = collider.ClosestPoint(here);
            }
            return Mathf.Max(0f, (nearest - here).magnitude - Gml.Px(RadiusPx));
        }

        /// <summary>
        /// GML obj_mine collision with obj_bullet: the bullet and the mine both go (a tiny
        /// explosion, no blast). <see cref="Bullet.TryHit"/> calls this; a bullet never hits its
        /// owner's own colliders, but the mine is not one of them.
        /// </summary>
        public bool OnProjectileHit(Bullet bullet)
        {
            if (gone) return false;
            Remove();
            return true;
        }

        /// <summary>Spawns obj_rocket_explosion here and removes the mine.</summary>
        public RocketExplosion Explode()
        {
            if (gone) return null;
            var go = new GameObject("RocketExplosion");
            var explosion = go.AddComponent<RocketExplosion>();
            explosion.Init(transform.position);
            Remove();
            return explosion;
        }

        void Remove()
        {
            gone = true;
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }
    }
}
