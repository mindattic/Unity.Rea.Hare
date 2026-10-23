using UnityEngine;
using Unity.Hare.Player;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// Anything a projectile can hit: enemies (par_enemy), rocks (par_rock) and structures
    /// (par_structure). The enemy and rock tickets implement this on their own components.
    /// </summary>
    public interface IProjectileTarget
    {
        /// <returns>True when the hit consumes the projectile (GML: the bullet's instance_destroy).</returns>
        bool OnProjectileHit(Bullet bullet);
    }

    /// <summary>
    /// Ports obj_bullet (gmlHARE): a straight-line projectile that flies at a constant speed along
    /// its direction, dies once it is out of play (GML: outside the room plus buffer_zone; here a
    /// lifetime, until rooms have bounds), is destroyed by whatever it hits, and bounces off a
    /// shield wall along the wall's normal (GML: direction = normal_detect(...)).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class Bullet : MonoBehaviour, IShieldable
    {
        Rigidbody2D body;

        /// <summary>GML: owner. The ship that fired this; its own colliders are never hit.</summary>
        public GameObject Owner { get; private set; }

        /// <summary>Degrees, GML's math convention (0 = +x, CCW positive).</summary>
        public float Direction { get; private set; }
        public float Speed { get; private set; }
        public int Damage { get; private set; }
        public float Lifetime { get; private set; }

        /// <summary>GML: age, here in seconds.</summary>
        public float Age { get; private set; }

        public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;

        /// <summary>Called right after AddComponent (Awake does not run in EditMode).</summary>
        public void Init(GameObject owner, float direction, float speed, int damage, float lifetime, float radius)
        {
            Owner = owner;
            Speed = speed;
            Damage = damage;
            Lifetime = lifetime;
            Age = 0f;

            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;

            var hitbox = GetComponent<CircleCollider2D>();
            hitbox.isTrigger = true;
            hitbox.radius = radius;

            SetDirection(direction);
        }

        /// <summary>GML: direction = d; image_angle = d. Speed is kept.</summary>
        public void SetDirection(float degrees)
        {
            Direction = Mathf.Repeat(degrees, 360f);
            transform.rotation = Quaternion.Euler(0f, 0f, Direction);
            if (body != null)
                body.linearVelocity = new Vector2(Mathf.Cos(Direction * Mathf.Deg2Rad), Mathf.Sin(Direction * Mathf.Deg2Rad)) * Speed;
        }

        void FixedUpdate() => Step(Time.fixedDeltaTime);

        /// <summary>One physics step; public so EditMode tests can drive it.</summary>
        public void Step(float dt)
        {
            Age += dt;
            if (Age >= Lifetime) Despawn();
        }

        void OnTriggerEnter2D(Collider2D other) => TryHit(other);

        /// <summary>Hits <paramref name="other"/> if it is a projectile target that is not the owner.</summary>
        public bool TryHit(Collider2D other)
        {
            if (Owner != null && other.transform.IsChildOf(Owner.transform)) return false;
            var target = other.GetComponentInParent<IProjectileTarget>();
            if (target == null) return false;
            if (target.OnProjectileHit(this)) Despawn();
            return true;
        }

        /// <summary>GML obj_bullet / Collision obj_shieldwall: direction = image_angle = normal.</summary>
        public void OnShieldHit(ShieldWall wall, Vector2 normal)
        {
            SetDirection(Mathf.Atan2(normal.y, normal.x) * Mathf.Rad2Deg);
        }

        void Despawn()
        {
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }
    }
}
