using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.U2D;
using Unity.Hare.Art;
using Unity.Hare.EditorTools;

namespace Unity.Hare.Tests.EditMode.Art
{
    /// <summary>
    /// The imported art (U.HARE-T46): what tools/import-gml-art.ps1 and GmlArtBuilder produced from HARE.gmx is all
    /// there, wired into the runtime manifest, and carries the original's origins, masks, timing and tiling.
    /// </summary>
    public class GmlArtPipelineTests
    {
        GmlArtData data;
        HareArtManifest manifest;

        [SetUp]
        public void SetUp()
        {
            data = GmlArtData.Load();
            Assert.That(data, Is.Not.Null, GmlArtData.DataPath + " is missing; run tools/import-gml-art.ps1");
            manifest = HareArtManifest.Load();
            Assert.That(manifest, Is.Not.Null, "Resources/" + HareArtManifest.ResourcePath + " is missing; run GmlArtBuilder.BuildAll");
        }

        [Test]
        public void EverySpriteAndBackgroundOfTheOriginalIsImported()
        {
            // HARE.project.gmx lists 213 sprites (398 frames) and 18 backgrounds; none is skipped.
            Assert.That(data.sprites, Has.Length.EqualTo(213));
            Assert.That(data.sprites.Sum(s => s.frames.Length), Is.EqualTo(398));
            Assert.That(data.backgrounds, Has.Length.EqualTo(18));
            Assert.That(data.skipped, Is.Empty, string.Join("\n", data.skipped.Select(s => s.name + ": " + s.reason)));
        }

        [Test]
        public void TheManifestMapsEveryGmlNameToItsAssets()
        {
            Assert.That(manifest.Sprites.Count, Is.EqualTo(data.sprites.Length));
            Assert.That(manifest.Backgrounds.Count, Is.EqualTo(data.backgrounds.Length));
            foreach (var s in data.sprites)
            {
                var entry = manifest.FindSprite(s.name);
                Assert.That(entry, Is.Not.Null, s.name);
                Assert.That(entry.frames, Has.Length.EqualTo(s.frames.Length), s.name);
                for (int i = 0; i < s.frames.Length; i++)
                {
                    Assert.That(entry.frames[i], Is.Not.Null, s.name + " frame " + i);
                    Assert.That(AssetDatabase.GetAssetPath(entry.frames[i]), Is.EqualTo(s.frames[i]), s.name + " frame " + i);
                }
                Assert.That(entry.clip != null, Is.EqualTo(!string.IsNullOrEmpty(s.clip)), s.name + " clip");
            }
            foreach (var b in data.backgrounds)
            {
                var entry = manifest.FindBackground(b.name);
                Assert.That(entry, Is.Not.Null, b.name);
                Assert.That(AssetDatabase.GetAssetPath(entry.sprite), Is.EqualTo(b.texture), b.name);
            }
        }

        [Test]
        public void EveryFramePivotsOnItsGmlOrigin()
        {
            foreach (var s in data.sprites)
            {
                var entry = manifest.FindSprite(s.name);
                foreach (var frame in entry.frames)
                {
                    Assert.That(frame.rect.size, Is.EqualTo(new Vector2(s.width, s.height)), s.name);
                    // Unity's pivot is in pixels from the bottom left; GML's origin is from the top left.
                    Assert.That(frame.pivot.x, Is.EqualTo(s.xorig).Within(1e-3f), s.name);
                    Assert.That(frame.pivot.y, Is.EqualTo(s.height - s.yorig).Within(1e-3f), s.name);
                    Assert.That(frame.pixelsPerUnit, Is.EqualTo(GameClock.PixelsPerUnit), s.name);
                }
            }
        }

        [Test]
        public void ClipsTimeEachFrameAtImageSpeedTimesRoomSpeed()
        {
            // obj_mine / obj_enemy_mine: image_speed = 0.07 in a 120 step room, 6 frames.
            var mine = manifest.FindSprite("spr_mine");
            Assert.That(mine.imageSpeed, Is.EqualTo(0.07f).Within(1e-6f));
            Assert.That(mine.roomSpeed, Is.EqualTo(120f));
            Assert.That(mine.clip.frameRate, Is.EqualTo(8.4f).Within(1e-4f));
            Assert.That(mine.clip.length, Is.EqualTo(6f / 8.4f).Within(1e-4f));
            Assert.That(mine.clip.isLooping, Is.True);
            var keys = AnimationUtility.GetObjectReferenceCurve(mine.clip, GmlArtBuilder.SpriteBinding);
            Assert.That(keys.Select(k => k.value), Is.EqualTo(mine.frames));
            for (int i = 0; i < keys.Length; i++) Assert.That(keys[i].time, Is.EqualTo(i / 8.4f).Within(1e-4f));

            // obj_textdone (room_story, 60 steps a second): image_speed = 0.1.
            var textDone = manifest.FindSprite("spr_textdone");
            Assert.That(textDone.roomSpeed, Is.EqualTo(60f));
            Assert.That(textDone.clip.frameRate, Is.EqualTo(6f).Within(1e-4f));

            // The clip really drives a SpriteRenderer: sampled mid-way through frame 3 it shows frame 3.
            var host = new GameObject("clip host", typeof(SpriteRenderer), typeof(Animator));
            var graph = PlayableGraph.Create("clip test");
            try
            {
                var output = AnimationPlayableOutput.Create(graph, "sprite", host.GetComponent<Animator>());
                var playable = AnimationClipPlayable.Create(graph, mine.clip);
                output.SetSourcePlayable(playable);
                playable.SetTime(3.5 / 8.4);
                graph.Evaluate();
                Assert.That(host.GetComponent<SpriteRenderer>().sprite, Is.SameAs(mine.frames[3]));
                playable.SetTime(6.5 / 8.4); // past the end it loops back to frame 0
                graph.Evaluate();
                Assert.That(host.GetComponent<SpriteRenderer>().sprite, Is.SameAs(mine.frames[0]));
            }
            finally
            {
                graph.Destroy();
                Object.DestroyImmediate(host);
            }

            // A single frame has nothing to animate.
            Assert.That(manifest.FindSprite("spr_rock1").clip, Is.Null);
        }

