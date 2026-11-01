using System;
using NUnit.Framework;
using Unity.Rea.Hare.Text;
using Assert = NUnit.Framework.Legacy.ClassicAssert;

namespace MindAttic.Hare.Tests
{
    /// <summary>U.HARE-T48: a GameMaker font.gmx glyph table becomes TextCore glyphs that lay text out the way draw_text did.</summary>
    public class GmlFontMetricsTests
    {
        // Real cells from fnt_basic.font.gmx (Arial 12, 256x128 atlas).
        static readonly GmlGlyph M = new GmlGlyph('M', 235, 2, 13, 19, 17, 2);
        static readonly GmlGlyph Q = new GmlGlyph('q', 187, 27, 9, 23, 11, 1);
        static readonly GmlGlyph Underscore = new GmlGlyph('_', 0, 0, 11, 23, 11, 0);
        static readonly GmlGlyph Caret = new GmlGlyph('^', 0, 0, 8, 12, 8, 0);
        static readonly GmlGlyph J = new GmlGlyph('j', 0, 0, 4, 23, 4, -1);
        static readonly GmlGlyph[] Basic = { Caret, M, Q, Underscore, J };

        [Test]
        public void LineHeightIsTheMCellsHeight_StringHeightOfM()
        {
            Assert.AreEqual(19, GmlFontMetrics.LineHeight(Basic));
        }

        [Test]
        public void LineHeightFallsBackToTheTallestCellWithoutAnM()
        {
            Assert.AreEqual(23, GmlFontMetrics.LineHeight(new[] { Caret, Q }));
        }

        [Test]
        public void DescentIsHowFarTheDeepestCellReachesBelowTheMBaseline()
        {
            Assert.AreEqual(23, GmlFontMetrics.TallestCell(Basic));
            Assert.AreEqual(-4, GmlFontMetrics.Descent(Basic));
            Assert.AreEqual(0, GmlFontMetrics.Descent(new[] { M, Caret }));
        }

        [Test]
        public void ARectIsFlippedToTheAtlasBottomLeft()
        {
            var g = GmlFontMetrics.ToTextCore(M, 128, 19);
            Assert.AreEqual(235, g.RectX);
            Assert.AreEqual(128 - 2 - 19, g.RectY);
            Assert.AreEqual(13, g.Width);
            Assert.AreEqual(19, g.Height);
        }

        [Test]
        public void EveryCellTopSitsOnTheLineTopAndShiftAndOffsetBecomeAdvanceAndBearing()
        {
            foreach (var glyph in Basic)
            {
                var g = GmlFontMetrics.ToTextCore(glyph, 128, 19);
                Assert.AreEqual(19f, g.BearingY, "cell top = line top, whatever the cell's height");
                Assert.AreEqual(glyph.Offset, g.BearingX);
                Assert.AreEqual(glyph.Shift, g.Advance);
            }
            // q's 23 px cell hangs 4 px below the baseline, as its descender does in the atlas.
            var q = GmlFontMetrics.ToTextCore(Q, 128, 19);
            Assert.AreEqual(-4f, q.BearingY - q.Height);
        }

        [Test]
        public void ACellOutsideTheAtlasIsRefused()
        {
            Assert.Throws<ArgumentException>(() => GmlFontMetrics.ToTextCore(new GmlGlyph('x', 0, 120, 5, 19, 5, 0), 128, 19));
        }

        [Test]
        public void StringWidthSumsShiftsAndSkipsMissingCharacters()
        {
            Assert.AreEqual(17 + 11 + 17, GmlFontMetrics.StringWidth(Basic, "MqM"));
            Assert.AreEqual(17, GmlFontMetrics.StringWidth(Basic, "Mé"));
            Assert.AreEqual(0, GmlFontMetrics.StringWidth(Basic, ""));
            Assert.AreEqual(0, GmlFontMetrics.StringWidth(Basic, null));
        }

        [Test]
        public void AnEmptyFontIsRefused()
        {
            Assert.Throws<ArgumentException>(() => GmlFontMetrics.LineHeight(Array.Empty<GmlGlyph>()));
            Assert.Throws<ArgumentException>(() => GmlFontMetrics.Pack(Array.Empty<GmlGlyph>()));
        }

        [Test]
        public void PackingKeepsEveryGlyphButMovesItsCell()
        {
            var packed = GmlFontMetrics.Pack(Basic, 16);
            Assert.AreEqual(Basic.Length, packed.Glyphs.Length);
            for (int i = 0; i < Basic.Length; i++)
            {
                var a = Basic[i]; var b = packed.Glyphs[i];
                Assert.AreEqual(a.Character, b.Character, "input order kept");
                Assert.AreEqual(new[] { a.Width, a.Height, a.Shift, a.Offset }, new[] { b.Width, b.Height, b.Shift, b.Offset });
            }
        }

        [Test]
        public void PackedCellsKeepTheGapFromEachOtherAndTheEdge()
        {
            var glyphs = new GmlGlyph[96];
            for (int i = 0; i < glyphs.Length; i++) glyphs[i] = new GmlGlyph(32 + i, 0, 0, 5 + i % 40, 20 + i % 47, 10, 0);
            const int gap = 16;
            var packed = GmlFontMetrics.Pack(glyphs, gap);
            Assert.That(packed.Width & (packed.Width - 1), NUnit.Framework.Is.EqualTo(0), "power of two");
            Assert.That(packed.Height & (packed.Height - 1), NUnit.Framework.Is.EqualTo(0), "power of two");
            Assert.That(packed.Height, NUnit.Framework.Is.LessThanOrEqualTo(packed.Width));
            for (int i = 0; i < glyphs.Length; i++)
            {
                var a = packed.Glyphs[i];
                Assert.That(a.X >= gap && a.Y >= gap && a.X + a.Width + gap <= packed.Width && a.Y + a.Height + gap <= packed.Height, $"glyph {i} keeps the edge gap");
                for (int j = i + 1; j < glyphs.Length; j++)
                {
                    var b = packed.Glyphs[j];
                    bool apart = a.X + a.Width + gap <= b.X || b.X + b.Width + gap <= a.X || a.Y + a.Height + gap <= b.Y || b.Y + b.Height + gap <= a.Y;
                    Assert.That(apart, $"glyphs {i} and {j} are {gap} px apart");
                }
            }
        }

        [Test]
        public void PackingIsDeterministic()
        {
            var a = GmlFontMetrics.Pack(Basic);
            var b = GmlFontMetrics.Pack(Basic);
            Assert.AreEqual(a.Width, b.Width);
            Assert.AreEqual(a.Height, b.Height);
            for (int i = 0; i < Basic.Length; i++) Assert.AreEqual((a.Glyphs[i].X, a.Glyphs[i].Y), (b.Glyphs[i].X, b.Glyphs[i].Y));
        }
    }
}
