using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using Unity.Hare.Art;

namespace Unity.Hare.EditorTools
{
    /// <summary>
    /// Turns Art/gml-art.json (written by tools/import-gml-art.ps1) into the Unity side of the original's art: makes
    /// sure every frame and background carries <see cref="GmlArtImporter"/>'s settings, writes a looping SpriteRenderer
    /// animation clip per animated sprite (Art/Animations, image_speed x room_speed frames per second), a Sprite
    /// Atlas V2 per sprite group (Art/Atlases), and the runtime <see cref="HareArtManifest"/>
    /// (Resources/Hare/HareArtManifest) mapping every GML sprite and background name to its assets. Stale clips and
    /// atlases are deleted. Rerunning it on the same data rewrites the same files.
    ///
    /// Batch: tools/unity.ps1 -Method Unity.Hare.EditorTools.GmlArtBuilder.BuildAll (tools/import-gml-art.ps1 runs it).
    /// </summary>
    public static class GmlArtBuilder
    {
        public const string ManifestPath = "Assets/Unity.Rea.Hare/Resources/" + HareArtManifest.ResourcePath + ".asset";

        /// <summary>Atlas pages are at most this big; a sprite over gml-art.json's atlasMaxSide is never packed.</summary>
        public const int AtlasMaxTextureSize = 4096;

