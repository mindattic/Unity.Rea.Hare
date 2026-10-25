using UnityEngine;
using UnityEngine.Audio;
using Unity.Shared.Core;

namespace Unity.Hare.Audio
{
    /// <summary>
    /// The game's sound, by GML sound name, through the AudioMixer the audio pipeline builds (U.HARE-T47):
    /// Master, with Music and Sfx under it. The mixer's exposed volumes follow the player's profile (Unity.Shared's
    /// <see cref="CommonSettings"/> music and sfx volume and mutes) and the session's <see cref="MasterVolume"/>, the
    /// original's <c>master_volume</c> global (1 at start, never saved; obj_game's numpad keys move it, U.HARE-T60).
    /// Every AudioSource the game makes routes through <see cref="Route"/> so all three volumes reach it.
    /// </summary>
    public static class HareAudio
    {
        static float masterVolume = 1f;
        static AudioSource sfxOneShots;
        static AudioSource musicOneShots;

        /// <summary>The project's audio manifest, or null if the audio has not been imported.</summary>
        public static HareAudioManifest Manifest => HareAudioManifest.Load();

        /// <summary>The clip for GML sound <paramref name="name"/>, or null (a missing sound never stops the logic).</summary>
        public static AudioClip Clip(string name) => string.IsNullOrEmpty(name) ? null : Manifest?.Clip(name);

        /// <summary>The mixer group a channel plays through, or null without the manifest.</summary>
        public static AudioMixerGroup Group(SoundChannel channel) => Manifest?.GroupFor(channel);

        /// <summary>Sends <paramref name="source"/> through the channel's mixer group. Returns the source.</summary>
        public static AudioSource Route(AudioSource source, SoundChannel channel = SoundChannel.Sfx)
        {
            if (source != null) source.outputAudioMixerGroup = Group(channel);
            return source;
        }

        /// <summary>
        /// The session's master volume (0-1), the original's <c>master_volume</c>: what <c>sound_global_volume</c>
        /// set. Setting it applies it to the mixer.
        /// </summary>
        public static float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
                SetLevel(HareAudioManifest.MasterVolumeParameter, MixerLevels.ToDecibels(masterVolume));
            }
        }

        /// <summary>
        /// Puts the player's music and sfx volume and mutes, and the session's master volume, on the mixer. Call it
        /// again after the options change them. Returns false if the mixer refused (no manifest, or a parameter
        /// missing).
        /// </summary>
        public static bool ApplyVolumes(CommonSettings common)
        {
            bool ok = SetLevel(HareAudioManifest.MasterVolumeParameter, MixerLevels.ToDecibels(masterVolume));
            ok &= SetLevel(HareAudioManifest.MusicVolumeParameter, MixerLevels.ToDecibels(common.MusicVolume, common.MuteMusic));
            ok &= SetLevel(HareAudioManifest.SfxVolumeParameter, MixerLevels.ToDecibels(common.SfxVolume, common.MuteSfx));
            return ok;
        }

        /// <summary><see cref="ApplyVolumes"/> with the current profile's settings.</summary>
        public static bool ApplyProfile()
        {
            CommonSettings common;
            try
            {
                common = ActiveProfile.Current.Settings.Common;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Unity.Hare] No profile volumes, the mixer stays at full: " + e.Message);
                return false;
            }
            return ApplyVolumes(common);
        }

        static bool SetLevel(string parameter, float decibels)
        {
            var mixer = Manifest?.Mixer;
            return mixer != null && mixer.SetFloat(parameter, decibels);
        }

        /// <summary>
        /// GML <c>audio_play_sound(name, priority, false)</c>: a one-shot through the sound's own channel at its
        /// sound.gmx volume, not positional (GameMaker's is not). Play mode only; an unknown name is silent.
        /// </summary>
        public static void Play(string name)
        {
            if (!Application.isPlaying) return;
            var sound = Manifest?.Find(name);
            if (sound == null || sound.clip == null) return;
            var source = OneShots(sound.channel);
            source.PlayOneShot(sound.clip, sound.volume);
        }

        static AudioSource OneShots(SoundChannel channel)
        {
            ref AudioSource source = ref channel == SoundChannel.Music ? ref musicOneShots : ref sfxOneShots;
            if (source == null)
            {
                var host = HareAudioHost.Ensure();
                source = host.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                Route(source, channel);
            }
            return source;
        }

        /// <summary>Puts the volumes on the mixer once the first scene is up (AudioMixer ignores SetFloat before it).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            masterVolume = 1f; // globals.gml: master_volume = 1
            HareAudioHost.Ensure();
        }
    }

    /// <summary>
    /// The persistent object that owns <see cref="HareAudio"/>'s one-shot sources and applies the profile's volumes
    /// to the mixer on its first Start (an AudioMixer drops SetFloat calls made before then).
    /// </summary>
    [AddComponentMenu("")]
    public sealed class HareAudioHost : MonoBehaviour
    {
        static HareAudioHost instance;

        /// <summary>The host, made (and kept across scenes) on first use.</summary>
        public static HareAudioHost Ensure()
        {
            if (instance != null) return instance;
            var go = new GameObject("HareAudio");
            if (Application.isPlaying) DontDestroyOnLoad(go);
            instance = go.AddComponent<HareAudioHost>();
            return instance;
        }

        void Start() => HareAudio.ApplyProfile();

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
