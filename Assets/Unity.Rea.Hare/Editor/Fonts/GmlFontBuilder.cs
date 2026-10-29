using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using Unity.Hare.Text;
using Unity.Hare.UI;

namespace Unity.Hare.EditorTools
{
    /// <summary>
    /// Turns Fonts/gml-fonts.json (written by tools/import-gml-fonts.ps1) into the rebuild's fonts: one static bitmap
    /// <see cref="FontAsset"/> per GML font at <c>Resources/Hare/Fonts/&lt;name&gt;.asset</c>, built from GameMaker's own
    /// baked glyph atlas and glyph table, so text draws with exactly the glyphs the original drew, pixel for pixel at
    /// 1:1, and no typeface file (Arial, Berlin Sans FB: not redistributable) is ever needed. <see cref="HareFonts"/> and
    /// the Theme.uss text styles (<see cref="HareTextStyles"/>) load them.
    ///
    /// Each glyph's metrics come from Core's <see cref="GmlFontMetrics"/> (cell top on the line top, GameMaker's
    /// <c>shift</c> and <c>offset</c> as advance and bearing, <c>string_height("M")</c> lines). The face's point size is
    /// the font.gmx size, so a style at <c>font-size</c> equal to that size draws the atlas 1:1. The cells are copied
    /// byte for byte out of GameMaker's atlas (Fonts/Atlases/&lt;name&gt;.png, read straight from the file) onto a
    /// repacked Alpha8 atlas with <see cref="GmlFontMetrics.AtlasGap"/> clear pixels around each
    /// (<see cref="GmlFontMetrics.Pack"/>), saved inside the font asset as TextCore's own atlases are.
    ///
    /// TextCore has no public way to author a static FontAsset from an existing atlas, so the first build asks
    /// <see cref="FontAsset.CreateFontAsset(Font,int,int,GlyphRenderMode,int,int,AtlasPopulationMode,bool)"/> for an empty
    /// asset (so its serialized layout and material are whatever this Unity version expects) and then sets everything
    /// that matters itself, because that call resolves the built-in font to the system's Arial and ignores the requested
    /// render mode, atlas size and padding: face, glyph and character tables, atlas, bitmap render mode, atlas size, no
    /// source font, no fallbacks, and saved (not DontSave) hide flags. A rebuild rewrites the existing asset, material and
    /// atlas in place, so their GUIDs and file IDs hold and the same data writes the same bytes.
    ///
    /// Batch: tools/unity.ps1 -Method Unity.Hare.EditorTools.GmlFontBuilder.BuildAll (tools/import-gml-fonts.ps1 runs it).
    /// </summary>
    public static class GmlFontBuilder
    {
        public const string OutputFolder = "Assets/Unity.Rea.Hare/Resources/" + HareFonts.ResourceFolder;

        /// <summary>The render mode every GML font is built with: anti-aliased bitmap glyphs (GameMaker's <c>aa</c>).</summary>
        public const GlyphRenderMode RenderMode = GlyphRenderMode.SMOOTH;

        public static string AssetPath(string fontName) => OutputFolder + fontName + ".asset";

        [MenuItem("Unity.Hare/Build Fonts From gml-fonts.json")]
        public static void BuildAll()
        {
            var data = GmlFontData.Load() ?? throw new FileNotFoundException(
                GmlFontData.DataPath + " is missing; run tools/import-gml-fonts.ps1 first", GmlFontData.DataPath);

            Directory.CreateDirectory(OutputFolder);
            foreach (var font in data.fonts) Build(font);
            AssetDatabase.SaveAssets();
            HareFonts.ClearCache();

            Debug.Log($"[Unity.Hare] GML fonts built: {data.fonts.Length} fonts, {data.skipped.Length} skipped");
            foreach (var f in data.fonts)
            {
                var asset = AssetDatabase.LoadAssetAtPath<FontAsset>(AssetPath(f.name));
                Debug.Log($"[Unity.Hare] Font {f.name}: {f.Description}, {f.glyphs.Length} glyphs, line {GmlFontMetrics.LineHeight(f.Glyphs())} px, atlas {asset.atlasWidth}x{asset.atlasHeight}");
            }
            foreach (var s in data.skipped) Debug.Log($"[Unity.Hare] Not imported: {s.kind} {s.name}: {s.reason}");
        }