        [Test]
        public void MasksKeepTheOriginalShapeAndBox()
        {
            // spr_tip_box: rectangle, box (0, 0)-(547, 249), origin (0, 0) at the top left.
            var tip = manifest.FindSprite("spr_tip_box");
            Assert.That(tip.mask, Is.EqualTo(GmlMaskKind.Rectangle));
            var outline = tip.MaskOutline();
            Assert.That(outline[0].x, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(outline[0].y, Is.EqualTo(-2.5f).Within(1e-5f));
            Assert.That(outline[2].x, Is.EqualTo(5.48f).Within(1e-5f));
            Assert.That(outline[2].y, Is.EqualTo(0f).Within(1e-5f));

            Assert.That(manifest.FindSprite("spr_bigexplosion").mask, Is.EqualTo(GmlMaskKind.Ellipse));
            // spr_alertmarker: precise, inside a manual box (0, 23)-(31, 40).
            var alert = manifest.FindSprite("spr_alertmarker");
            Assert.That(alert.mask, Is.EqualTo(GmlMaskKind.Precise));
            Assert.That((alert.bboxLeft, alert.bboxTop, alert.bboxRight, alert.bboxBottom), Is.EqualTo((0, 23, 31, 40)));
            Assert.That(alert.MaskOutline(), Is.Null);

            // A precise mask's shape is the sprite's own alpha, so it is the one kind that keeps a physics shape.
            foreach (var s in data.sprites)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(s.frames[0]);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.That(settings.spriteGenerateFallbackPhysicsShape, Is.EqualTo(s.colkind == 0), s.name);
            }
        }

        [Test]
        public void SpritesUpToTheAtlasLimitArePackedAndLargerOnesAreNot()
        {
            // Atlases pack for player builds only, so membership is read from each atlas's packables.
            var packables = data.sprites.Where(s => !string.IsNullOrEmpty(s.atlas)).Select(s => s.atlas).Distinct()
                .ToDictionary(path => path, path =>
                {
                    var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                    Assert.That(atlas, Is.Not.Null, path);
                    Assert.That(((SpriteAtlasImporter)AssetImporter.GetAtPath(path)).includeInBuild, Is.True, path);
                    return atlas.GetPackables().Select(AssetDatabase.GetAssetPath).ToList();
                });
            foreach (var s in data.sprites)
            {
                bool large = s.width > data.atlasMaxSide || s.height > data.atlasMaxSide;
                Assert.That(string.IsNullOrEmpty(s.atlas), Is.EqualTo(large), s.name);
                if (large) continue;
                foreach (var frame in s.frames) Assert.That(packables[s.atlas], Does.Contain(frame), s.name + " in " + s.atlas);
            }
            Assert.That(manifest.FindSprite("spr_earthball_simple").atlas, Is.Empty);
        }

        [Test]
        public void BackgroundsArePivotedTopLeftAndTileLikeTheOriginal()
        {
            var metal = manifest.FindBackground("bg_metal");
            Assert.That(metal.sprite.rect.size, Is.EqualTo(new Vector2(1920, 1200)));
            Assert.That(metal.sprite.pivot, Is.EqualTo(new Vector2(0f, 1200f)));
            Assert.That(metal.tileHorizontal && metal.tileVertical, Is.True);
            Assert.That(metal.sprite.texture.wrapModeU, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(metal.sprite.texture.wrapModeV, Is.EqualTo(TextureWrapMode.Repeat));
        }

        [Test]
        public void FrameWrapsLikeImageIndex()
        {
            var mine = manifest.FindSprite("spr_mine");
            Assert.That(manifest.Frame("spr_mine", 7), Is.SameAs(mine.frames[1]));
            Assert.That(manifest.Frame("spr_mine", -1), Is.SameAs(mine.frames[5]));
            Assert.That(manifest.Frame("spr_no_such_sprite"), Is.Null);
        }

        [Test]
        public void TheDataFileIsValidatedOnLoad()
        {
            Assert.Throws<InvalidDataException>(() => GmlArtData.Parse("{\"pixelsPerUnit\":0,\"defaultRoomSpeed\":120}"));
            Assert.Throws<InvalidDataException>(() => GmlArtData.Parse(
                "{\"pixelsPerUnit\":100,\"defaultRoomSpeed\":120,\"sprites\":[{\"name\":\"a\",\"width\":1,\"height\":1,\"roomSpeed\":120,\"colkind\":7,\"frames\":[\"Assets/Unity.Rea.Hare/Art/Sprites/a_0.png\"]}]}"));
            Assert.Throws<InvalidDataException>(() => GmlArtData.Parse(
                "{\"pixelsPerUnit\":100,\"defaultRoomSpeed\":120,\"sprites\":[{\"name\":\"a\",\"width\":1,\"height\":1,\"roomSpeed\":120,\"frames\":[\"Assets/Elsewhere/a_0.png\"]}]}"));
        }
    }
}
