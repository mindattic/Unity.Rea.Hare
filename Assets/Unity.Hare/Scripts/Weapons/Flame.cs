using UnityEngine;
using Unity.Hare.Player;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// One burst of flame (GML: obj_flame, sprite spr_powder_fire). Drifts at 4 px/step along the
    /// aim at alpha 0.75, starts at half size and grows 0.01 scale per step up to full size, so the
    /// stream widens into a cone; it lives for one play of its 42-frame animation at image_speed
    /// 0.3 (140 steps) and burns through enemies without being spent: each <see cref="IEnemyTarget"/>
    /// it touches gets a <see cref="WeaponHitKind.Flame"/> hit, which kills (obj_flame, Collision
    /// par_enemy: with(other) instance_destroy()). It never touches the ship that fired it, and
    /// passes through rocks and structures, which the original gave it no collision with.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public class Flame : MonoBehaviour
    {
        /// <summary>obj_flame's sprite_index (41 frames at image_speed 0.3, played once) and depth.</summary>
        public const string SpriteName = "spr_powder_fire";
        public const int Depth = -4;
        const int SpriteFrames = 41;
        const float SpriteSpeed = 0.3f;
        public const float StartScale = 0.5f;                                  // image_xscale = 0.5
        public const float MaxScale = 1f;                                      // grows while < 1
        public const float GrowPerSecond = 0.01f * Gml.StepsPerSecond;  // grow_rate per step
        public const float StartAlpha = 0.75f;                                 // image_alpha

        Effects.SpriteAnimation anim;

        public PlayerShipController Owner { get; private set; }
        public float Direction { get; private set; }
        public float Speed { get; private set; }
        public float Lifetime { get; private set; }
        public float Age { get; private set; }
        public float Scale { get; private set; }

        public Vector2 Velocity => Gml.Dir(Direction) * Speed;

        /// <summary>Called right after AddComponent (Awake does not run in EditMode).</summary>
        public void Init(PlayerShipController owner, float direction, float speed, float lifetime, float radius)
        {
            Owner = owner;
            Direction = direction;
            Speed = speed;
            Lifetime = lifetime;
            Age = 0f;
            transform.rotation = Quaternion.Euler(0f, 0f, direction); // image_angle = direction

            var body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            var hitbox = GetComponent<CircleCollider2D>();
            hitbox.isTrigger = true;
            hitbox.radius = radius; // scales with the transform as the flame grows

            var spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                var c = spriteRenderer.color;
                c.a = StartAlpha;
                spriteRenderer.color = c;
            }
            anim = new Effects.SpriteAnimation(SpriteName, SpriteFrames, SpriteSpeed, spriteRenderer); // image_speed 0.3, played once
            SetScale(StartScale);
        }

        void FixedUpdate() => Step(Time.fixedDeltaTime);

        /// <summary>One movement step; public so EditMode tests can drive it.</summary>
        public void Step(float dt)
        {
            transform.position += (Vector3)(Velocity * dt);
            if (Scale < MaxScale) SetScale(Mathf.Min(MaxScale, Scale + GrowPerSecond * dt));
            Age += dt;
            anim?.Advance(dt * Gml.StepsPerSecond);
            // GML Animation End: instance_destroy(). The tolerance keeps float dust from adding a step.
            if (Age >= Lifetime - 1e-4f)
            {
                if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            }
        }

        void SetScale(float scale)
        {
            Scale = scale;
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        void OnTriggerEnter2D(Collider2D other) => TryBurn(other);

        /// <summary>Burns <paramref name="other"/> if it is an enemy other than the owner.</summary>
        public bool TryBurn(Collider2D other)
        {
            if (other == null) return false;
            if (Owner != null && other.transform.IsChildOf(Owner.transform)) return false;
            var target = other.GetComponentInParent<IEnemyTarget>();
            if (target == null) return false;
            target.OnWeaponHit(new WeaponHit(WeaponHitKind.Flame, other.transform.position, Gml.Dir(Direction)));
            return true;
        }
    }
}
