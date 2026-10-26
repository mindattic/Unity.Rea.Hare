using System;
using UnityEngine;

namespace Unity.Hare.Effects
{
    /// <summary>
    /// Ports obj_oval_explosion (gmlHARE, depth -4): spr_explosion_oval (256x128 px, origin 128, 64, 12 frames)
    /// at image_speed 0.3, playing sfx_explode when in_view(x, y), destroying itself at Animation End (40 steps).
    /// No object or script in HARE.gmx creates it, so nothing in the rebuild spawns it either; it is ported so
    /// the effect exists as the original defines it (see the BIBLE, V23).
    /// </summary>
    public class OvalExplosion : MonoBehaviour
    {
        public const string SpriteName = "spr_explosion_oval";
        public const string Sound = "sfx_explode";
        public const int Frames = 12;
        public const float ImageSpeed = 0.3f;
        public const int Depth = -4;

        SpriteAnimation anim;

        public static event Action<OvalExplosion> Exploded;

        public float ImageIndex => anim.ImageIndex;

        /// <summary>GML instance_create(x, y, obj_oval_explosion).</summary>
        public static OvalExplosion Spawn(Vector2 at)
        {
            var go = new GameObject("OvalExplosion");
            go.transform.position = new Vector3(at.x, at.y, 0f);
            var explosion = go.AddComponent<OvalExplosion>();
            explosion.anim = new SpriteAnimation(SpriteName, Frames, ImageSpeed, HareArt.Show(go, SpriteName, Depth));
            if (HareArt.InView(at)) HareArt.Play(Sound);
            Exploded?.Invoke(explosion);
            return explosion;
        }

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>Runs the animation for <paramref name="dt"/> seconds; false once it has ended and the explosion is gone.</summary>
        public bool Tick(float dt)
        {
            if (!anim.Advance(SpriteAnimation.StepsIn(dt))) return true;
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            return false;
        }
    }
}
