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
    /// its direction, dies once it is out of play (GML: outside the room plus buffer_zone, the
    /// running play room's; a lifetime only with no room running), is destroyed by whatever it hits, and bounces off a
    /// shield wall along the wall's normal (GML: direction = normal_detect(...)).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class Bullet : MonoBehaviour, IShieldable
    {
        /// <summary>obj_bullet's sprite_index and depth.</summary>
        public const string DefaultSpriteName = "spr_bullet";
        public const int DefaultDepth = -3;

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

        /// <summary>
        /// GML owner = ID: fired by an enemy. Each enemy's ID is its own id, so an enemy's "other.owner != ID" check
        /// skips only the rounds it fired (<see cref="ShooterId"/>); another enemy's round hits it (U.HARE-T91).
        /// </summary>
        public bool FromEnemy { get; private set; }

        /// <summary>The firing enemy's Core id, or -1 when unknown.</summary>
        public int ShooterId { get; private set; } = -1;

        /// <summary>Flags this round as an enemy's (see <see cref="FromEnemy"/>), fired by the enemy with this id.</summary>
        public void MarkFromEnemy(int shooterId = -1)
        {
            FromEnemy = true;
            ShooterId = shooterId;
        }

        /// <summary>Called right after AddComponent (Awake does not run in EditMode).</summary>
        public void Init(GameObject owner, float direction, float speed, int damage, float lifetime, float radius)
        {
            // obj_bullet's own sprite_index is spr_bullet at depth -3; a shooter that drew its round first (the vulcan's
            // bullet_asset, the shotgun's spr_pellet) keeps its sprite (U.HARE-T95).
            var look = GetComponent<SpriteRenderer>();
            if (look == null || look.sprite == null) Effects.HareArt.Show(gameObject, DefaultSpriteName, DefaultDepth);

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

        /// <summary>
        /// One physics step; public so EditMode tests can drive it. While <see cref="Gml.Paused"/> the bullet holds
        /// still and does not age (obj_bullet's Step); after, it flies on at its speed. In a play room the room's rule decides (obj_bullet's
        /// Step: gone once more than buffer_zone outside room_width x room_height, U.HARE-T21); with no room running,
        /// <see cref="Lifetime"/> stands in for it.
        /// </summary>
        public void Step(float dt)
        {
            // obj_bullet's Step: if(!paused) { speed = Speed; ... age += 1; } else speed = 0 (U.HARE-T60).
            if (Gml.Paused)
            {
                if (body != null) body.linearVelocity = Vector2.zero;
                return;
            }
            if (body != null) body.linearVelocity = Gml.Dir(Direction) * Speed;
            Age += dt;
            var outOfPlay = Unity.Hare.Arena.ArenaRoom.OutOfPlay(transform.position);
            if (outOfPlay.HasValue ? outOfPlay.Value : Age >= Lifetime) Despawn();
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
