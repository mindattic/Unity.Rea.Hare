using UnityEngine;
using Unity.Hare.Weapons;

namespace Unity.Hare.Effects
{
    /// <summary>
    /// GML image_index += image_speed each step over a sprite's frames, with the Animation End
    /// event raised the step image_index passes the last frame. The VFX objects that destroy
    /// themselves there (obj_tinyexplosion, obj_grenade_explosion) last frames / image_speed steps.
    /// </summary>
    public sealed class SpriteAnimation
    {
        readonly string sprite;
        readonly int frames;
        readonly float imageSpeed;
        readonly SpriteRenderer renderer;
        float imageIndex;

        public SpriteAnimation(string sprite, int frames, float imageSpeed, SpriteRenderer renderer)
        {
            this.sprite = sprite;
            this.frames = frames;
            this.imageSpeed = imageSpeed;
            this.renderer = renderer;
        }

        public float ImageIndex => imageIndex;
        public int Frame => Mathf.Min(frames - 1, Mathf.FloorToInt(imageIndex + 1e-4f));

        /// <summary>Steps from Create to Animation End.</summary>
        public float LengthSteps => frames / imageSpeed;

        /// <summary>Advances <paramref name="steps"/> steps; true once the animation has ended.</summary>
        public bool Advance(float steps)
        {
            imageIndex += imageSpeed * steps;
            if (renderer != null) renderer.sprite = HareArt.Frame(sprite, Frame);
            return imageIndex >= frames - 1e-4f;
        }

        /// <summary>Advances <paramref name="steps"/> steps for an object that loops (image_index wraps at the last frame).</summary>
        public void AdvanceLooping(float steps)
        {
            imageIndex = (imageIndex + imageSpeed * steps) % frames;
            if (renderer != null) renderer.sprite = HareArt.Frame(sprite, Frame);
        }

        /// <summary>Back to frame 0 (GML image_index = 0).</summary>
        public void Rewind()
        {
            imageIndex = 0f;
            if (renderer != null) renderer.sprite = HareArt.Frame(sprite, 0);
        }

        /// <summary>Seconds per GML step, for callers counting real time.</summary>
        public static float StepsIn(float seconds) => seconds * Gml.StepsPerSecond;
    }
}
