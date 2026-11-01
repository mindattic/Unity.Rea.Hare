using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Unity.Rea.Hare.EditorTools
{
    /// <summary>
    /// Art/gml-art.json, the file tools/import-gml-art.ps1 writes from HARE.gmx: every GML sprite and background, its
    /// source data and the Unity assets it becomes, plus what was not imported and why. The editor reads it and
    /// never writes it; to change the art, rerun the tool.
    /// </summary>
    [Serializable]
    public sealed class GmlArtData
    {
        public const string ArtFolder = "Assets/Unity.Rea.Hare/Art";
        public const string DataPath = ArtFolder + "/gml-art.json";
        public const string SpritesFolder = ArtFolder + "/Sprites/";
        public const string BackgroundsFolder = ArtFolder + "/Backgrounds/";
        public const string AnimationsFolder = ArtFolder + "/Animations/";
        public const string AtlasesFolder = ArtFolder + "/Atlases/";

        public string source;
        public int pixelsPerUnit;
        public int defaultRoomSpeed;
        public int atlasMaxSide;
        public SpriteData[] sprites;
        public BackgroundData[] backgrounds;
        public SkippedData[] skipped;

        [Serializable]
        public sealed class SpriteData
        {
            public string name, group;
            public int width, height, xorig, yorig;
            /// <summary>0 precise, 1 rectangle, 2 ellipse, 3 diamond (<see cref="Unity.Rea.Hare.Art.GmlMaskKind"/>).</summary>
            public int colkind, coltolerance;
            public bool sepmasks;
            public int bboxmode, bboxLeft, bboxTop, bboxRight, bboxBottom;
            public bool tileHorizontal, tileVertical;
            public float imageSpeed;
            public string imageSpeedSource;
            public float roomSpeed;
            public string[] frames;
            public string clip, atlas;
            public string[] notes;
        }

        [Serializable]
        public sealed class BackgroundData
        {
            public string name, group;
            public int width, height;
            public bool tileHorizontal, tileVertical, tileset;
            public int tileWidth, tileHeight;
            public string texture;
        }

        [Serializable]
        public sealed class SkippedData
        {
            public string name, kind, reason;
        }

        [NonSerialized] Dictionary<string, SpriteData> spriteByFrame;
        [NonSerialized] Dictionary<string, BackgroundData> backgroundByTexture;

        /// <summary>The sprite one of whose frames is at <paramref name="assetPath"/>, or null.</summary>
        public SpriteData SpriteForFrame(string assetPath)
        {
            if (spriteByFrame == null)
            {
                spriteByFrame = new Dictionary<string, SpriteData>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in sprites)
                    foreach (var f in s.frames) spriteByFrame[f] = s;
            }
            return spriteByFrame.TryGetValue(assetPath, out var found) ? found : null;
        }

        /// <summary>The background whose image is at <paramref name="assetPath"/>, or null.</summary>
        public BackgroundData BackgroundForTexture(string assetPath)
        {
            if (backgroundByTexture == null)
            {
                backgroundByTexture = new Dictionary<string, BackgroundData>(StringComparer.OrdinalIgnoreCase);
                foreach (var b in backgrounds) backgroundByTexture[b.texture] = b;
            }
            return backgroundByTexture.TryGetValue(assetPath, out var found) ? found : null;
        }

        static GmlArtData cached;
        static DateTime cachedStamp;

        /// <summary>The project's gml-art.json, re-read whenever the file changes; null if the tool has not run.</summary>
        public static GmlArtData Load()
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

        /// <summary>Parses and validates the file, so a malformed one fails loudly here rather than as missing art.</summary>
        public static GmlArtData Parse(string json)
        {
            var data = JsonUtility.FromJson<GmlArtData>(json);
            if (data == null) throw new InvalidDataException(DataPath + " is not valid JSON");
            if (data.pixelsPerUnit <= 0) throw new InvalidDataException(DataPath + ": pixelsPerUnit must be positive");
            if (data.defaultRoomSpeed <= 0) throw new InvalidDataException(DataPath + ": defaultRoomSpeed must be positive");
            data.sprites ??= Array.Empty<SpriteData>();
            data.backgrounds ??= Array.Empty<BackgroundData>();
            data.skipped ??= Array.Empty<SkippedData>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in data.sprites)
            {
                if (string.IsNullOrEmpty(s.name) || !names.Add(s.name)) throw new InvalidDataException(DataPath + ": sprite name missing or repeated: " + s.name);
                if (s.width <= 0 || s.height <= 0) throw new InvalidDataException(DataPath + ": " + s.name + " has no size");
                if (s.frames == null || s.frames.Length == 0) throw new InvalidDataException(DataPath + ": " + s.name + " has no frames");
                if (s.colkind < 0 || s.colkind > 3) throw new InvalidDataException(DataPath + ": " + s.name + " has unknown colkind " + s.colkind);
                if (s.imageSpeed < 0f || s.roomSpeed <= 0f) throw new InvalidDataException(DataPath + ": " + s.name + " has a bad image_speed or room speed");
                foreach (var f in s.frames)
                    if (!f.StartsWith(SpritesFolder, StringComparison.Ordinal)) throw new InvalidDataException(DataPath + ": " + s.name + " frame outside " + SpritesFolder + ": " + f);
            }
            foreach (var b in data.backgrounds)
            {
                if (string.IsNullOrEmpty(b.name) || !names.Add(b.name)) throw new InvalidDataException(DataPath + ": background name missing or repeated: " + b.name);
                if (b.texture == null || !b.texture.StartsWith(BackgroundsFolder, StringComparison.Ordinal)) throw new InvalidDataException(DataPath + ": " + b.name + " texture outside " + BackgroundsFolder);
            }
            return data;
        }
    }
}
