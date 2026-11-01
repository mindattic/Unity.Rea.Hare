using UnityEditor;
using UnityEngine;
using Unity.Rea.Hare.Art;

namespace Unity.Rea.Hare.EditorTools
{
    /// <summary>
    /// Import settings for the art tools/import-gml-art.ps1 mirrors into Assets/Unity.Rea.Hare/Art, read from
    /// Art/gml-art.json so nothing is set by hand in the Inspector and every .meta is generated:
    ///
    /// Sprite frames (Art/Sprites): single sprites at the manifest's pixels per unit (GameClock.PixelsPerUnit), the
    /// pivot on the GML origin, a full-rect mesh (GameMaker draws the whole frame), bilinear (HARE's
    /// option_interpolate is on), clamped unless the sprite tiles, uncompressed and without mipmaps (2D at a fixed
    /// reference resolution). A precise mask (colkind 0) keeps Unity's alpha-traced physics shape, the nearest thing to
    /// GameMaker's per-pixel test; the other masks are closed shapes the runtime builds from the bounding box
    /// (GmlSpriteAsset.MaskOutline), so they generate none.
    ///
    /// Backgrounds (Art/Backgrounds): single sprites pivoted on the top left, where GameMaker draws a background
    /// from, repeating along any axis the background tiles, compressed at high quality (BIBLE: art pipeline).
    ///
    /// The import depends on gml-art.json, so a changed origin, mask or tiling reimports the affected art.
    /// </summary>
    public class GmlArtImporter : AssetPostprocessor
    {
        /// <summary>Raise when the settings below change, so Unity reimports the art with them.</summary>
        public override uint GetVersion() => 1;

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            bool sprite = path.StartsWith(GmlArtData.SpritesFolder);
            if (!sprite && !path.StartsWith(GmlArtData.BackgroundsFolder)) return;
            context.DependsOnSourceAsset(GmlArtData.DataPath);
            var data = GmlArtData.Load();
            if (data == null)
            {
                Debug.LogWarning("[Unity.Rea.Hare] " + path + " has no " + GmlArtData.DataPath + "; run tools/import-gml-art.ps1");
                return;
            }
            var importer = (TextureImporter)assetImporter;
            if (sprite)
            {
                var entry = data.SpriteForFrame(path);
                if (entry == null) Debug.LogWarning("[Unity.Rea.Hare] " + path + " is not a frame in " + GmlArtData.DataPath);
                else Configure(importer, entry, data.pixelsPerUnit);
            }
            else
            {
                var entry = data.BackgroundForTexture(path);
                if (entry == null) Debug.LogWarning("[Unity.Rea.Hare] " + path + " is not a background in " + GmlArtData.DataPath);
                else Configure(importer, entry, data.pixelsPerUnit);
            }
        }

        /// <summary>Applies a sprite frame's settings; true if anything changed.</summary>
        public static bool Configure(TextureImporter importer, GmlArtData.SpriteData sprite, int pixelsPerUnit)
        {
            var pivot = GmlSpriteGeometry.Pivot(sprite.xorig, sprite.yorig, sprite.width, sprite.height);
            return Apply(importer, pixelsPerUnit, new Vector2(pivot.X, pivot.Y),
                WrapFor(sprite.tileHorizontal), WrapFor(sprite.tileVertical),
                physicsShape: sprite.colkind == (int)GmlMaskKind.Precise,
                compression: TextureImporterCompression.Uncompressed);
        }

        /// <summary>Applies a background's settings; true if anything changed.</summary>
        public static bool Configure(TextureImporter importer, GmlArtData.BackgroundData background, int pixelsPerUnit)
        {
            return Apply(importer, pixelsPerUnit, new Vector2(0f, 1f),
                WrapFor(background.tileHorizontal), WrapFor(background.tileVertical),
                physicsShape: false, compression: TextureImporterCompression.CompressedHQ);
        }

        static TextureWrapMode WrapFor(bool tiles) => tiles ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

        static bool Apply(TextureImporter importer, int pixelsPerUnit, Vector2 pivot, TextureWrapMode wrapU,
            TextureWrapMode wrapV, bool physicsShape, TextureImporterCompression compression)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool changed =
                settings.textureType != TextureImporterType.Sprite ||
                settings.spriteMode != (int)SpriteImportMode.Single ||
                !Mathf.Approximately(settings.spritePixelsPerUnit, pixelsPerUnit) ||
                settings.spriteAlignment != (int)SpriteAlignment.Custom ||
                (settings.spritePivot - pivot).sqrMagnitude > 1e-12f ||
                settings.spriteMeshType != SpriteMeshType.FullRect ||
                settings.spriteGenerateFallbackPhysicsShape != physicsShape ||
                settings.mipmapEnabled ||
                !settings.alphaIsTransparency ||
                settings.filterMode != FilterMode.Bilinear ||
                settings.wrapModeU != wrapU || settings.wrapModeV != wrapV ||
                importer.textureCompression != compression ||
                importer.maxTextureSize != 2048;
            if (!changed) return false;

            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spritePixelsPerUnit = pixelsPerUnit;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = physicsShape;
            settings.mipmapEnabled = false;
            settings.alphaIsTransparency = true;
            settings.filterMode = FilterMode.Bilinear;
            settings.wrapMode = wrapU;
            settings.wrapModeU = wrapU;
            settings.wrapModeV = wrapV;
            importer.SetTextureSettings(settings);
            importer.textureCompression = compression;
            // Every image in the original is at most 1920 px on a side, so 2048 never downscales one.
            importer.maxTextureSize = 2048;
            return true;
        }
    }
}
