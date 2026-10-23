using UnityEngine;

namespace Unity.Hare.Player
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
            if (health.TryTakeHit(ignoresSpawnProtection) && destroyOnHit)
                Destroy(gameObject);
        }
    }
}
