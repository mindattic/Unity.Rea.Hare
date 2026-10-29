using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.Hare.Text;

namespace Unity.Hare.EditorTools
{
    /// <summary>
    /// Fonts/gml-fonts.json, the file tools/import-gml-fonts.ps1 writes from HARE.gmx: every GML font with its
    /// font.gmx fields (face, size, bold, italic, charset, anti-aliasing, ranges), its baked glyph table and atlas, the
    /// objects that draw with it, the licence of each typeface, and what was not imported and why. The editor reads it
    /// and never writes it; to change the fonts, rerun the tool.
    /// </summary>
    [Serializable]
    public sealed class GmlFontData
    {
        public const string FontsFolder = "Assets/Unity.Rea.Hare/Fonts";
        public const string DataPath = FontsFolder + "/gml-fonts.json";
        public const string AtlasFolder = FontsFolder + "/Atlases/";

        public string source;
        public LicenceData[] licences;
        public FontData[] fonts;
        public SkippedData[] skipped;

        [Serializable]
        public sealed class LicenceData
        {
            public string family, owner, licence, rebuild;
        }

        [Serializable]
        public sealed class FontData
        {
            public string name;
            /// <summary>False for a font.gmx left on disk but not in HARE.project.gmx (font4).</summary>
            public bool inResourceTree;
            /// <summary>The typeface GameMaker rasterised, and the size (points) and style it rasterised it at.</summary>
            public string family;
            public int size;
            public bool bold, italic;
            public int charset, antialias;
            public string[] ranges;
            public string image, atlas;
            public int atlasWidth, atlasHeight;
            public string[] usedBy, notes;
            public KerningData[] kerning;
            public GlyphData[] glyphs;

            /// <summary>The glyph table as Core's <see cref="GmlGlyph"/>s.</summary>
            public GmlGlyph[] Glyphs()
            {
                var list = new GmlGlyph[glyphs.Length];
                for (int i = 0; i < glyphs.Length; i++)
                {
                    var g = glyphs[i];
                    list[i] = new GmlGlyph(g.character, g.x, g.y, g.w, g.h, g.shift, g.offset);
                }
                return list;
            }

            /// <summary>"Berlin Sans FB 36", with " Bold"/" Italic" when set: the face as GameMaker's font editor shows it.</summary>
            public string Description => family + " " + size + (bold ? " Bold" : "") + (italic ? " Italic" : "");
        }

        [Serializable]
        public sealed class GlyphData
        {
            public int character, x, y, w, h, shift, offset;
        }

        [Serializable]
        public sealed class KerningData
        {
            public int first, second, amount;
        }

        [Serializable]
        public sealed class SkippedData
        {
            public string name, kind, reason;
        }

        static GmlFontData cached;
        static DateTime cachedStamp;

        /// <summary>The project's gml-fonts.json, re-read whenever the file changes; null if the tool has not run.</summary>
        public static GmlFontData Load()
        {
            if (!File.Exists(DataPath)) return null;
            var stamp = File.GetLastWriteTimeUtc(DataPath);
            if (cached == null || stamp != cachedStamp)
            {
                cached = Parse(File.ReadAllText(DataPath));
                cachedStamp = stamp;
            }
            return cached;
        }

        /// <summary>Parses and validates the file, so a malformed one fails loudly here rather than as missing text.</summary>
        public static GmlFontData Parse(string json)
        {
            var data = JsonUtility.FromJson<GmlFontData>(json);
            if (data == null) throw new InvalidDataException(DataPath + " is not valid JSON");
            data.licences ??= Array.Empty<LicenceData>();
            data.fonts ??= Array.Empty<FontData>();
            data.skipped ??= Array.Empty<SkippedData>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            var families = new HashSet<string>(StringComparer.Ordinal);
            foreach (var l in data.licences) families.Add(l.family);
            foreach (var f in data.fonts)
            {
                if (string.IsNullOrEmpty(f.name) || !names.Add(f.name)) throw new InvalidDataException(DataPath + ": font name missing or repeated: " + f.name);
                if (string.IsNullOrEmpty(f.family)) throw new InvalidDataException(DataPath + ": " + f.name + " names no typeface");
                if (!families.Contains(f.family)) throw new InvalidDataException(DataPath + ": " + f.name + "'s typeface " + f.family + " has no licence entry");
                if (f.size <= 0) throw new InvalidDataException(DataPath + ": " + f.name + " has no size");
                if (f.atlasWidth <= 0 || f.atlasHeight <= 0) throw new InvalidDataException(DataPath + ": " + f.name + " has no atlas size");
                if (f.atlas == null || !f.atlas.StartsWith(AtlasFolder, StringComparison.Ordinal)) throw new InvalidDataException(DataPath + ": " + f.name + " atlas outside " + AtlasFolder + ": " + f.atlas);
                if (Path.GetFileNameWithoutExtension(f.atlas) != f.name) throw new InvalidDataException(DataPath + ": " + f.name + " atlas is not named for its font: " + f.atlas);
                if (f.glyphs == null || f.glyphs.Length == 0) throw new InvalidDataException(DataPath + ": " + f.name + " has no glyphs");
                f.usedBy ??= Array.Empty<string>();
                f.notes ??= Array.Empty<string>();
                f.kerning ??= Array.Empty<KerningData>();
                f.ranges ??= Array.Empty<string>();
                var characters = new HashSet<int>();
                foreach (var g in f.glyphs)
                {
                    if (!characters.Add(g.character)) throw new InvalidDataException(DataPath + ": " + f.name + " repeats glyph " + g.character);
                    if (g.w < 0 || g.h < 0 || g.x < 0 || g.y < 0 || g.x + g.w > f.atlasWidth || g.y + g.h > f.atlasHeight)
                        throw new InvalidDataException(DataPath + ": " + f.name + " glyph " + g.character + " falls outside its " + f.atlasWidth + "x" + f.atlasHeight + " atlas");
                }
            }
            return data;
        }
    }
}