        /// <summary>GameMaker's atlas for <paramref name="font"/> as RGBA pixels, bottom row first, read from the PNG itself.</summary>
        public static Color32[] ReadSourceAtlas(GmlFontData.FontData font)
        {
            if (!File.Exists(font.atlas)) throw new FileNotFoundException(font.atlas + " is missing; rerun tools/import-gml-fonts.ps1", font.atlas);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(font.atlas))) throw new InvalidDataException(font.atlas + " is not a readable image");
                if (texture.width != font.atlasWidth || texture.height != font.atlasHeight)
                    throw new InvalidDataException($"{font.atlas} is {texture.width}x{texture.height}, not the font.gmx's {font.atlasWidth}x{font.atlasHeight}");
                return texture.GetPixels32();
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        /// <summary>Builds (or rebuilds in place) one font's FontAsset and returns it.</summary>
        public static FontAsset Build(GmlFontData.FontData font)
        {
            var source = ReadSourceAtlas(font);
            var glyphs = font.Glyphs();
            var packed = GmlFontMetrics.Pack(glyphs);
            var alpha = new byte[packed.Width * packed.Height];
            for (int i = 0; i < glyphs.Length; i++)
            {
                GmlGlyph from = glyphs[i], to = packed.Glyphs[i];
                for (int row = 0; row < from.Height; row++)
                for (int col = 0; col < from.Width; col++)
                    alpha[(packed.Height - 1 - (to.Y + row)) * packed.Width + to.X + col] =
                        source[(font.atlasHeight - 1 - (from.Y + row)) * font.atlasWidth + from.X + col].a;
            }

            string path = AssetPath(font.name);
            var asset = AssetDatabase.LoadAssetAtPath<FontAsset>(path) ?? Create(font, path);
            var atlas = Atlas(asset, font, path);
            if (atlas.width != packed.Width || atlas.height != packed.Height || atlas.format != TextureFormat.Alpha8)
                atlas.Reinitialize(packed.Width, packed.Height, TextureFormat.Alpha8, false);
            atlas.LoadRawTextureData(alpha);
            atlas.Apply(false, false);

            int line = GmlFontMetrics.LineHeight(glyphs);
            int descent = GmlFontMetrics.Descent(glyphs);
            asset.faceInfo = new FaceInfo
            {
                familyName = font.family,
                styleName = font.bold ? (font.italic ? "Bold Italic" : "Bold") : (font.italic ? "Italic" : "Regular"),
                pointSize = font.size,
                scale = 1f,
                lineHeight = line,
                ascentLine = line,
                capLine = line,
                meanLine = line / 2f,
                baseline = 0f,
                descentLine = descent,
                superscriptOffset = line,
                superscriptSize = 0.5f,
                subscriptOffset = descent,
                subscriptSize = 0.5f,
                underlineOffset = descent / 2f,
                underlineThickness = 1f,
                strikethroughOffset = line / 3f,
                strikethroughThickness = 1f,
                tabWidth = Array.Find(glyphs, g => g.Character == ' ').Shift,
            };

            asset.glyphTable.Clear();
            asset.characterTable.Clear();
            foreach (var g in packed.Glyphs)
            {
                var t = GmlFontMetrics.ToTextCore(g, packed.Height, line);
                var glyph = new Glyph((uint)t.Unicode,
                    new GlyphMetrics(t.Width, t.Height, t.BearingX, t.BearingY, t.Advance),
                    new GlyphRect(t.RectX, t.RectY, t.Width, t.Height), 1f, 0);
                asset.glyphTable.Add(glyph);
                asset.characterTable.Add(new Character((uint)t.Unicode, glyph));
            }

            asset.name = font.name;
            asset.hideFlags = HideFlags.None;
            asset.atlasTextures = new[] { atlas };
            var material = asset.material;
            material.name = font.name + " Material";
            material.hideFlags = HideFlags.None;
            material.mainTexture = atlas;
            // TextCore reads the atlas padding back from the material's gradient scale (padding + 1); the repacked atlas
            // keeps its clear gap between cells, but no glyph is drawn with any padding of its own.
            material.SetFloat("_GradientScale", 1f);
            material.SetFloat("_TextureWidth", packed.Width);
            material.SetFloat("_TextureHeight", packed.Height);

            var so = new SerializedObject(asset);
            ClearReference(so, "m_SourceFontFile");
            ClearReference(so, "m_SourceFontFile_EditorRef");
            SetString(so, "m_SourceFontFileGUID", string.Empty);
            SetString(so, "m_SourceFontFilePath", string.Empty);
            SetString(so, "m_fontAssetCreationEditorSettings.sourceFontFileGUID", string.Empty);
            SetInt(so, "m_AtlasPopulationMode", (int)AtlasPopulationMode.Static);
            SetBool(so, "InternalDynamicOS", false);
            SetBool(so, "m_ClearDynamicDataOnBuild", false);
            SetBool(so, "m_IsMultiAtlasTexturesEnabled", false);
            SetBool(so, "m_GetFontFeatures", false);
            SetInt(so, "m_AtlasWidth", packed.Width);
            SetInt(so, "m_AtlasHeight", packed.Height);
            SetInt(so, "m_AtlasPadding", 0);
            SetInt(so, "m_AtlasRenderMode", (int)RenderMode);
            SetInt(so, "m_AtlasTextureIndex", 0);
            ClearArray(so, "m_FallbackFontAssetTable");
            ClearArray(so, "m_UsedGlyphRects");
            ClearArray(so, "m_FreeGlyphRects");
            so.ApplyModifiedPropertiesWithoutUndo();

            asset.ReadFontAssetDefinition();
            EditorUtility.SetDirty(atlas);
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        /// <summary>A new font asset at <paramref name="path"/>, from TextCore's own template, with its material inside it.</summary>
        static FontAsset Create(GmlFontData.FontData font, string path)
        {
            var placeholder = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var asset = FontAsset.CreateFontAsset(placeholder, font.size, 0, RenderMode, font.atlasWidth, font.atlasHeight,
                AtlasPopulationMode.Static, false)
                ?? throw new InvalidOperationException("TextCore could not create a FontAsset template for " + font.name);
            foreach (var t in asset.atlasTextures) if (t != null) UnityEngine.Object.DestroyImmediate(t);
            asset.atlasTextures = Array.Empty<Texture2D>();
            asset.hideFlags = HideFlags.None;
            asset.material.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            return asset;
        }

        /// <summary>The font's atlas texture, stored inside its asset; created there on the first build.</summary>
        static Texture2D Atlas(FontAsset asset, GmlFontData.FontData font, string path)
        {
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Texture2D existing) return existing;
            var atlas = new Texture2D(font.atlasWidth, font.atlasHeight, TextureFormat.Alpha8, false)
            {
                name = font.name + " Atlas",
                filterMode = FilterMode.Bilinear, // HARE draws everything interpolated (option_interpolate)
                wrapMode = TextureWrapMode.Clamp,
            };
            AssetDatabase.AddObjectToAsset(atlas, asset);
            return atlas;
        }

        static void ClearReference(SerializedObject so, string property)
        {
            var p = so.FindProperty(property);
            if (p != null && p.propertyType == SerializedPropertyType.ObjectReference) p.objectReferenceValue = null;
        }

        static void SetString(SerializedObject so, string property, string value)
        {
            var p = so.FindProperty(property);
            if (p != null) p.stringValue = value;
        }

        static void SetInt(SerializedObject so, string property, int value)
        {
            var p = so.FindProperty(property);
            if (p != null) p.intValue = value;
        }

        static void SetBool(SerializedObject so, string property, bool value)
        {
            var p = so.FindProperty(property);
            if (p != null) p.boolValue = value;
        }

        static void ClearArray(SerializedObject so, string property)
        {
            var p = so.FindProperty(property);
            if (p != null && p.isArray) p.ClearArray();
        }
    }
}
