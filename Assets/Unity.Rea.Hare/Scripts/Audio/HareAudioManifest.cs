using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Unity.Rea.Hare.Audio
{
    /// <summary>One GML sound (sound/*.sound.gmx) as imported.</summary>
    [Serializable]
    public sealed class GmlSoundAsset
    {
        public string name;
        /// <summary>The sound's group in HARE.project.gmx's resource tree ("music", "player", "vulcan"), empty at the top.</summary>
        public string group;
        public AudioClip clip;
        /// <summary>The mixer channel it plays through (music or sfx).</summary>
        public SoundChannel channel;
        /// <summary>sound.gmx volume (0-1) and pan (-1..1), applied on every play.</summary>
        public float volume = 1f, pan;
    }

    /// <summary>
    /// Every GML sound name mapped to its imported AudioClip and mixer channel, plus the AudioMixer itself and the
    /// original's sound credits. Generated, never edited by hand: tools/import-gml-audio.ps1 mirrors the original's
    /// sounds and writes Audio/gml-audio.json, and the editor's GmlAudioBuilder turns that into this asset
    /// (Resources/Hare/HareAudioManifest) along with the mixer (Audio/HareMixer.mixer). Load it with <see cref="Load"/>;
    /// play through <see cref="HareAudio"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Unity.Rea.Hare/Audio Manifest", fileName = "HareAudioManifest")]
    public sealed class HareAudioManifest : ScriptableObject
    {
        public const string ResourcePath = "Hare/HareAudioManifest";

        /// <summary>The mixer's exposed volume parameters, in decibels (see <see cref="MixerLevels"/>).</summary>
        public const string MasterVolumeParameter = "MasterVolume";
        public const string MusicVolumeParameter = "MusicVolume";
        public const string SfxVolumeParameter = "SfxVolume";

        /// <summary>The mixer's groups: Master, with Music and Sfx under it.</summary>
        public const string MasterGroupName = "Master";
        public const string MusicGroupName = "Music";
        public const string SfxGroupName = "Sfx";

        [SerializeField] List<GmlSoundAsset> sounds = new List<GmlSoundAsset>();
        [SerializeField] AudioMixer mixer;
        [SerializeField] AudioMixerGroup musicGroup;
        [SerializeField] AudioMixerGroup sfxGroup;
        [Tooltip("datafiles/credits.txt: the original's sound credits, for the credits screen.")]
        [SerializeField] TextAsset credits;

        Dictionary<string, GmlSoundAsset> index;
        static HareAudioManifest loaded;

        public IReadOnlyList<GmlSoundAsset> Sounds => sounds;
        public AudioMixer Mixer => mixer;
        public AudioMixerGroup MusicGroup => musicGroup;
        public AudioMixerGroup SfxGroup => sfxGroup;
        /// <summary>datafiles/credits.txt, kept beside the clips it credits (Audio/credits.txt).</summary>
        public TextAsset Credits => credits;

        /// <summary>The group a channel plays through.</summary>
        public AudioMixerGroup GroupFor(SoundChannel channel) => channel == SoundChannel.Music ? musicGroup : sfxGroup;

        /// <summary>The project's manifest from Resources, or null if the audio has not been imported.</summary>
        public static HareAudioManifest Load()
        {
            if (loaded == null) loaded = Resources.Load<HareAudioManifest>(ResourcePath);
            return loaded;
        }

        /// <summary>Replaces the whole manifest (the editor's builder only).</summary>
        public void Set(List<GmlSoundAsset> newSounds, AudioMixer newMixer, AudioMixerGroup newMusicGroup,
            AudioMixerGroup newSfxGroup, TextAsset newCredits)
        {
            sounds = newSounds ?? throw new ArgumentNullException(nameof(newSounds));
            mixer = newMixer;
            musicGroup = newMusicGroup;
            sfxGroup = newSfxGroup;
            credits = newCredits;
            index = null;
        }

        /// <summary>The sound named <paramref name="name"/> (a GML sound name, "sfx_explode"), or null.</summary>
        public GmlSoundAsset Find(string name)
        {
            if (index == null)
            {
                index = new Dictionary<string, GmlSoundAsset>(sounds.Count, StringComparer.Ordinal);
                foreach (var s in sounds) index[s.name] = s;
            }
            return name != null && index.TryGetValue(name, out var found) ? found : null;
        }

        /// <summary>The clip for GML sound <paramref name="name"/>, or null.</summary>
        public AudioClip Clip(string name) => Find(name)?.clip;

        void OnEnable() => index = null;
    }
}
