using System;
using System.Collections.Generic;

namespace Unity.Hare.Text
{
    /// <summary>
    /// One glyph of a GameMaker font.gmx: its cell in the baked atlas (x right and y down from the atlas's top left,
    /// exactly as GameMaker stores it), its advance (<c>shift</c>) and its left bearing (<c>offset</c>).
    /// GameMaker crops every cell at the top of the line and at the bottom of the glyph's own ink, so a cell's top is
    /// always the top of the line and only its height varies (taller for descenders).
    /// </summary>
    public readonly struct GmlGlyph
    {
        public readonly int Character, X, Y, Width, Height, Shift, Offset;

        public GmlGlyph(int character, int x, int y, int width, int height, int shift, int offset)
        {
            if (width < 0 || height < 0) throw new ArgumentException($"Glyph {character} has a negative size {width}x{height}");
            Character = character; X = x; Y = y; Width = width; Height = height; Shift = shift; Offset = offset;
        }
    }

    /// <summary>
    /// A glyph in TextCore's terms: its rect in the atlas measured from the atlas's bottom left, its size, and its
    /// bearings and advance from the pen position on the baseline (y up).
    /// </summary>
    public readonly struct TextCoreGlyph
    {
        public readonly int Unicode, RectX, RectY, Width, Height;
        public readonly float BearingX, BearingY, Advance;

        public TextCoreGlyph(int unicode, int rectX, int rectY, int width, int height, float bearingX, float bearingY, float advance)
        {
            Unicode = unicode; RectX = rectX; RectY = rectY; Width = width; Height = height;
            BearingX = bearingX; BearingY = bearingY; Advance = advance;
        }
    }

    /// <summary>A font's glyphs repacked onto a fresh atlas (<see cref="GmlFontMetrics.Pack"/>), and that atlas's size.</summary>
    public readonly struct PackedAtlas
    {
        public readonly int Width, Height;
        public readonly GmlGlyph[] Glyphs;

        public PackedAtlas(int width, int height, GmlGlyph[] glyphs) { Width = width; Height = height; Glyphs = glyphs; }
    }

    /// <summary>
    /// The one place a GameMaker font's baked glyph table becomes the rebuild's (docs/BIBLE.md, font pipeline,
    /// U.HARE-T48). Engine-free, so the editor's font builder and the tests share it.
    ///
    /// GameMaker has no baseline: <c>draw_text</c> puts every cell's top on the line's top (the y it was given) and
    /// steps each next character by the glyph's <c>shift</c>, drawing it <c>offset</c> pixels right of the pen. Lines
    /// of a multi-line string are <c>string_height("M")</c> apart, the height of the <c>M</c> cell, which ends on the
    /// baseline because M has no descender. So the rebuild puts the baseline at the bottom of the M cell: the ascent
    /// is that height, every glyph's top bearing is the ascent (its cell top on the line top, exactly as GameMaker
    /// draws it), and the line height is the same figure.
    /// </summary>
    public static class GmlFontMetrics
    {
        /// <summary>The character whose cell height GameMaker uses for line spacing.</summary>
        public const int LineReferenceCharacter = 'M';

        /// <summary>
        /// The font's line height: the height of its <c>M</c> cell (GameMaker's <c>string_height("M")</c>), or the
        /// tallest cell when the font has no M.
        /// </summary>
        public static int LineHeight(IReadOnlyList<GmlGlyph> glyphs)
        {
            if (glyphs == null || glyphs.Count == 0) throw new ArgumentException("A font needs at least one glyph");
            int tallest = 0;
            foreach (var g in glyphs)
            {
                if (g.Character == LineReferenceCharacter) return g.Height;
                tallest = Math.Max(tallest, g.Height);
            }
            return tallest;
        }

        /// <summary>The tallest cell, reaching furthest below the line's top (the deepest descender).</summary>
        public static int TallestCell(IReadOnlyList<GmlGlyph> glyphs)
        {
            int tallest = 0;
            foreach (var g in glyphs) tallest = Math.Max(tallest, g.Height);
            return tallest;
        }

