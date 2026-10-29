using UnityEditor;
using UnityEngine;
using Unity.Hare.Audio;

namespace Unity.Hare.EditorTools
{
    /// <summary>
    /// Import settings for the sounds tools/import-gml-audio.ps1 mirrors into Assets/Unity.Rea.Hare/Audio/Sounds, read from
    /// Audio/gml-audio.json so nothing is set by hand in the Inspector and every .meta is generated. Core's
    /// <see cref="GmlSoundSettings"/> decides each clip's load type and format from its sound.gmx fields (the music
    /// streams as Vorbis; the uncompressed sfx are PCM decompressed on load, as GameMaker played them); the sample rate
    /// is GameMaker's own export rate, GameMaker's mono target forces mono, and its preload box sets
    /// preloadAudioData. Nothing is imported as 3D: GameMaker's audio_play_sound is not positional.
    ///
    /// The import depends on gml-audio.json, so a changed sound.gmx reimports the affected clip.
    /// </summary>
    public class GmlAudioImporter : AssetPostprocessor
    {
        /// <summary>Raise when the settings below change, so Unity reimports the sounds with them.</summary>
        public override uint GetVersion() => 1;

        void OnPreprocessAudio()
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(GmlAudioData.SoundsFolder)) return;
            context.DependsOnSourceAsset(GmlAudioData.DataPath);
            var data = GmlAudioData.Load();
            if (data == null)
            {
                Debug.LogWarning("[Unity.Hare] " + path + " has no " + GmlAudioData.DataPath + "; run tools/import-gml-audio.ps1");
                return;
            }
            var sound = data.SoundForClip(path);
            if (sound == null) Debug.LogWarning("[Unity.Hare] " + path + " is not a sound in " + GmlAudioData.DataPath);
            else Configure((AudioImporter)assetImporter, sound, data.vorbisQuality);
        }

        /// <summary>The default-platform settings a sound imports with.</summary>
        public static AudioImporterSampleSettings SampleSettings(GmlAudioData.SoundData sound, float vorbisQuality)
        {
            var plan = sound.Settings;
            return new AudioImporterSampleSettings
            {
                loadType = plan.Load switch
                {
                    SoundLoad.Streaming => AudioClipLoadType.Streaming,
                    SoundLoad.CompressedInMemory => AudioClipLoadType.CompressedInMemory,
                    _ => AudioClipLoadType.DecompressOnLoad,
                },
                compressionFormat = plan.Format == SoundFormat.Vorbis ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM,
                quality = plan.Format == SoundFormat.Vorbis ? vorbisQuality : 1f,
                sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate,
                sampleRateOverride = (uint)sound.sampleRate,
                preloadAudioData = plan.Preload,
            };
        }

        /// <summary>Applies a sound's settings; true if anything changed.</summary>
        public static bool Configure(AudioImporter importer, GmlAudioData.SoundData sound, float vorbisQuality)
        {
            var want = SampleSettings(sound, vorbisQuality);
            bool mono = sound.Settings.ForceToMono;
            var have = importer.defaultSampleSettings;
            bool changed =
                have.loadType != want.loadType ||
                have.compressionFormat != want.compressionFormat ||
                !Mathf.Approximately(have.quality, want.quality) ||
                have.sampleRateSetting != want.sampleRateSetting ||
                have.sampleRateOverride != want.sampleRateOverride ||
                have.preloadAudioData != want.preloadAudioData ||
                importer.forceToMono != mono ||
                importer.loadInBackground ||
                importer.ambisonic;
            if (!changed) return false;
            importer.defaultSampleSettings = want;
            importer.forceToMono = mono;
            importer.loadInBackground = false;
            importer.ambisonic = false;
            return true;
        }
    }
}
