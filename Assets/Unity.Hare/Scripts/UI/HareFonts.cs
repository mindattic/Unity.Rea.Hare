using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Unity.Hare.UI
{
    /// <summary>
    /// The original's eight <c>draw_set_font</c> faces (<c>fnt_basic</c>, <c>fnt_medium</c>, <c>fnt_point</c>,
    /// <c>fnt_score</c>, <c>fnt_small</c>, <c>font4</c>, <c>font7</c>, <c>font8</c>), one named slot per face, each a
    /// static bitmap <see cref="FontAsset"/> at <c>Resources/Hare/Fonts/&lt;name&gt;</c> built by the font pipeline
    /// (U.HARE-T48: tools/import-gml-fonts.ps1, then the editor's GmlFontBuilder) from GameMaker's own baked glyph atlas
    /// and glyph table, so text draws with exactly the glyphs the original drew. Screens normally reach a face through
    /// a named text style (<see cref="HareTextStyles"/>, Theme.uss) rather than by face name.
    ///
    /// A face draws its atlas 1:1 at a font size equal to its font.gmx size (<see cref="NativeSize"/>), which
    /// <see cref="Apply"/> sets along with the face.
    /// </summary>
    public static class HareFonts
    {
        public const string Basic = "fnt_basic";
        public const string Medium = "fnt_medium";
        public const string Point = "fnt_point";
        public const string Score = "fnt_score";
        public const string Small = "fnt_small";
        public const string Font4 = "font4";
        public const string Font7 = "font7";
        public const string Font8 = "font8";

        /// <summary>Every face name this seam knows about, in the order <c>docs/BIBLE.md</c> lists them.</summary>
        public static readonly string[] All = { Basic, Medium, Point, Score, Small, Font4, Font7, Font8 };

        /// <summary>Resources folder (with trailing slash) holding one FontAsset per face, named for it.</summary>
        public const string ResourceFolder = "Hare/Fonts/";

        // null is a valid cached value (an unknown face name) so a miss isn't re-resolved from Resources.Load on
        // every call; ClearCache() below lets a test or the font builder reset it.
        static readonly Dictionary<string, FontAsset> cache = new Dictionary<string, FontAsset>();

        /// <summary>The bitmap font asset for a named face, or null for a name that is not one of the faces.</summary>
        public static FontAsset Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (cache.TryGetValue(name, out var cached) && cached != null) return cached;
            var asset = Resources.Load<FontAsset>(ResourceFolder + name);
            cache[name] = asset;
            return asset;
        }

        /// <summary>The font size, in panel pixels, at which a face draws its baked atlas 1:1 (its font.gmx size);
        /// 0 for an unknown face.</summary>
        public static float NativeSize(string name)
        {
            var asset = Get(name);
            return asset != null ? asset.faceInfo.pointSize : 0f;
        }

        /// <summary>Applies a named face to <paramref name="element"/> at its <see cref="NativeSize"/>; leaves the
        /// element's font alone for an unknown face name.</summary>
        public static void Apply(VisualElement element, string name)
        {
            var asset = Get(name);
            if (asset == null) return;
            element.style.unityFontDefinition = FontDefinition.FromSDFFont(asset);
            element.style.fontSize = asset.faceInfo.pointSize;
        }

        /// <summary>Drops every cached font asset so a rebuilt one is picked up on the next <see cref="Get"/>.</summary>
        public static void ClearCache() => cache.Clear();
    }
}