        /// <summary>How far below the baseline the deepest cell reaches, as a non-positive y (TextCore's descent line).</summary>
        public static int Descent(IReadOnlyList<GmlGlyph> glyphs) => Math.Min(0, LineHeight(glyphs) - TallestCell(glyphs));

        /// <summary>
        /// The glyph in TextCore's terms for an atlas <paramref name="atlasHeight"/> pixels tall whose baseline sits
        /// <paramref name="ascent"/> pixels below the line's top: the rect flipped to the atlas's bottom left, the cell
        /// top on the line top, the GameMaker <c>offset</c> as the left bearing and <c>shift</c> as the advance.
        /// </summary>
        public static TextCoreGlyph ToTextCore(GmlGlyph glyph, int atlasHeight, int ascent)
        {
            int rectY = atlasHeight - glyph.Y - glyph.Height;
            if (rectY < 0 || glyph.X < 0) throw new ArgumentException($"Glyph {glyph.Character}'s cell falls outside a {atlasHeight} px atlas");
            return new TextCoreGlyph(glyph.Character, glyph.X, rectY, glyph.Width, glyph.Height, glyph.Offset, ascent, glyph.Shift);
        }

        /// <summary>
        /// Transparent pixels kept around every glyph when <see cref="Pack"/> repacks a GameMaker atlas. GameMaker packs
        /// its cells edge to edge because it draws each one as an exact quad; UI Toolkit draws a bitmap glyph's quad
        /// a few pixels wider than its cell (5 px a side on Unity 6000.4, measured by HareTextPlayTests), so with no gap
        /// every character drags slivers of its atlas neighbours onto the screen. 16 px is three times that margin.
        /// </summary>
        public const int AtlasGap = 16;

        /// <summary>
        /// Repacks a font's glyph cells into a fresh atlas with <paramref name="gap"/> transparent pixels between cells and
        /// around the edge: the narrowest power-of-two square-or-wider atlas they fit on, tallest cells first, in shelves.
        /// Returns the glyphs in their input order with only their cell position changed (x right and y down from the
        /// top left, as GameMaker stores it), and the atlas size. Deterministic, so a rebuild repacks identically.
        /// </summary>
        public static PackedAtlas Pack(IReadOnlyList<GmlGlyph> glyphs, int gap = AtlasGap)
        {
            if (glyphs == null || glyphs.Count == 0) throw new ArgumentException("A font needs at least one glyph");
            if (gap < 0) throw new ArgumentOutOfRangeException(nameof(gap));
            var order = new int[glyphs.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                int byHeight = glyphs[b].Height.CompareTo(glyphs[a].Height);
                return byHeight != 0 ? byHeight : glyphs[a].Character.CompareTo(glyphs[b].Character);
            });

            for (int width = 64; width <= 8192; width *= 2)
            {
                var placed = new GmlGlyph[glyphs.Count];
                int x = gap, y = gap, row = 0;
                bool fits = true;
                foreach (int i in order)
                {
                    var g = glyphs[i];
                    if (g.Width + 2 * gap > width) { fits = false; break; }
                    if (x + g.Width + gap > width) { x = gap; y += row + gap; row = 0; }
                    placed[i] = new GmlGlyph(g.Character, x, y, g.Width, g.Height, g.Shift, g.Offset);
                    x += g.Width + gap;
                    row = Math.Max(row, g.Height);
                }
                int used = y + row + gap;
                if (!fits || used > width) continue;
                int height = 1;
                while (height < used) height *= 2;
                return new PackedAtlas(width, height, placed);
            }
            throw new ArgumentException("The glyphs do not fit an 8192 px atlas");
        }

        /// <summary>
        /// GameMaker's <c>string_width</c> for one line: the sum of every character's <c>shift</c>; a character the
        /// font has no glyph for adds nothing (GameMaker skips it).
        /// </summary>
        public static int StringWidth(IReadOnlyList<GmlGlyph> glyphs, string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            var shifts = new Dictionary<int, int>(glyphs.Count);
            foreach (var g in glyphs) shifts[g.Character] = g.Shift;
            int width = 0;
            foreach (var c in text) if (shifts.TryGetValue(c, out var s)) width += s;
            return width;
        }
    }
}
