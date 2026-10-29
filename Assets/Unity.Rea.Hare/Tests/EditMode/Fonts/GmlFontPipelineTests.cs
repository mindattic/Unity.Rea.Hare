using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using Unity.Hare.EditorTools;
using Unity.Hare.Text;
using Unity.Hare.UI;

namespace Unity.Hare.Tests.EditMode.Fonts
{
    /// <summary>
    /// The imported fonts (U.HARE-T48): what tools/import-gml-fonts.ps1 and GmlFontBuilder produced from HARE.gmx is all
    /// there, records each font's face, size and style, licence and users, is built only from GameMaker's baked atlases
    /// (never a typeface file), carries exactly the GML glyph table, and survives a player build; and the named text
    /// styles in Theme.uss put each kind of text in the face the original drew it with.
    /// </summary>
    public class GmlFontPipelineTests
    {
        const string ThemePath = "Assets/Unity.Rea.Hare/Resources/Hare/UI/Theme.uss";
        GmlFontData data;

        [SetUp]
        public void SetUp()
        {
            data = GmlFontData.Load();
            Assert.That(data, Is.Not.Null, GmlFontData.DataPath + " is missing; run tools/import-gml-fonts.ps1");
        }

        [TearDown]
        public void TearDown() => HareFonts.ClearCache();

        [Test]
        public void EveryFontOfTheOriginalIsImportedWithItsFaceSizeAndStyle()
        {
            Assert.That(data.fonts.Select(f => f.name), Is.EquivalentTo(HareFonts.All));
            Assert.That(data.skipped, Is.Empty, string.Join("\n", data.skipped.Select(s => s.name + ": " + s.reason)));
            var expected = new (string name, string family, int size)[]
            {
                ("fnt_basic", "Arial", 12), ("fnt_medium", "Berlin Sans FB", 16), ("fnt_point", "Berlin Sans FB", 12),
                ("fnt_score", "Berlin Sans FB", 36), ("fnt_small", "Berlin Sans FB", 11),
                ("font4", "Arial", 12), ("font7", "Arial", 12), ("font8", "Arial", 12),
            };
            foreach (var (name, family, size) in expected)
            {
                var f = data.fonts.Single(x => x.name == name);
                Assert.That(f.family, Is.EqualTo(family), name);
                Assert.That(f.size, Is.EqualTo(size), name);
                Assert.That(f.bold || f.italic, Is.False, name + " is regular");
                Assert.That(f.antialias, Is.EqualTo(3), name + " was baked anti-aliased");
                Assert.That(f.glyphs, Has.Length.EqualTo(96), name + ": characters 32-127");
            }
            Assert.That(data.fonts.Single(f => f.name == "font4").inResourceTree, Is.False, "font4 is on disk but not in HARE.project.gmx");
        }

        [Test]
        public void TheFontsTheGameDrawsWithAreRecordedWithTheirUsers()
        {
            FontData("fnt_medium").usedBy.ShouldInclude("obj_typewriter_story");
            FontData("fnt_small").usedBy.ShouldInclude("obj_weapon_gui");
            FontData("fnt_point").usedBy.ShouldInclude("obj_typewriter", "obj_game", "obj_healthbar", "obj_conversation", "obj_float_text");
            FontData("fnt_score").usedBy.ShouldInclude("obj_game", "obj_healthbar", "obj_conversation", "obj_level_results", "obj_btn_campaign");
            foreach (var unused in new[] { "fnt_basic", "font4", "font7", "font8" })
                Assert.That(FontData(unused).usedBy, Is.Empty, unused + " is never passed to draw_set_font");
        }

