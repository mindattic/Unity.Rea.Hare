using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Hare.Effects;
using Unity.Hare.Player;
using Object = UnityEngine.Object;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// Ports obj_rocket_explosion (gmlHARE, depth -4): spr_bigexplosion (96 px, origin at its centre, 20 frames
    /// at image_speed 0.5, so 40 steps), playing sfx_explode when in_view(x, y) and destroying itself when its
    /// animation ends (U.HARE-T31 draws it and plays the sound). Everything it overlaps is hit
    /// once: the ship takes a hit even through spawn protection (obj_player's
    /// obj_rocket_explosion collision skips the justspawned check), and every
    /// <see cref="IWeaponTarget"/> gets a <see cref="WeaponHitKind.RocketExplosion"/> hit.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class RocketExplosion : MonoBehaviour
    {
        public const string SpriteName = "spr_bigexplosion";
        public const string Sound = "sfx_explode";
        public const int Frames = 20;
        public const float ImageSpeed = 0.5f;
        public const int Depth = -4;
        public const float LifetimeSteps = Frames / ImageSpeed;
        public const float RadiusPx = 48f;

        readonly HashSet<Object> alreadyHit = new HashSet<Object>();
        SpriteAnimation anim;
        float remaining;

        /// <summary>Every rocket explosion as it is created (GML Create).</summary>
        public static event Action<RocketExplosion> Exploded;

        public float Remaining => remaining;
        public float ImageIndex => anim != null ? anim.ImageIndex : 0f;

        /// <summary>
        /// GML Create at <paramref name="position"/>. <paramref name="scale"/> grows the explosion (sprite and hit circle
        /// together) for a ship's rocket under the garage's "Power" pips (U.HARE-T24, BIBLE V49); 1 everywhere else.
        /// </summary>
        public void Init(Vector2 position, float scale = 1f)
        {
            transform.position = position;
            if (scale > 0f && scale != 1f) transform.localScale = new Vector3(scale, scale, 1f);
            anim = new SpriteAnimation(SpriteName, Frames, ImageSpeed, HareArt.Show(gameObject, SpriteName, Depth));
            var hitbox = GetComponent<CircleCollider2D>();
            hitbox.isTrigger = true;
            hitbox.radius = Gml.Px(RadiusPx);
            // A trigger needs a rigidbody on one side; the explosion brings its own so it also hits static rocks.
            var body = gameObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            remaining = Gml.Steps(LifetimeSteps);
            if (HareArt.InView(position)) HareArt.Play(Sound);
            Exploded?.Invoke(this);
        }

        void Update() => Tick(Time.deltaTime);

        /// <summary>Runs the animation down; returns false once the explosion is gone.</summary>
        public bool Tick(float dt)
        {
            remaining -= dt;
            bool ended = anim != null ? anim.Advance(SpriteAnimation.StepsIn(dt)) : remaining <= 0f;
            if (!ended) return true;
            remaining = 0f;
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
