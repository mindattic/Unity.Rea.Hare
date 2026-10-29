using System;
using UnityEngine;

namespace Unity.Hare.Effects
{
    /// <summary>
    /// Ports obj_muzzleflash (gmlHARE, depth -2): spr_muzzleflash_new (96 px, 4 frames, origin at its centre)
    /// at image_alpha 0.95 and image_speed 0.3, turned to image_angle, destroying itself at Animation End
    /// (4 / 0.3, so 14 steps). Its Step's fade-out is commented out in the source and stays out: the flash
    /// is the animation alone. shoot_vulcan creates it at the muzzle (X, Y, before the bullet's vertical
    /// jitter) turned to the bullet's direction; shoot_shotgun at the muzzle turned to the last pellet's
    /// direction (the aim plus that pellet's spread).
    /// </summary>
    public class MuzzleFlash : MonoBehaviour
    {
        public const string SpriteName = "spr_muzzleflash_new";
        public const int Frames = 4;
        public const float ImageSpeed = 0.3f;
        public const float Alpha = 0.95f;
        public const int Depth = -2;

        SpriteAnimation anim;

        public static event Action<MuzzleFlash> Spawned;

        /// <summary>GML image_angle, degrees.</summary>
        public float Angle { get; private set; }
        public float ImageIndex => anim.ImageIndex;

        /// <summary>GML: muzzleflash = instance_create(X, Y, obj_muzzleflash); muzzleflash.image_angle = angle.</summary>
        public static MuzzleFlash Spawn(Vector2 at, float angle)
        {
            var go = new GameObject("MuzzleFlash");
            go.transform.SetPositionAndRotation(new Vector3(at.x, at.y, 0f), Quaternion.Euler(0f, 0f, angle));
            var flash = go.AddComponent<MuzzleFlash>();
            flash.Angle = angle;
            flash.anim = new SpriteAnimation(SpriteName, Frames, ImageSpeed, HareArt.Show(go, SpriteName, Depth, Alpha));
            Spawned?.Invoke(flash);
            return flash;
        }

        void FixedUpdate() => Tick(Time.fixedDeltaTime);

        /// <summary>Runs the animation for <paramref name="dt"/> seconds; false once it has ended and the flash is gone.</summary>
        public bool Tick(float dt)
        {
            if (!anim.Advance(SpriteAnimation.StepsIn(dt))) return true;
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            return false;
        }
    }
}
