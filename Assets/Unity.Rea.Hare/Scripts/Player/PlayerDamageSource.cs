using System;
using UnityEngine;

namespace Unity.Rea.Hare.Player
{
    /// <summary>
    /// Put on anything that hurts the player on contact: enemy bullets, enemies (par_enemy),
    /// rockets, rocket explosions. Mirrors obj_player's collision events, which all end in
    /// player_hurt. Works with either trigger or solid 2D colliders.
    /// </summary>
    public class PlayerDamageSource : MonoBehaviour
    {
        [Tooltip("GML: only obj_rocket_explosion hits a just-respawned ship (it skips the justspawned check).")]
        [SerializeField] bool ignoresSpawnProtection;

        [Tooltip("Destroy this object when its hit lands (bullets). Leave off for enemies and explosions.")]
        [SerializeField] bool destroyOnHit;

        public bool IgnoresSpawnProtection
        {
            get => ignoresSpawnProtection;
            set => ignoresSpawnProtection = value;
        }

        public bool DestroyOnHit
        {
            get => destroyOnHit;
            set => destroyOnHit = value;
        }

        /// <summary>
        /// Routes the hit through the rules that own this source (the enemy world's <c>HurtPlayer</c>) instead of straight to
        /// the ship; returns whether the hit landed. Empty means the source hurts the ship directly.
        /// </summary>
        public Func<PlayerHealth, bool> HitOverride { get; set; }

        void OnTriggerEnter2D(Collider2D other) => TryHit(other);
        void OnTriggerStay2D(Collider2D other) => TryHit(other);
        void OnCollisionEnter2D(Collision2D collision) => TryHit(collision.collider);
        void OnCollisionStay2D(Collision2D collision) => TryHit(collision.collider);

        /// <summary>
        /// GML collision events fire every step while overlapping, so a source still touching the
        /// ship lands again once invulnerability ends; the Stay callbacks give the same behaviour.
        /// </summary>
        void TryHit(Collider2D other)
        {
            var health = other.GetComponentInParent<PlayerHealth>();
            if (health == null) return;
            bool landed;
            if (HitOverride != null)
            {
                // HitOverride routes the hit through HurtPlayer (the enemy world's rules), which does not carry
                // ignoresSpawnProtection; only obj_rocket_explosion needs it, and RocketExplosion.Touch calls
                // TryTakeHit directly instead of going through a PlayerDamageSource's HitOverride.
                Debug.Assert(!ignoresSpawnProtection, $"{name}: ignoresSpawnProtection is set but HitOverride would silently drop it.");
                landed = HitOverride(health);
            }
            else
            {
                landed = health.TryTakeHit(ignoresSpawnProtection);
            }
            if (landed && destroyOnHit)
                Destroy(gameObject);
        }
    }
}