        [MenuItem("Unity.Hare/Build Art From gml-art.json")]
        public static void BuildAll()
        {
            var data = GmlArtData.Load() ?? throw new FileNotFoundException(
                GmlArtData.DataPath + " is missing; run tools/import-gml-art.ps1 first", GmlArtData.DataPath);

            // Sprite Atlas V2, packed for player builds (the editor draws the source textures).
            if (EditorSettings.spritePackerMode != SpritePackerMode.SpriteAtlasV2Build)
                EditorSettings.spritePackerMode = SpritePackerMode.SpriteAtlasV2Build;

            AssetDatabase.Refresh();
            int reimported = EnsureImportSettings(data);
            int clips = BuildClips(data);
            int atlases = BuildAtlases(data);
            BuildManifest(data);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Unity.Hare] GML art built: {data.sprites.Length} sprites, {data.backgrounds.Length} backgrounds, " +
                      $"{clips} clips, {atlases} atlases, {reimported} textures reimported, {data.skipped.Length} skipped");
            foreach (var s in data.skipped) Debug.Log($"[Unity.Hare] Not imported: {s.kind} {s.name}: {s.reason}");
        }

        /// <summary>Reimports any texture whose settings are not yet the importer's (e.g. imported before it compiled).</summary>
        static int EnsureImportSettings(GmlArtData data)
        {
            var stale = new List<string>();
            foreach (var s in data.sprites)
                foreach (var frame in s.frames)
                    if (GmlArtImporter.Configure(Importer(frame), s, data.pixelsPerUnit)) stale.Add(frame);
            foreach (var b in data.backgrounds)
                if (GmlArtImporter.Configure(Importer(b.texture), b, data.pixelsPerUnit)) stale.Add(b.texture);
            if (stale.Count == 0) return 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in stale)
                {
                    AssetDatabase.WriteImportSettingsIfDirty(path);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            return stale.Count;
        }

        static TextureImporter Importer(string path) =>
            AssetImporter.GetAtPath(path) as TextureImporter
            ?? throw new FileNotFoundException(path + " is in " + GmlArtData.DataPath + " but not imported; rerun tools/import-gml-art.ps1", path);

        static Sprite[] Frames(GmlArtData.SpriteData s) =>
            s.frames.Select(f => AssetDatabase.LoadAssetAtPath<Sprite>(f)
                ?? throw new InvalidOperationException(f + " did not import as a sprite")).ToArray();

        /// <summary>
        /// One clip per sprite gml-art.json gives a clip path: its frames in order on SpriteRenderer.m_Sprite, each held
        /// 1 / image_speed steps at the sprite's room_speed, looping as GameMaker's image_index wraps.
        /// </summary>
        static int BuildClips(GmlArtData data)
        {
            var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in data.sprites)
            {
                if (string.IsNullOrEmpty(s.clip)) continue;
                wanted.Add(s.clip);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(s.clip);
                if (clip == null)
                {
                    EnsureFolder(Path.GetDirectoryName(s.clip));
                    clip = new AnimationClip();
                    AssetDatabase.CreateAsset(clip, s.clip);
                }
                Fill(clip, s.name, Frames(s), GmlSpriteGeometry.FramesPerSecond(s.imageSpeed, s.roomSpeed));
                EditorUtility.SetDirty(clip);
            }
            DeleteStale(GmlArtData.AnimationsFolder, "*.anim", wanted);
            return wanted.Count;
        }

        /// <summary>The binding every clip animates: the sprite a SpriteRenderer on the animated object shows.</summary>
        public static readonly EditorCurveBinding SpriteBinding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");

        static void Fill(AnimationClip clip, string name, Sprite[] frames, float fps)
        {
            clip.name = name;
            clip.ClearCurves();
            AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding, null);
            clip.frameRate = fps;
            // Unity holds a sprite curve's last key for one frame at the clip's frame rate, so the clip lasts
            // frames / fps seconds, every frame shown equally long, as GameMaker's image_index advances.
            var keys = new ObjectReferenceKeyframe[frames.Length];
            for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
            AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding, keys);
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        /// <summary>One Sprite Atlas V2 per atlas path in gml-art.json, packing every frame of the sprites that name it.</summary>
        static int BuildAtlases(GmlArtData data)
        {
            var groups = data.sprites.Where(s => !string.IsNullOrEmpty(s.atlas))
                .GroupBy(s => s.atlas, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.Ordinal);
            var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var group in groups)
            {
                string path = group.Key;
                wanted.Add(path);
                EnsureFolder(Path.GetDirectoryName(path));
                var asset = new SpriteAtlasAsset();
                var textures = group.OrderBy(s => s.name, StringComparer.Ordinal)
                    .SelectMany(s => s.frames).Select(f => (UnityEngine.Object)AssetDatabase.LoadAssetAtPath<Texture2D>(f)).ToArray();
                asset.Add(textures);
                SpriteAtlasAsset.Save(asset, path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

                var importer = (SpriteAtlasImporter)AssetImporter.GetAtPath(path);
                importer.includeInBuild = true;
                importer.packingSettings = new SpriteAtlasPackingSettings
                {
                    blockOffset = 1,
                    padding = 4,               // bilinear filtering samples a neighbour; keep frames from bleeding
                    enableRotation = false,
                    enableTightPacking = false, // full-rect sprites
                    enableAlphaDilation = true,
                };
                importer.textureSettings = new SpriteAtlasTextureSettings
                {
                    readable = false,
                    generateMipMaps = false,
                    sRGB = true,
                    filterMode = FilterMode.Bilinear,
                    anisoLevel = 0,
                };
                var platform = importer.GetPlatformSettings("DefaultTexturePlatform");
                platform.maxTextureSize = AtlasMaxTextureSize;
                platform.textureCompression = TextureImporterCompression.Uncompressed;
                platform.format = TextureImporterFormat.Automatic;
                importer.SetPlatformSettings(platform);
                importer.SaveAndReimport();
            }
            DeleteStale(GmlArtData.AtlasesFolder, "*.spriteatlasv2", wanted);
            return wanted.Count;
        }

        /// <summary>The runtime manifest: every GML name to its frames, clip, mask and atlas, in gml-art.json's order.</summary>
        static void BuildManifest(GmlArtData data)
        {
            var sprites = new List<GmlSpriteAsset>(data.sprites.Length);
            foreach (var s in data.sprites)
            {
                sprites.Add(new GmlSpriteAsset
                {
                    name = s.name, group = s.group,
                    width = s.width, height = s.height, xorig = s.xorig, yorig = s.yorig,
                    mask = (GmlMaskKind)s.colkind, separateMasks = s.sepmasks,
                    bboxLeft = s.bboxLeft, bboxTop = s.bboxTop, bboxRight = s.bboxRight, bboxBottom = s.bboxBottom,
                    imageSpeed = s.imageSpeed, roomSpeed = s.roomSpeed,
                    frames = Frames(s),
                    clip = string.IsNullOrEmpty(s.clip) ? null : AssetDatabase.LoadAssetAtPath<AnimationClip>(s.clip),
                    atlas = s.atlas ?? "",
                });
            }
            var backgrounds = new List<GmlBackgroundAsset>(data.backgrounds.Length);
            foreach (var b in data.backgrounds)
            {
                backgrounds.Add(new GmlBackgroundAsset
                {
                    name = b.name, group = b.group, width = b.width, height = b.height,
                    tileHorizontal = b.tileHorizontal, tileVertical = b.tileVertical,
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(b.texture)
                        ?? throw new InvalidOperationException(b.texture + " did not import as a sprite"),
                });
            }

            var manifest = AssetDatabase.LoadAssetAtPath<HareArtManifest>(ManifestPath);
            if (manifest == null)
            {
                EnsureFolder(Path.GetDirectoryName(ManifestPath));
                manifest = ScriptableObject.CreateInstance<HareArtManifest>();
                AssetDatabase.CreateAsset(manifest, ManifestPath);
            }
            manifest.Set(sprites, backgrounds);
            EditorUtility.SetDirty(manifest);
        }

        static void DeleteStale(string folder, string pattern, HashSet<string> wanted)
        {
            if (!Directory.Exists(folder)) return;
            foreach (var file in Directory.GetFiles(folder, pattern, SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                if (!wanted.Contains(path)) AssetDatabase.DeleteAsset(path);
            }
        }

        static void EnsureFolder(string folder)
        {
            folder = folder.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
