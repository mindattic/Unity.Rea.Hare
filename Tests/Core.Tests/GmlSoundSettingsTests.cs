using System;
using NUnit.Framework;
using Unity.Rea.Hare.Audio;

namespace MindAttic.Hare.Tests
{
    /// <summary>How a GML sound resource is imported, and the mixer's volume curve (U.HARE-T47).</summary>
    public class GmlSoundSettingsTests
    {
        [Test]
        public void StreamedMusic_StreamsAsVorbisThroughTheMusicChannel()
        {
            // Every music_*.sound.gmx: kind 3 (multimedia player), compressed 1, streamed 1, stereo, preload -1.
            var s = GmlSoundSettings.From(kind: 3, compressed: true, streamed: true, uncompressOnLoad: false, type: 1, preload: -1);
            Assert.That(s.Channel, Is.EqualTo(SoundChannel.Music));
            Assert.That(s.Load, Is.EqualTo(SoundLoad.Streaming));
            Assert.That(s.Format, Is.EqualTo(SoundFormat.Vorbis));
            Assert.That(s.ForceToMono, Is.False);
            Assert.That(s.Preload, Is.True);
        }

        [Test]
        public void UncompressedSfx_IsPcmDecompressedOnLoadThroughTheSfxChannel()
        {
            // Every sfx_*.sound.gmx: kind 0, compressed 0, streamed 0.
            var s = GmlSoundSettings.From(0, false, false, false, 1, -1);
            Assert.That(s.Channel, Is.EqualTo(SoundChannel.Sfx));
            Assert.That(s.Load, Is.EqualTo(SoundLoad.DecompressOnLoad));
            Assert.That(s.Format, Is.EqualTo(SoundFormat.Pcm));
        }

        [Test]
        public void CompressedNotStreamed_StaysCompressedUnlessUncompressOnLoad()
        {
            var inMemory = GmlSoundSettings.From(0, true, false, false, 1, -1);
            Assert.That(inMemory.Load, Is.EqualTo(SoundLoad.CompressedInMemory));
            Assert.That(inMemory.Format, Is.EqualTo(SoundFormat.Vorbis));
            var decompressed = GmlSoundSettings.From(0, true, false, true, 1, -1);
            Assert.That(decompressed.Load, Is.EqualTo(SoundLoad.DecompressOnLoad));
            Assert.That(decompressed.Format, Is.EqualTo(SoundFormat.Vorbis));
        }

        [Test]
        public void AnUncompressedSoundWhoseSourceIsAlreadyMp3_StaysCompressedInMemory()
        {
            // sfx_whoosh: compressed 0, streamed 0, but sfx_whoosh.wav is a 320 kbps MP3.
            var s = GmlSoundSettings.From(0, false, false, false, 1, -1, sourceCompressed: true);
            Assert.That(s.Load, Is.EqualTo(SoundLoad.CompressedInMemory));
            Assert.That(s.Format, Is.EqualTo(SoundFormat.Vorbis));
            Assert.That(s.Channel, Is.EqualTo(SoundChannel.Sfx));
        }

        [Test]
        public void BackgroundMusicKind_AlsoPlaysThroughMusic_And3DThroughSfx()
        {
            Assert.That(GmlSoundSettings.From(1, false, false, false, 1, -1).Channel, Is.EqualTo(SoundChannel.Music));
            Assert.That(GmlSoundSettings.From(2, false, false, false, 1, -1).Channel, Is.EqualTo(SoundChannel.Sfx));
        }

        [Test]
        public void TypeZero_IsGameMakersMonoTarget_AndPreloadZeroIsOff()
        {
            // sfx_hurt8 and sfx_vulcan_spindown are the two mono targets in HARE.gmx.
            Assert.That(GmlSoundSettings.From(0, false, false, false, 0, -1).ForceToMono, Is.True);
            Assert.That(GmlSoundSettings.From(0, false, false, false, 1, 0).Preload, Is.False);
        }

        [Test]
        public void UnknownKind_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GmlSoundSettings.From(4, false, false, false, 1, -1));
        }

        [Test]
        public void ToDecibels_IsTwentyLogTen_FlooredAtSilence()
        {
            Assert.That(MixerLevels.ToDecibels(1f), Is.EqualTo(0f));
            Assert.That(MixerLevels.ToDecibels(0.5f), Is.EqualTo(-6.0206f).Within(1e-3f));
            Assert.That(MixerLevels.ToDecibels(0.1f), Is.EqualTo(-20f).Within(1e-3f));
            Assert.That(MixerLevels.ToDecibels(0f), Is.EqualTo(MixerLevels.SilentDecibels));
            Assert.That(MixerLevels.ToDecibels(1e-6f), Is.EqualTo(MixerLevels.SilentDecibels), "below -80 dB floors");
            Assert.That(MixerLevels.ToDecibels(2f), Is.EqualTo(0f), "clamped to unity gain");
            Assert.That(MixerLevels.ToDecibels(float.NaN), Is.EqualTo(MixerLevels.SilentDecibels));
            Assert.That(MixerLevels.ToDecibels(0.8f, muted: true), Is.EqualTo(MixerLevels.SilentDecibels));
        }

        [Test]
        public void ToVolume_InvertsToDecibels()
        {
            foreach (var v in new[] { 0.05f, 0.25f, 0.6f, 0.85f, 1f })
                Assert.That(MixerLevels.ToVolume(MixerLevels.ToDecibels(v)), Is.EqualTo(v).Within(1e-4f), v.ToString());
            Assert.That(MixerLevels.ToVolume(MixerLevels.SilentDecibels), Is.EqualTo(0f));
        }
    }
}
