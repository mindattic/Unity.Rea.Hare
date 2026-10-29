using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Unity.Hare.Audio;
using Unity.Hare.EditorTools;

namespace Unity.Hare.Tests.EditMode.Audio
{
    /// <summary>
    /// The imported sound (U.HARE-T47): what tools/import-gml-audio.ps1 and GmlAudioBuilder produced from HARE.gmx is
    /// all there, imported with the load type its sound.gmx asks for, routed through the mixer's music or sfx group,
    /// and named in the runtime manifest by its GML sound name; the credits data travels with it.
    /// </summary>
    public class GmlAudioPipelineTests
    {
        GmlAudioData data;
        HareAudioManifest manifest;

        [SetUp]
        public void SetUp()
        {
            data = GmlAudioData.Load();
            Assert.That(data, Is.Not.Null, GmlAudioData.DataPath + " is missing; run tools/import-gml-audio.ps1");
            manifest = HareAudioManifest.Load();
            Assert.That(manifest, Is.Not.Null, "Resources/" + HareAudioManifest.ResourcePath + " is missing; run GmlAudioBuilder.BuildAll");
        }

        [Test]
        public void EverySoundOfTheOriginalIsImported()
        {
            // HARE.project.gmx lists 75 sounds: 69 sfx_* and 6 music_*. Only the two album-art JPEGs in sound/audio,
            // which no sound uses, are left out.
            Assert.That(data.sounds, Has.Length.EqualTo(75));
            Assert.That(data.sounds.Count(s => s.name.StartsWith("music_")), Is.EqualTo(6));
            Assert.That(data.sounds.Count(s => s.name.StartsWith("sfx_")), Is.EqualTo(69));
            Assert.That(data.skipped.Select(s => s.name), Is.EquivalentTo(new[] { "AlbumArtSmall.jpg", "Folder.jpg" }));
            Assert.That(data.skipped.All(s => s.kind == "file"), string.Join("\n", data.skipped.Select(s => s.name + ": " + s.reason)));
        }

        [Test]
        public void TheFamiliesTheGameCallsByNameAreAllThere()
        {
            var names = new[]
            {
                "sfx_vulcan_spinup", "sfx_vulcan_loop", "sfx_vulcan_spindown", "sfx_empty_clip",
                "sfx_reload_half", "sfx_reload_full", "sfx_switch", "sfx_switch_back",
                "sfx_death", "sfx_explode", "sfx_beep", "sfx_blipgun", "sfx_flyover", "sfx_hawk",
                "music_pinballspring", "music_inaheartbeat", "music_latinindustries", "music_overcast", "music_presenterator", "music_wallpaper",
            }
            .Concat(Enumerable.Range(1, 6).Select(i => "sfx_fuse" + i))
            .Concat(Enumerable.Range(1, 8).Select(i => "sfx_hurt" + i));
            foreach (var name in names)
                Assert.That(manifest.Clip(name), Is.Not.Null, name);
            Assert.That(manifest.Clip("sfx_not_a_sound"), Is.Null);
        }

        [Test]
        public void TheManifestMapsEveryGmlNameToItsClipAndChannel()
        {
            Assert.That(manifest.Sounds.Count, Is.EqualTo(data.sounds.Length));
            foreach (var s in data.sounds)
            {
                var entry = manifest.Find(s.name);
                Assert.That(entry, Is.Not.Null, s.name);
                Assert.That(AssetDatabase.GetAssetPath(entry.clip), Is.EqualTo(s.clip), s.name);
                Assert.That(entry.group, Is.EqualTo(s.group), s.name);
                Assert.That(entry.volume, Is.EqualTo(s.volume), s.name);
                bool music = s.name.StartsWith("music_");
                Assert.That(entry.channel, Is.EqualTo(music ? SoundChannel.Music : SoundChannel.Sfx), s.name);
                Assert.That(entry.group == "music", Is.EqualTo(music), s.name + " sits in the author's music group exactly when it is music");
            }
        }

