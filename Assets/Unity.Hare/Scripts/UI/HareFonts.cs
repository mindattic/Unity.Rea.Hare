using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Unity.Hare.UI
{
    /// <summary>
    /// The shared home for the original's eight <c>draw_set_font</c> faces (<c>fnt_basic</c>, <c>fnt_medium</c>,
    /// <c>fnt_point</c>, <c>fnt_score</c>, <c>fnt_small</c>, <c>font4</c>, <c>font7</c>, <c>font8</c>): one named
    /// slot per face, each lazily built into a runtime SDF <see cref="FontAsset"/> (the Unity.GridGame
    /// <c>Fonts.cs</c> pattern) from <c>Resources/Hare/Fonts/&lt;name&gt;</c> the first time it's asked for.
    ///
    /// U.HARE-T48 drops the licensed TTFs under those exact resource names; until then every slot is empty and
    /// <see cref="Apply"/> is a no-op, so screens built against this seam today render in the panel's default font
    /// and pick up the real face the moment T48 lands, with no code change on their side.
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

        /// <summary>Every face name this seam knows about, in the order <c>docs/BIBLE.md</c> §6 lists them.</summary>
        public static readonly string[] All = { Basic, Medium, Point, Score, Small, Font4, Font7, Font8 };

        const string ResourceFolder = "Hare/Fonts/";

        // null is a valid cached value (a face with no TTF imported yet) so a missing face isn't re-resolved
        // from Resources.Load on every call; ClearCache() below lets a test or an importer reset it.
        static readonly Dictionary<string, FontAsset> cache = new Dictionary<string, FontAsset>();

        /// <summary>The runtime SDF font asset for a named face, or null when its TTF (U.HARE-T48) isn't imported yet.</summary>
        public static FontAsset Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (cache.TryGetValue(name, out var cached)) return cached;

            var font = Resources.Load<Font>(ResourceFolder + name);
            var asset = font != null ? FontAsset.CreateFontAsset(font) : null;
            cache[name] = asset;
            return asset;
        }

        /// <summary>Applies a named face to <paramref name="element"/>; leaves the panel's own default font alone
        /// when the face isn't imported yet so nothing breaks before U.HARE-T48.</summary>
        public static void Apply(VisualElement element, string name)
        {
            var asset = Get(name);
            if (asset != null) element.style.unityFontDefinition = FontDefinition.FromSDFFont(asset);
        }

        /// <summary>Drops every cached font asset so a newly imported TTF (or a test's fake Resources folder) is
        /// picked up on the next <see cref="Get"/>.</summary>
        public static void ClearCache() => cache.Clear();
    }
}
