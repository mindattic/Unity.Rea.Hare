using System;
using UnityEngine;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// Ports obj_grenade_explosion (gmlHARE): spr_grenade_explode (200x182 px, 10 frames at
    /// image_speed 0.2, so 50 steps) drawn at image_alpha 0.7, playing sfx_explode, then destroying
    /// itself when the animation ends. It has no collision events: it is the big blast drawn where
    /// something is destroyed outright (par_rock when its colour runs out, obj_meatball,
    /// obj_scrapmental, obj_worm and its segments), not a hitbox. The source has no grenade-firing
    /// weapon; the mine's launch sound is the only other "grenade" asset (sfx_fire_grenade). The
    /// rock and enemy tickets spawn this with <see cref="Spawn"/>.
    /// </summary>
    public class GrenadeExplosion : MonoBehaviour
    {
        public const float LifetimeSteps = 10f / 0.2f;
        public const float Alpha = 0.7f;

        float remaining;

        /// <summary>GML: audio_play_sound(sfx_explode) in Create (the Audio epic hooks in here).</summary>
        public static event Action<GrenadeExplosion> Exploded;

        public float Remaining => remaining;

        /// <summary>GML instance_create(x, y, obj_grenade_explosion).</summary>
        public static GrenadeExplosion Spawn(Vector2 position, Sprite sprite = null)
        {
            var go = new GameObject("GrenadeExplosion");
            var explosion = go.AddComponent<GrenadeExplosion>();
            explosion.Init(position, sprite);
            return explosion;
        }

        public void Init(Vector2 position, Sprite sprite = null)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            if (sprite != null)
            {
                var renderer = gameObject.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.color = new Color(1f, 1f, 1f, Alpha);
            }
            remaining = Gml.Steps(LifetimeSteps);
            Exploded?.Invoke(this);
        }

        void Update() => Tick(Time.deltaTime);

        /// <summary>Counts the animation down; returns false once the explosion is gone.</summary>
        public bool Tick(float dt)
        {
            remaining -= dt;
            if (remaining > 1e-5f) return true;
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            return false;
        }
    }
}
