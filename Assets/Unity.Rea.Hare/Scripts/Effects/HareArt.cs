using System.Collections.Generic;
using UnityEngine;
using Unity.Hare.Art;
using Unity.Hare.Audio;
using Unity.Hare.Weapons;

namespace Unity.Hare.Effects
{
    /// <summary>
    /// Loads the original's art and sound from Resources/Hare, in code, so no object needs a
    /// reference wired in the Inspector. Sprites are gmlHARE's frames (Resources/Hare/Sprites/
    /// spr_name_frame, pivot on the GML origin, see the editor's HareArtImporter), else the imported art's
    /// <see cref="HareArtManifest"/> (U.HARE-T46, pivots on the same origins); sounds come from
    /// <see cref="HareAudio"/> by GML sound name (the imported sound/ resources, U.HARE-T47). A missing
    /// asset gives null, so logic still runs without art.
    /// </summary>
    public static class HareArt
    {
        public const string SpritesPath = "Hare/Sprites/";
        public const string ParticlesPath = "Hare/Particles/";

        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();

        /// <summary>Frame <paramref name="frame"/> of GML sprite <paramref name="name"/>, or null.</summary>
        public static Sprite Frame(string name, int frame = 0)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string key = name + "_" + frame;
            if (!sprites.TryGetValue(key, out var sprite))
            {
                sprite = Resources.Load<Sprite>(SpritesPath + key);
                // Sprites not copied under Resources/Hare/Sprites come from the imported art (U.HARE-T46's manifest).
                if (sprite == null)
                {
                    // No wrapping here (the manifest's Frame wraps like image_index): a frame past the end is null.
                    var manifest = HareArtManifest.Load();
                    var asset = manifest != null ? manifest.FindSprite(name) : null;
                    if (asset?.frames != null && frame >= 0 && frame < asset.frames.Length) sprite = asset.frames[frame];
                }
                sprites[key] = sprite;
            }
            return sprite;
        }

        /// <summary>A GameMaker particle shape texture (pt_shape_flare, pt_shape_explosion), or null.</summary>
        public static Texture2D ParticleShape(string name)
        {
            if (!textures.TryGetValue(name, out var texture))
            {
                texture = Resources.Load<Texture2D>(ParticlesPath + name);
                textures[name] = texture;
            }
            return texture;
        }

        /// <summary>The clip for GML sound <paramref name="name"/> (<see cref="HareAudio.Clip"/>), or null.</summary>
        public static AudioClip Clip(string name) => HareAudio.Clip(name);

        /// <summary>
        /// GML draw order: a lower depth draws on top, so the sorting order is -depth
        /// (obj_tinyexplosion and obj_grenade_explosion -4 above the enemies' -3, gibs and death
        /// particles 0 beneath them).
        /// </summary>
        public static int SortingOrderForDepth(int depth) => -depth;

        /// <summary>A sprite renderer showing frame 0 of <paramref name="name"/> on <paramref name="host"/>.</summary>
        public static SpriteRenderer Show(GameObject host, string name, int depth, float alpha = 1f)
        {
            var renderer = host.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = host.AddComponent<SpriteRenderer>();
            renderer.sprite = Frame(name);
            renderer.sortingOrder = SortingOrderForDepth(depth);
            renderer.color = new Color(1f, 1f, 1f, alpha);
            return renderer;
        }

        /// <summary>
        /// GML in_view(x, y): strictly inside the view. With no orthographic main camera (tests,
        /// before the arena camera of UH-T52) everything counts as in view.
        /// </summary>
        public static bool InView(Vector2 point)
        {
            var camera = Camera.main;
            if (camera == null || !camera.orthographic) return true;
            Vector3 v = camera.WorldToViewportPoint(point);
            return v.x > 0f && v.x < 1f && v.y > 0f && v.y < 1f;
        }

        /// <summary>GML audio_play_sound(name, 1, false): <see cref="HareAudio.Play"/>.</summary>
        public static void Play(string name) => HareAudio.Play(name);

        /// <summary>GML x/y offset in px (y grows downward) as a Unity world offset.</summary>
        public static Vector2 GmlOffset(float dxPx, float dyPx) => new Vector2(Gml.Px(dxPx), -Gml.Px(dyPx));
    }
}
