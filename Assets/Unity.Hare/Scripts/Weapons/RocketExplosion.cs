using System.Collections.Generic;
using UnityEngine;
using Unity.Hare.Player;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// Ports obj_rocket_explosion (gmlHARE): spr_bigexplosion (96 px, 20 frames at image_speed 0.5,
    /// so 40 steps) that destroys itself when its animation ends. Everything it overlaps is hit
    /// once: the ship takes a hit even through spawn protection (obj_player's
    /// obj_rocket_explosion collision skips the justspawned check), and every
    /// <see cref="IWeaponTarget"/> gets a <see cref="WeaponHitKind.RocketExplosion"/> hit.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class RocketExplosion : MonoBehaviour
    {
        public const float LifetimeSteps = 40f;
        public const float RadiusPx = 48f;

        readonly HashSet<Object> alreadyHit = new HashSet<Object>();
        float remaining;

        public float Remaining => remaining;

        public void Init(Vector2 position)
        {
            transform.position = position;
            var hitbox = GetComponent<CircleCollider2D>();
            hitbox.isTrigger = true;
            hitbox.radius = Gml.Px(RadiusPx);
            // A trigger needs a rigidbody on one side; the explosion brings its own so it also hits static rocks.
            var body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            remaining = Gml.Steps(LifetimeSteps);
        }

        void Update() => Tick(Time.deltaTime);

        /// <summary>Counts the animation down; returns false once the explosion is gone.</summary>
        public bool Tick(float dt)
        {
            remaining -= dt;
            if (remaining > 0f) return true;
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            return false;
        }

        void OnTriggerEnter2D(Collider2D other) => Touch(other);
        void OnTriggerStay2D(Collider2D other) => Touch(other);

        /// <summary>Hits <paramref name="other"/> once; public so EditMode tests can drive it.</summary>
        public void Touch(Collider2D other)
        {
            if (other == null) return;

            var health = other.GetComponentInParent<PlayerHealth>();
            if (health != null)
            {
                // GML fires the collision every step, so keep trying until a hit lands (invulnerability may block it).
                if (!alreadyHit.Contains(health) && health.TryTakeHit(ignoresSpawnProtection: true))
                    alreadyHit.Add(health);
                return;
            }

            var target = other.GetComponentInParent<IWeaponTarget>();
            if (target == null || !(target is Object key) || !alreadyHit.Add(key)) return;
            Vector2 outward = (Vector2)other.transform.position - (Vector2)transform.position;
            target.OnWeaponHit(new WeaponHit(WeaponHitKind.RocketExplosion, other.transform.position,
                outward.sqrMagnitude > Mathf.Epsilon ? outward.normalized : Vector2.zero));
        }
    }
}