        [Test]
        public void EveryTypefaceHasALicenceEntryAndNoTypefaceFileIsShipped()
        {
            Assert.That(data.licences.Select(l => l.family), Is.EquivalentTo(new[] { "Arial", "Berlin Sans FB" }));
            foreach (var l in data.licences)
            {
                Assert.That(l.licence, Does.Contain("Proprietary"), l.family);
                Assert.That(l.rebuild, Does.StartWith("Not shipped"), l.family);
            }
            // TextMesh Pro's Essential Resources (imported for the multiplayer kit's front end, U.HARE-T35) carry
            // LiberationSans under the SIL Open Font License, which may be redistributed; it is not one of the two.
            var typefaces = Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories)
                .Where(p => Regex.IsMatch(p, @"\.(ttf|otf|ttc|fon|fnt)$", RegexOptions.IgnoreCase))
                .Where(p => !IsTmpOflFont(p)).ToArray();
            Assert.That(typefaces, Is.Empty, "Arial and Berlin Sans FB may not be redistributed; only GameMaker's baked atlases ship");
        }

        static bool IsTmpOflFont(string path) =>
            path.Replace('\\', '/').StartsWith("Assets/TextMesh Pro/") && File.Exists(Path.ChangeExtension(path, null) + " - OFL.txt");

        [Test]
        public void EachAtlasHoldsGameMakersCellsByteForByteWithClearGapsBetweenThem()
        {
            foreach (var f in data.fonts)
            {
                var asset = HareFonts.Get(f.name);
                var atlas = (Texture2D)asset.atlasTextures[0];
                Assert.That(AssetDatabase.GetAssetPath(atlas), Is.EqualTo(GmlFontBuilder.AssetPath(f.name)), f.name + "'s atlas is stored inside its font asset");
                Assert.That(atlas.format, Is.EqualTo(TextureFormat.Alpha8), f.name);
                Assert.That(atlas.mipmapCount, Is.EqualTo(1), f.name + " has no mipmaps");
                Assert.That(atlas.filterMode, Is.EqualTo(FilterMode.Bilinear), f.name);
                Assert.That(atlas.wrapMode, Is.EqualTo(TextureWrapMode.Clamp), f.name);

                var source = GmlFontBuilder.ReadSourceAtlas(f);
                var glyphs = f.Glyphs();
                var packed = GmlFontMetrics.Pack(glyphs);
                Assert.That(new Vector2Int(atlas.width, atlas.height), Is.EqualTo(new Vector2Int(packed.Width, packed.Height)), f.name);
                var alpha = atlas.GetRawTextureData<byte>();
                long inkInCells = 0, inkInAtlas = 0;
                for (int i = 0; i < glyphs.Length; i++)
                {
                    GmlGlyph from = glyphs[i], to = packed.Glyphs[i];
                    for (int row = 0; row < from.Height; row++)
                    for (int col = 0; col < from.Width; col++)
                    {
                        byte expected = source[(f.atlasHeight - 1 - (from.Y + row)) * f.atlasWidth + from.X + col].a;
                        byte actual = alpha[(packed.Height - 1 - (to.Y + row)) * packed.Width + to.X + col];
                        if (actual != expected) Assert.Fail($"{f.name} '{(char)from.Character}' pixel ({col},{row}) is {actual}, GameMaker baked {expected}");
                        inkInCells += expected;
                    }
                }
                foreach (var a in alpha) inkInAtlas += a;
                Assert.That(inkInAtlas, Is.EqualTo(inkInCells), f.name + ": nothing but the cells is inked; the gaps are clear");
                Assert.That(inkInCells, Is.GreaterThan(0), f.name);
            }
        }

