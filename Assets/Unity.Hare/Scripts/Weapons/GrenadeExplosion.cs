using System;
using UnityEngine;
using Unity.Hare.Effects;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// Ports obj_grenade_explosion (gmlHARE, depth -4): spr_grenade_explode (200x182 px, origin
    /// 100, 91, 9 frames at image_speed 0.2, so 45 steps) drawn at image_alpha 0.7, playing
    /// sfx_explode when it is in view, then destroying itself at Animation End. It has no
    /// collision events: it is the big blast drawn where something is destroyed outright (par_rock
    /// when its colour runs out, obj_meatball, obj_scrapmental, obj_worm and its segments), not a
    /// hitbox. The source has no grenade-firing weapon; the mine's launch sound is the only other
    /// "grenade" asset (sfx_fire_grenade).
    /// </summary>
    public class GrenadeExplosion : MonoBehaviour
    {
        public const string SpriteName = "spr_grenade_explode";
        public const string Sound = "sfx_explode";
        public const int Frames = 9;
        public const float ImageSpeed = 0.2f;
        public const float LifetimeSteps = Frames / ImageSpeed;
        public const float Alpha = 0.7f;
        public const int Depth = -4;

        SpriteAnimation anim;
        float remaining;

        /// <summary>GML Create: audio_play_sound(sfx_explode) when in_view(x, y).</summary>
        public static event Action<GrenadeExplosion> Exploded;

        public float Remaining => remaining;

        /// <summary>GML instance_create(x, y, obj_grenade_explosion).</summary>
        public static GrenadeExplosion Spawn(Vector2 position)
        {
            var go = new GameObject("GrenadeExplosion");
            var explosion = go.AddComponent<GrenadeExplosion>();
            explosion.Init(position);
            return explosion;
        }

        public void Init(Vector2 position)
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            anim = new SpriteAnimation(SpriteName, Frames, ImageSpeed, HareArt.Show(gameObject, SpriteName, Depth, Alpha));
            remaining = Gml.Steps(LifetimeSteps);
            if (HareArt.InView(position)) HareArt.Play(Sound);
            Exploded?.Invoke(this);
        }

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>Runs the animation; returns false once it has ended and the explosion is gone.</summary>
        public bool Tick(float dt)
        {
            remaining -= dt;
            if (!anim.Advance(SpriteAnimation.StepsIn(dt))) return true;
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            return false;
        }
    }
}
