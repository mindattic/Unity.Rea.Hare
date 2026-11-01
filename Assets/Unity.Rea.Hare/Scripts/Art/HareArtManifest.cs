using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Rea.Hare.Art
{
    /// <summary>One GML sprite (sprites/*.sprite.gmx) as imported: its frames, animation and collision mask.</summary>
    [Serializable]
    public sealed class GmlSpriteAsset
    {
        public string name;
        /// <summary>The sprite's group in HARE.project.gmx's resource tree ("game/enemy"), empty at the top.</summary>
        public string group;
        public int width, height;
        /// <summary>The GML origin in pixels from the top left; every frame's pivot sits on it.</summary>
        public int xorig, yorig;
        public GmlMaskKind mask;
        /// <summary>sepmasks: GameMaker kept a separate precise mask per frame.</summary>
        public bool separateMasks;
        /// <summary>The bounding box in pixels from the top left, every edge inclusive.</summary>
        public int bboxLeft, bboxTop, bboxRight, bboxBottom;
        /// <summary>image_speed (frames per step) and the room_speed (steps per second) the clip is timed for.</summary>
        public float imageSpeed, roomSpeed;
        public Sprite[] frames;
        /// <summary>A looping SpriteRenderer clip, or null for a single frame or an image_speed of 0.</summary>
        public AnimationClip clip;
        /// <summary>The sprite atlas that packs the frames, or empty when the sprite keeps its own textures.</summary>
        public string atlas;

        public GmlBoundingBox BoundingBox => new GmlBoundingBox(bboxLeft, bboxTop, bboxRight, bboxBottom);

        /// <summary>The collision mask in world units around the origin (null for a precise mask; see GmlSpriteGeometry).</summary>
        public Vector2[] MaskOutline(int ellipseSegments = 32)
        {
            var points = GmlSpriteGeometry.MaskOutline(mask, BoundingBox, xorig, yorig, ellipseSegments);
            if (points == null) return null;
            var result = new Vector2[points.Length];
            for (int i = 0; i < points.Length; i++) result[i] = new Vector2(GameClock.Px(points[i].X), GameClock.Px(points[i].Y));
            return result;
        }
    }

    /// <summary>One GML background (background/*.background.gmx) as imported.</summary>
    [Serializable]
    public sealed class GmlBackgroundAsset
    {
        public string name;
        public string group;
        public int width, height;
        /// <summary>HTile / VTile: the texture repeats along that axis.</summary>
        public bool tileHorizontal, tileVertical;
        /// <summary>Pivoted on the top left, where GameMaker draws a background from.</summary>
        public Sprite sprite;
    }

    /// <summary>
    /// Every GML sprite and background name mapped to the Unity assets it was imported as. Generated, never edited by
    /// hand: tools/import-gml-art.ps1 mirrors the original's images and writes Art/gml-art.json, and the editor's
    /// GmlArtBuilder turns that into this asset (Resources/Hare/HareArtManifest) along with the animation clips and
    /// sprite atlases. Load it with <see cref="Load"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Unity.Rea.Hare/Art Manifest", fileName = "HareArtManifest")]
    public sealed class HareArtManifest : ScriptableObject
    {
        public const string ResourcePath = "Hare/HareArtManifest";

        [SerializeField] List<GmlSpriteAsset> sprites = new List<GmlSpriteAsset>();
        [SerializeField] List<GmlBackgroundAsset> backgrounds = new List<GmlBackgroundAsset>();

        Dictionary<string, GmlSpriteAsset> spriteIndex;
        Dictionary<string, GmlBackgroundAsset> backgroundIndex;
        static HareArtManifest loaded;

        public IReadOnlyList<GmlSpriteAsset> Sprites => sprites;
        public IReadOnlyList<GmlBackgroundAsset> Backgrounds => backgrounds;

        /// <summary>The project's manifest from Resources, or null if the art has not been imported.</summary>
        public static HareArtManifest Load()
        {
            if (loaded == null) loaded = Resources.Load<HareArtManifest>(ResourcePath);
            return loaded;
        }

        /// <summary>Replaces the whole manifest (the editor's builder only).</summary>
        public void Set(List<GmlSpriteAsset> newSprites, List<GmlBackgroundAsset> newBackgrounds)
        {
            sprites = newSprites ?? throw new ArgumentNullException(nameof(newSprites));
            backgrounds = newBackgrounds ?? throw new ArgumentNullException(nameof(newBackgrounds));
            spriteIndex = null;
            backgroundIndex = null;
        }

        public GmlSpriteAsset FindSprite(string name)
        {
            if (spriteIndex == null)
            {
                spriteIndex = new Dictionary<string, GmlSpriteAsset>(sprites.Count, StringComparer.Ordinal);
                foreach (var s in sprites) spriteIndex[s.name] = s;
            }
            return name != null && spriteIndex.TryGetValue(name, out var found) ? found : null;
        }

        /// <summary>Frame <paramref name="index"/> of GML sprite <paramref name="name"/>, wrapping like image_index, or null.</summary>
        public Sprite Frame(string name, int index = 0)
        {
            var s = FindSprite(name);
            if (s == null || s.frames == null || s.frames.Length == 0) return null;
            int n = s.frames.Length;
            return s.frames[((index % n) + n) % n];
        }

        public GmlBackgroundAsset FindBackground(string name)
        {
            if (backgroundIndex == null)
            {
                backgroundIndex = new Dictionary<string, GmlBackgroundAsset>(backgrounds.Count, StringComparer.Ordinal);
                foreach (var b in backgrounds) backgroundIndex[b.name] = b;
            }
            return name != null && backgroundIndex.TryGetValue(name, out var found) ? found : null;
        }

        void OnEnable()
        {
            spriteIndex = null;
            backgroundIndex = null;
        }
    }
}
