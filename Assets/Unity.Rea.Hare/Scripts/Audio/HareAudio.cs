using UnityEngine;
using UnityEngine.Audio;
using Unity.Shared.Core;

namespace Unity.Rea.Hare.Audio
{
    /// <summary>
    /// The game's sound, by GML sound name, through the AudioMixer the audio pipeline builds (U.HARE-T47):
    /// Master, with Music and Sfx under it. The mixer's exposed volumes follow the player's profile (Unity.Shared's
    /// <see cref="CommonSettings"/> music and sfx volume and mutes) and <see cref="MasterVolume"/>, the original's
    /// <c>master_volume</c> global, which obj_game's numpad keys move (U.HARE-T60) and which is kept in the profile's
    /// Hare block (<see cref="HareSettings.MasterVolume"/>, loaded at boot, written by <see cref="SaveMasterVolume"/>).
    /// While Unity.Shared's session mute (<see cref="Profiles.SessionMuted"/>, the CLI's -muted) is on, every level on
    /// the mixer is silent, and nothing the mute forces is ever saved.
    /// Every AudioSource the game makes routes through <see cref="Route"/> so all three volumes reach it.
    /// </summary>
    public static class HareAudio
    {
        static float masterVolume = 1f;
        static AudioSource sfxOneShots;
        static AudioSource musicOneShots;
        static readonly System.Collections.Generic.Dictionary<string, AudioSource> restartable = new System.Collections.Generic.Dictionary<string, AudioSource>();

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
                SetLevel(HareAudioManifest.MasterVolumeParameter, MasterDecibels);
            }
        }

        /// <summary>
        /// Puts the player's music and sfx volume and mutes, and the session's master volume, on the mixer. Call it
        /// again after the options change them. Returns false if the mixer refused (no manifest, or a parameter
        /// missing).
        /// </summary>
        public static bool ApplyVolumes(CommonSettings common)
        {
            bool ok = SetLevel(HareAudioManifest.MasterVolumeParameter, MasterDecibels);
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
                Debug.LogWarning("[Unity.Rea.Hare] No profile volumes, the mixer stays at full: " + e.Message);
                return false;
            }
            return ApplyVolumes(common);
        }

        /// <summary>The master group's level: <see cref="MasterVolume"/>, or silence while the session is muted.</summary>
        static float MasterDecibels => MixerLevels.ToDecibels(masterVolume, Profiles.SessionMuted);

        /// <summary>The master volume the current profile saved (<see cref="HareSettings.DefaultMasterVolume"/> without one).</summary>
        public static float SavedMasterVolume()
        {
            try
            {
                return ActiveProfile.Hare.MasterVolume;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Unity.Rea.Hare] No profile master volume, using " + HareSettings.DefaultMasterVolume + ": " + e.Message);
                return HareSettings.DefaultMasterVolume;
            }
        }

        /// <summary>
        /// Writes <see cref="MasterVolume"/> into the current profile's Hare block and saves settings.json (U.HARE-T60).
        /// Only the player's stored values are written: the session mute changes none of them (Unity.Shared's
        /// <see cref="CommonSettings"/> reports the mute without storing it, and the master volume is kept here as set).
        /// False if there is no profile to save to.
        /// </summary>
        public static bool SaveMasterVolume()
        {
            try
            {
                var profile = ActiveProfile.Current;
                new HareSettings(profile.Settings.Game).MasterVolume = masterVolume;
                profile.SaveSettings();
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Unity.Rea.Hare] The master volume was not saved: " + e.Message);
                return false;
            }
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

        /// <summary>
        /// GML <c>audio_stop_sound(name); audio_play_sound(name, priority, false)</c>: the sound starts over, cutting off
        /// the instance still playing (the typewriters' sfx_type and sfx_type2, U.HARE-T28). Play mode only.
        /// </summary>
        public static void Restart(string name)
        {
            if (!Application.isPlaying || string.IsNullOrEmpty(name)) return;
            var sound = Manifest?.Find(name);
            if (sound == null || sound.clip == null) return;
            if (!restartable.TryGetValue(name, out var source) || source == null)
            {
                var host = HareAudioHost.Ensure();
                source = host.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                Route(source, sound.channel);
                restartable[name] = source;
            }
            source.Stop();
            source.clip = sound.clip;
            source.volume = sound.volume;
            source.Play();
        }

        /// <summary>
        /// GML <c>audio_play_sound(name, priority, false)</c> for a sound the original also asks
        /// <c>audio_is_playing</c> or <c>audio_stop_sound</c> about (sfx_bounce, sfx_brake, the laser's two, U.HARE-T30):
        /// a one-shot on the sound's own source, so <see cref="IsPlaying"/> and <see cref="Stop"/> can see it. Instances
        /// still overlap, as GameMaker's do. Play mode only; an unknown name is silent.
        /// </summary>
        public static void PlayVoice(string name)
        {
            var source = Voice(name, out var sound);
            if (source != null) source.PlayOneShot(sound.clip, sound.volume);
        }

        /// <summary>GML <c>audio_is_playing(name)</c> for a sound played by <see cref="PlayVoice"/> or <see cref="Restart"/>.</summary>
        public static bool IsPlaying(string name) =>
            !string.IsNullOrEmpty(name) && restartable.TryGetValue(name, out var source) && source != null && source.isPlaying;

        /// <summary>GML <c>audio_stop_sound(name)</c>: every instance <see cref="PlayVoice"/> or <see cref="Restart"/> started.</summary>
        public static void Stop(string name)
        {
            if (!string.IsNullOrEmpty(name) && restartable.TryGetValue(name, out var source) && source != null) source.Stop();
        }

        /// <summary>The sound's own source (made on first use), or null outside Play mode or for an unknown sound.</summary>
        static AudioSource Voice(string name, out GmlSoundAsset sound)
        {
            sound = null;
            if (!Application.isPlaying || string.IsNullOrEmpty(name)) return null;
            sound = Manifest?.Find(name);
            if (sound == null || sound.clip == null) return null;
            if (!restartable.TryGetValue(name, out var source) || source == null)
            {
                var host = HareAudioHost.Ensure();
                source = host.gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                Route(source, sound.channel);
                restartable[name] = source;
            }
            return source;
        }

        static AudioSource music;

        /// <summary>
        /// Whether a music track is playing (U.HARE-T29): what obj_jukebox's play_random_music asks before it picks the
        /// next one. The track plays on the persistent audio host, so it carries on across rooms as GameMaker's did.
        /// </summary>
        public static bool IsMusicPlaying => music != null && music.isPlaying;

        /// <summary>The music sound now playing (or last played), or null.</summary>
        public static string CurrentMusic { get; private set; }

        /// <summary>
        /// GML <c>audio_play_sound(current_track, 1, false)</c>: plays music sound <paramref name="name"/> once through the
        /// Music group at its sound.gmx volume, replacing any track still playing. Play mode only; false (and silent)
        /// when the sound or its clip is missing, so the jukebox can stop asking.
        /// </summary>
        public static bool PlayMusic(string name)
        {
            if (!Application.isPlaying || string.IsNullOrEmpty(name)) return false;
            var sound = Manifest?.Find(name);
            if (sound == null || sound.clip == null) return false;
            if (music == null)
            {
                var host = HareAudioHost.Ensure();
                music = host.gameObject.AddComponent<AudioSource>();
                music.playOnAwake = false;
                music.spatialBlend = 0f;
                music.loop = false; // the third argument, loop, is 0: when it ends the jukebox picks again
                music.priority = 0; // never stolen by a burst of sfx voices
                Route(music, SoundChannel.Music);
            }
            music.Stop();
            music.clip = sound.clip;
            music.volume = sound.volume;
            music.panStereo = sound.pan;
            music.Play();
            CurrentMusic = name;
            return true;
        }

        /// <summary>GML <c>audio_stop_sound(current_track)</c>: stops the music track, if one is playing.</summary>
        public static void StopMusic()
        {
            if (music != null) music.Stop();
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
            masterVolume = SavedMasterVolume(); // globals.gml: master_volume = 1, here the player's saved level (U.HARE-T60)
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