        [Test]
        public void EachFontAssetIsAStaticBitmapFontOfItsGmlAtlas()
        {
            foreach (var f in data.fonts)
            {
                var asset = AssetDatabase.LoadAssetAtPath<FontAsset>(GmlFontBuilder.AssetPath(f.name));
                Assert.That(asset, Is.Not.Null, f.name);
                Assert.That(asset, Is.SameAs(HareFonts.Get(f.name)), f.name + " is what HareFonts loads");
                Assert.That(asset.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static), f.name);
                Assert.That(asset.atlasRenderMode, Is.EqualTo(GlyphRenderMode.SMOOTH), f.name + " is bitmap, not SDF");
                Assert.That(asset.atlasTextures, Has.Length.EqualTo(1), f.name);
                Assert.That(asset.atlasTextures[0], Is.Not.Null, f.name);
                Assert.That(asset.atlasWidth, Is.EqualTo(asset.atlasTextures[0].width), f.name + ": glyph UVs are measured against this");
                Assert.That(asset.atlasHeight, Is.EqualTo(asset.atlasTextures[0].height), f.name);
                Assert.That(asset.atlasPadding, Is.EqualTo(0), f.name);
                Assert.That(asset.sourceFontFile, Is.Null, f.name + " keeps no typeface file");
                Assert.That(asset.fallbackFontAssetTable == null || asset.fallbackFontAssetTable.All(x => x == null), f.name);
                Assert.That(asset.hideFlags, Is.EqualTo(HideFlags.None), f.name + " is saved into a player build");
                Assert.That(asset.material, Is.Not.Null, f.name);
                Assert.That(asset.material.hideFlags, Is.EqualTo(HideFlags.None), f.name);
                Assert.That(asset.atlasTextures[0].hideFlags, Is.EqualTo(HideFlags.None), f.name);
                Assert.That(AssetDatabase.GetAssetPath(asset.material), Is.EqualTo(GmlFontBuilder.AssetPath(f.name)), f.name + "'s material is a sub-asset");
                Assert.That(asset.material.mainTexture, Is.SameAs(asset.atlasTextures[0]), f.name);
                var text = File.ReadAllText(GmlFontBuilder.AssetPath(f.name));
                Assert.That(text, Does.Not.Contain("InternalDynamicOS: 1"), f.name + " is not a system-font asset");
                Assert.That(text, Does.Not.Contain(".ttf"), f.name + " names no typeface file");
            }
        }

        [Test]
        public void EachFontAssetCarriesExactlyTheGmlGlyphTable()
        {
            foreach (var f in data.fonts)
            {
                var asset = HareFonts.Get(f.name);
                var glyphs = f.Glyphs();
                var packed = GmlFontMetrics.Pack(glyphs);
                int line = GmlFontMetrics.LineHeight(glyphs);
                Assert.That(asset.faceInfo.pointSize, Is.EqualTo(f.size), f.name);
                Assert.That(asset.faceInfo.lineHeight, Is.EqualTo(line), f.name);
                Assert.That(asset.faceInfo.ascentLine, Is.EqualTo(line), f.name);
                Assert.That(asset.faceInfo.descentLine, Is.EqualTo(GmlFontMetrics.Descent(glyphs)), f.name);
                Assert.That(asset.characterTable, Has.Count.EqualTo(glyphs.Length), f.name);
                for (int i = 0; i < glyphs.Length; i++)
                {
                    var g = glyphs[i];
                    Assert.That(asset.characterLookupTable.TryGetValue((uint)g.Character, out var character), Is.True, f.name + " " + g.Character);
                    var expected = GmlFontMetrics.ToTextCore(packed.Glyphs[i], packed.Height, line);
                    var m = character.glyph.metrics;
                    var r = character.glyph.glyphRect;
                    string at = f.name + " '" + (char)g.Character + "'";
                    Assert.That(new[] { r.x, r.y, r.width, r.height }, Is.EqualTo(new[] { expected.RectX, expected.RectY, expected.Width, expected.Height }), at + " rect");
                    Assert.That(new[] { m.width, m.height, m.horizontalBearingX, m.horizontalBearingY, m.horizontalAdvance },
                        Is.EqualTo(new[] { (float)g.Width, g.Height, g.Offset, line, g.Shift }), at + ": GameMaker's cell size, offset and shift, cell top on the line top");
                    Assert.That(character.glyph.scale, Is.EqualTo(1f), at);
                }
            }
        }

        [Test]
        public void TheArialAliasesAreTheSameBakeAsFntBasic()
        {
            var basic = FontData("fnt_basic");
            foreach (var alias in new[] { "font4", "font7", "font8" })
            {
                var f = FontData(alias);
                Assert.That(File.ReadAllBytes(f.atlas), Is.EqualTo(File.ReadAllBytes(basic.atlas)), alias);
                Assert.That(f.notes.Any(n => n.Contains("byte-identical bake of fnt_basic")), Is.True, alias);
            }
        }

        [Test]
        public void RebuildingAFontChangesNothing()
        {
            var f = FontData(HareFonts.Point);
            string path = GmlFontBuilder.AssetPath(f.name);
            var before = File.ReadAllText(path);
            var guid = AssetDatabase.AssetPathToGUID(path);
            GmlFontBuilder.Build(f);
            AssetDatabase.SaveAssets();
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            Assert.That(File.ReadAllText(path), Is.EqualTo(before));
        }

        [Test]
        public void EveryNamedTextStyleIsInThemeUssInItsFaceAtItsNativeSize()
        {
            var uss = File.ReadAllText(ThemePath);
            Assert.That(HareTextStyles.All.Select(s => s.UssClass).Distinct().Count(), Is.EqualTo(HareTextStyles.All.Length));
            foreach (var style in HareTextStyles.All)
            {
                var rule = Regex.Match(uss, @"\." + Regex.Escape(style.UssClass) + @"\s*\{([^}]*)\}");
                Assert.That(rule.Success, Is.True, style.UssClass + " has a rule in Theme.uss");
                var face = Regex.Match(rule.Groups[1].Value, @"-unity-font-definition:\s*resource\(""Hare/Fonts/([A-Za-z0-9_]+)""\)");
                var size = Regex.Match(rule.Groups[1].Value, @"font-size:\s*(\d+)px");
                Assert.That(face.Groups[1].Value, Is.EqualTo(style.Face), style.Name);
                Assert.That(int.Parse(size.Groups[1].Value), Is.EqualTo(FontData(style.Face).size), style.Name + " draws the bake 1:1");
            }
            // Every resource("Hare/Fonts/...") the stylesheet names resolves to a built font.
            foreach (Match m in Regex.Matches(uss, @"resource\(""Hare/Fonts/([A-Za-z0-9_]+)""\)"))
                Assert.That(HareFonts.Get(m.Groups[1].Value), Is.Not.Null, m.Value);
        }

        [Test]
        public void TheStylesUseTheFacesTheOriginalDrawsThatTextWith()
        {
            Assert.That(HareTextStyles.HudScore.Face, Is.EqualTo(HareFonts.Score));
            Assert.That(HareTextStyles.HudText.Face, Is.EqualTo(HareFonts.Point));
            Assert.That(HareTextStyles.HudAmmo.Face, Is.EqualTo(HareFonts.Small));
            Assert.That(HareTextStyles.MenuTitle.Face, Is.EqualTo(HareFonts.Score));
            Assert.That(HareTextStyles.MenuText.Face, Is.EqualTo(HareFonts.Point));
            Assert.That(HareTextStyles.Typewriter.Face, Is.EqualTo(HareFonts.Point));
            Assert.That(HareTextStyles.StoryTypewriter.Face, Is.EqualTo(HareFonts.Medium));
            // Every style's face is one the original actually passes to draw_set_font for that object, except Default
            // (text drawn before any draw_set_font: GameMaker's own Arial 12, which fnt_basic bakes).
            foreach (var style in HareTextStyles.All.Where(s => s.Name != nameof(HareTextStyles.Default)))
                Assert.That(FontData(style.Face).usedBy, Is.Not.Empty, style.Name);
        }

        [Test]
        public void ApplyingAStyleReplacesTheElementsOtherTextStyle()
        {
            var label = new UnityEngine.UIElements.Label("x");
            HareTextStyles.Apply(label, HareTextStyles.HudText);
            HareTextStyles.Apply(label, HareTextStyles.HudScore);
            Assert.That(label.ClassListContains("text-hud-score"), Is.True);
            Assert.That(label.ClassListContains("text-hud"), Is.False);
        }

        GmlFontData.FontData FontData(string name) => data.fonts.Single(f => f.name == name);
    }

    static class UsedByAssert
    {
        public static void ShouldInclude(this string[] usedBy, params string[] objects)
        {
            foreach (var o in objects) Assert.That(usedBy, Does.Contain(o));
        }
    }
}
