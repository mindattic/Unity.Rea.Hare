using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unity.Hare.EditorTools
{
    /// <summary>
    /// Import settings for the original's art, so nothing has to be set by hand in the Inspector.
    /// Frames under Resources/Hare/Sprites (gmlHARE's sprites/images/*.png, named spr_name_frame)
    /// become single sprites at <see cref="Unity.Hare.Weapons.Gml.PixelsPerUnit"/> with their pivot
    /// on the GML origin listed in sprites.txt, filtered bilinearly (HARE's option_interpolate is
    /// on) and uncompressed. The GameMaker particle shapes under Resources/Hare/Particles stay
    /// textures, clamped, for <see cref="Unity.Hare.Effects.GmlEffectBurst"/> to draw.
    /// </summary>
    public class HareArtImporter : AssetPostprocessor
    {
        public const string SpritesFolder = "Assets/Unity.Hare/Resources/Hare/Sprites/";
        public const string ParticlesFolder = "Assets/Unity.Hare/Resources/Hare/Particles/";
        public const string OriginsFile = SpritesFolder + "sprites.txt";

        void OnPreprocessTexture()
        {
            string path = assetPath.Replace('\\', '/');
            var importer = (TextureImporter)assetImporter;
            if (path.StartsWith(SpritesFolder))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = Unity.Hare.Weapons.Gml.PixelsPerUnit;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spriteMeshType = SpriteMeshType.FullRect;
                settings.spritePivot = Pivot(Path.GetFileNameWithoutExtension(path));
                importer.SetTextureSettings(settings);
            }
            else if (path.StartsWith(ParticlesFolder))
            {
                importer.textureType = TextureImporterType.Default;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
            }
        }

        /// <summary>GML's origin (xorig, yorig from the top left) as a Unity pivot (from the bottom left).</summary>
        static Vector2 Pivot(string frameName)
        {
            int cut = frameName.LastIndexOf('_');
            string sprite = cut > 0 ? frameName.Substring(0, cut) : frameName;
            return Origins().TryGetValue(sprite, out var pivot) ? pivot : new Vector2(0.5f, 0.5f);
        }

        static Dictionary<string, Vector2> Origins()
        {
            var result = new Dictionary<string, Vector2>();
            if (!File.Exists(OriginsFile)) return result;
            foreach (var line in File.ReadAllLines(OriginsFile))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                var f = line.Split(' ');
                if (f.Length < 5) continue;
                float x = float.Parse(f[1]), y = float.Parse(f[2]), w = float.Parse(f[3]), h = float.Parse(f[4]);
                result[f[0]] = new Vector2(x / w, (h - y) / h);
            }
            return result;
        }
    }
}