        [Test]
        public void ClipsImportWithTheLoadTypeTheirSoundGmxAsksFor()
        {
            foreach (var s in data.sounds)
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(s.clip);
                var settings = importer.defaultSampleSettings;
                var clip = manifest.Clip(s.name);
                if (s.name.StartsWith("music_"))
                {
                    // compressed + streamed in GameMaker: streamed Vorbis here.
                    Assert.That(settings.loadType, Is.EqualTo(AudioClipLoadType.Streaming), s.name);
                    Assert.That(settings.compressionFormat, Is.EqualTo(AudioCompressionFormat.Vorbis), s.name);
                    Assert.That(settings.quality, Is.EqualTo(data.vorbisQuality).Within(1e-4f), s.name);
                }
                else if (s.name == "sfx_whoosh")
                {
                    // sfx_whoosh.wav is really an MP3: kept compressed rather than inflated to PCM.
                    Assert.That(s.clip, Does.EndWith("/sfx_whoosh.mp3"));
                    Assert.That(settings.loadType, Is.EqualTo(AudioClipLoadType.CompressedInMemory), s.name);
                    Assert.That(settings.compressionFormat, Is.EqualTo(AudioCompressionFormat.Vorbis), s.name);
                }
                else
                {
                    // Uncompressed in GameMaker: PCM, decompressed on load, as GameMaker played it.
                    Assert.That(settings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad), s.name);
                    Assert.That(settings.compressionFormat, Is.EqualTo(AudioCompressionFormat.PCM), s.name);
                }
                Assert.That(clip.loadType, Is.EqualTo(settings.loadType), s.name + " imported clip");
                Assert.That(settings.sampleRateSetting, Is.EqualTo(AudioSampleRateSetting.OverrideSampleRate), s.name);
                Assert.That(settings.sampleRateOverride, Is.EqualTo(44100u), s.name);
                Assert.That(settings.preloadAudioData, Is.True, s.name + ": every sound.gmx has preload checked");
                bool mono = s.name == "sfx_hurt8" || s.name == "sfx_vulcan_spindown"; // GameMaker's two mono targets
                Assert.That(importer.forceToMono, Is.EqualTo(mono), s.name);
                if (mono) Assert.That(clip.channels, Is.EqualTo(1), s.name);
                Assert.That(clip.length, Is.GreaterThan(0f), s.name + " decoded");
            }
        }

        [Test]
        public void TheWeaponSoundsTheMainSceneUsesKeptTheirGuids()
        {
            // Main.unity's vulcan and ammo components reference these clips by GUID from before the import moved them.
            Assert.That(AssetDatabase.GUIDToAssetPath("af0278de46f4ae645b7e9c1c8ee80df1"),
                Is.EqualTo("Assets/Unity.Rea.Hare/Audio/Sounds/vulcan/sfx_vulcan_loop.wav"));
            foreach (var name in new[] { "sfx_vulcan_spinup", "sfx_vulcan_spindown", "sfx_empty_clip", "sfx_reload_half", "sfx_reload_full" })
                Assert.That(manifest.Clip(name), Is.Not.Null, name);
        }

        [Test]
        public void TheMixerIsMasterWithMusicAndSfxUnderIt_EachVolumeExposed()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(GmlAudioData.MixerPath);
            Assert.That(mixer, Is.Not.Null, GmlAudioData.MixerPath);
            Assert.That(manifest.Mixer, Is.SameAs(mixer));
            var music = mixer.FindMatchingGroups("Master/Music").Single();
            var sfx = mixer.FindMatchingGroups("Master/Sfx").Single();
            Assert.That(manifest.MusicGroup, Is.SameAs(music));
            Assert.That(manifest.SfxGroup, Is.SameAs(sfx));
            Assert.That(manifest.GroupFor(SoundChannel.Music), Is.SameAs(music));
            Assert.That(manifest.GroupFor(SoundChannel.Sfx), Is.SameAs(sfx));
            Assert.That(mixer.FindMatchingGroups("Master"), Has.Length.EqualTo(3), "Master, Music and Sfx only");

            var authoring = new GmlAudioBuilder.MixerAuthoring(mixer);
            Assert.That(authoring.ExposedNames, Is.EquivalentTo(new[]
            {
                HareAudioManifest.MasterVolumeParameter, HareAudioManifest.MusicVolumeParameter, HareAudioManifest.SfxVolumeParameter,
            }));
        }

        [Test]
        public void RebuildingTheMixerChangesNothing()
        {
            var before = System.IO.File.ReadAllText(GmlAudioData.MixerPath);
            var mixer = GmlAudioBuilder.BuildMixer();
            Assert.That(AssetDatabase.GetAssetPath(mixer), Is.EqualTo(GmlAudioData.MixerPath));
            Assert.That(System.IO.File.ReadAllText(GmlAudioData.MixerPath), Is.EqualTo(before));
        }

        [Test]
        public void HareAudioRoutesSourcesThroughTheChannelsGroup()
        {
            var go = new GameObject("source");
            try
            {
                var sfx = HareAudio.Route(go.AddComponent<AudioSource>());
                Assert.That(sfx.outputAudioMixerGroup, Is.SameAs(manifest.SfxGroup));
                var music = HareAudio.Route(go.AddComponent<AudioSource>(), SoundChannel.Music);
                Assert.That(music.outputAudioMixerGroup, Is.SameAs(manifest.MusicGroup));
                Assert.That(HareAudio.Clip("sfx_explode"), Is.SameAs(manifest.Clip("sfx_explode")));
                Assert.That(HareAudio.Clip(null), Is.Null);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheCreditsDataTravelsWithTheClips()
        {
            Assert.That(data.datafiles.Select(d => d.asset), Is.EquivalentTo(new[] { "Assets/Unity.Rea.Hare/Audio/credits.txt" }));
            Assert.That(manifest.Credits, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(manifest.Credits), Is.EqualTo("Assets/Unity.Rea.Hare/Audio/credits.txt"));
            Assert.That(manifest.Credits.text, Does.Contain("Jim Rogers - Mingun sound effect"));
            Assert.That(manifest.Credits.text, Does.Contain("http://soundbible.com/1920-Minigun.html"));
        }
    }
}
