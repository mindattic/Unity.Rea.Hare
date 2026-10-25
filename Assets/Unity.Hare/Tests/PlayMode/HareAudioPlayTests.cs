using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Hare.Audio;
using Unity.Shared.Core;
using Object = UnityEngine.Object;

namespace Unity.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T47: the mixer actually takes the profile's volumes once Unity's audio is running (an AudioMixer only
    /// accepts SetFloat in play), and a GML sound plays through its channel's group.
    /// </summary>
    public class HareAudioPlayTests
    {
        string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "UnityHareAudioTest_" + Guid.NewGuid().ToString("N"));
            Profiles.UseRoot(root);
        }

        [TearDown]
        public void TearDown()
        {
            HareAudio.MasterVolume = 1f;
            var mixer = HareAudio.Manifest.Mixer;
            mixer.ClearFloat(HareAudioManifest.MasterVolumeParameter);
            mixer.ClearFloat(HareAudioManifest.MusicVolumeParameter);
            mixer.ClearFloat(HareAudioManifest.SfxVolumeParameter);
            Profiles.Reset();
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }

        static float Level(string parameter)
        {
            Assert.IsTrue(HareAudio.Manifest.Mixer.GetFloat(parameter, out float db), parameter + " is exposed");
            return db;
        }

        [UnityTest]
        public IEnumerator TheMixerFollowsTheProfileAndTheSessionMasterVolume()
        {
            yield return null; // the mixer drops SetFloat before the first frame

            var common = ActiveProfile.Current.Settings.Common;
            common.MusicVolume = 0.5f;
            common.SfxVolume = 0.25f;
            Assert.IsTrue(HareAudio.ApplyProfile());
            Assert.That(Level(HareAudioManifest.MusicVolumeParameter), Is.EqualTo(MixerLevels.ToDecibels(0.5f)).Within(1e-3f));
            Assert.That(Level(HareAudioManifest.SfxVolumeParameter), Is.EqualTo(MixerLevels.ToDecibels(0.25f)).Within(1e-3f));
            Assert.That(Level(HareAudioManifest.MasterVolumeParameter), Is.EqualTo(0f).Within(1e-3f), "master_volume starts at 1");

            common.MuteSfx = true;
            HareAudio.ApplyProfile();
            Assert.That(Level(HareAudioManifest.SfxVolumeParameter), Is.EqualTo(MixerLevels.SilentDecibels).Within(1e-3f));
            Assert.That(Level(HareAudioManifest.MusicVolumeParameter), Is.EqualTo(MixerLevels.ToDecibels(0.5f)).Within(1e-3f));

            HareAudio.MasterVolume = 0.1f;
            Assert.That(Level(HareAudioManifest.MasterVolumeParameter), Is.EqualTo(-20f).Within(1e-3f));
            HareAudio.MasterVolume = 0f;
            Assert.That(Level(HareAudioManifest.MasterVolumeParameter), Is.EqualTo(MixerLevels.SilentDecibels).Within(1e-3f));
        }

        [UnityTest]
        public IEnumerator AGmlSoundPlaysThroughItsChannelsGroup()
        {
            yield return null;
            HareAudio.Play("sfx_explode");
            var host = Object.FindFirstObjectByType<HareAudioHost>();
            Assert.IsNotNull(host, "HareAudio keeps a persistent host for its one-shot sources");
            var source = host.GetComponent<AudioSource>();
            Assert.IsNotNull(source);
            Assert.That(source.outputAudioMixerGroup, Is.SameAs(HareAudio.Manifest.SfxGroup));
            Assert.That(source.spatialBlend, Is.EqualTo(0f), "GameMaker's audio_play_sound is not positional");
            HareAudio.Play("sfx_no_such_sound"); // silent, not an error
        }
    }
}
