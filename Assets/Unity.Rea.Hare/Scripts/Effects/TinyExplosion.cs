using System;
using UnityEngine;

namespace Unity.Rea.Hare.Effects
{
    /// <summary>
    /// Ports obj_tinyexplosion (gmlHARE, depth -4): spr_explosion (32 px, 7 frames, origin at its
    /// centre) at image_speed 0.2, drawn by its Draw event at alpha 0.5, unscaled and unrotated,
    /// destroying itself at Animation End (35 steps). It has no collision events: it is the puff
    /// where a bullet or rocket meets something (the enemies spawn it at the round's position on a
    /// hit, the dagger and sawblade at their own when the shield wall kills them). Its Create has
    /// audio_play_sound(sfx_bullet_explosion) commented out, and this port keeps it silent: every
    /// bullet impact would otherwise play it, which reads as a deliberate mix choice, not a defect.
    /// </summary>
    public class TinyExplosion : MonoBehaviour
    {
        public const string SpriteName = "spr_explosion";
        public const int Frames = 7;
        public const float ImageSpeed = 0.2f;
        public const float Alpha = 0.5f;
        public const int Depth = -4;
        public const float LifetimeSteps = Frames / ImageSpeed;

        SpriteAnimation anim;

        public static event Action<TinyExplosion> Spawned;

        public float ImageIndex => anim.ImageIndex;

        /// <summary>GML instance_create(x, y, obj_tinyexplosion).</summary>
        public static TinyExplosion Spawn(Vector2 at)
        {
            var go = new GameObject("TinyExplosion");
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var explosion = go.AddComponent<TinyExplosion>();
            explosion.anim = new SpriteAnimation(SpriteName, Frames, ImageSpeed, HareArt.Show(go, SpriteName, Depth, Alpha));
            Spawned?.Invoke(explosion);
            return explosion;
        }

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>One GML step's worth of animation; false once it has ended and the explosion is gone.</summary>
        public bool Tick(float dt)
        {
            if (!anim.Advance(SpriteAnimation.StepsIn(dt))) return true;
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            return false;
        }
    }
}
