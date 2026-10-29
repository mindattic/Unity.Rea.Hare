using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.Hare.Audio;

namespace Unity.Hare.EditorTools
{
    /// <summary>
    /// Audio/gml-audio.json, the file tools/import-gml-audio.ps1 writes from HARE.gmx: every GML sound, its sound.gmx
    /// fields, the facts of its source file and the clip it becomes, the original's data files (credits.txt), plus
    /// what was not imported and why. The editor reads it and never writes it; to change the audio, rerun the tool.
    /// </summary>
    [Serializable]
    public sealed class GmlAudioData
    {
        public const string AudioFolder = "Assets/Unity.Rea.Hare/Audio";
        public const string DataPath = AudioFolder + "/gml-audio.json";
        public const string SoundsFolder = AudioFolder + "/Sounds/";
        public const string MixerPath = AudioFolder + "/HareMixer.mixer";

        public string source;
        /// <summary>Unity's Vorbis quality (0-1) for every clip stored as Vorbis.</summary>
        public float vorbisQuality;
        public SoundData[] sounds;
        public DataFileData[] datafiles;
        public SkippedData[] skipped;

        [Serializable]
        public sealed class SoundData
        {
            public string name, group;
            /// <summary>sound.gmx kind (<see cref="GmlSoundKind"/>).</summary>
            public int kind;
            /// <summary>The file under sound/audio and its size in bytes.</summary>
            public string file;
            public long bytes;
            /// <summary>What the file really holds ("wav", "mp3", "ogg"), and whether that is already compressed audio.</summary>
            public string sourceFormat;
            public bool sourceCompressed;
            public float volume, pan;
            public int effects;
            public bool compressed, streamed, uncompressOnLoad;
            /// <summary>GameMaker's target: 0 mono, 1 stereo, 2 3D.</summary>
            public int type;
            public int sampleRate, bitRate, bitDepth, preload;
            /// <summary>From a WAV's own header; 0 for compressed sources.</summary>
            public int sourceChannels, sourceSampleRate, sourceBitsPerSample;
            public float sourceSeconds;
            public string clip;
            public string[] notes;

            /// <summary>How this sound imports and which mixer channel it plays through (Core's rule).</summary>
            public GmlSoundSettings Settings =>
                GmlSoundSettings.From(kind, compressed, streamed, uncompressOnLoad, type, preload, sourceCompressed);
        }

        [Serializable]
        public sealed class DataFileData
        {
            public string name, file, asset;
        }

        [Serializable]
        public sealed class SkippedData
        {
            public string name, kind, reason;
        }

        [NonSerialized] Dictionary<string, SoundData> soundByClip;

        /// <summary>The sound whose clip is at <paramref name="assetPath"/>, or null.</summary>
        public SoundData SoundForClip(string assetPath)
        {
            if (soundByClip == null)
            {
                soundByClip = new Dictionary<string, SoundData>(StringComparer.OrdinalIgnoreCase);
                foreach (var s in sounds) soundByClip[s.clip] = s;
            }
            return soundByClip.TryGetValue(assetPath, out var found) ? found : null;
        }

        static GmlAudioData cached;
        static DateTime cachedStamp;

        /// <summary>The project's gml-audio.json, re-read whenever the file changes; null if the tool has not run.</summary>
        public static GmlAudioData Load()
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

        /// <summary>Parses and validates the file, so a malformed one fails loudly here rather than as missing sound.</summary>
        public static GmlAudioData Parse(string json)
        {
            var data = JsonUtility.FromJson<GmlAudioData>(json);
            if (data == null) throw new InvalidDataException(DataPath + " is not valid JSON");
            if (!(data.vorbisQuality > 0f && data.vorbisQuality <= 1f)) throw new InvalidDataException(DataPath + ": vorbisQuality must be in (0, 1]");
            data.sounds ??= Array.Empty<SoundData>();
            data.datafiles ??= Array.Empty<DataFileData>();
            data.skipped ??= Array.Empty<SkippedData>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var s in data.sounds)
            {
                if (string.IsNullOrEmpty(s.name) || !names.Add(s.name)) throw new InvalidDataException(DataPath + ": sound name missing or repeated: " + s.name);
                if (!Enum.IsDefined(typeof(GmlSoundKind), s.kind)) throw new InvalidDataException(DataPath + ": " + s.name + " has unknown kind " + s.kind);
                if (s.volume < 0f || s.volume > 1f) throw new InvalidDataException(DataPath + ": " + s.name + " volume outside 0-1");
                if (s.pan < -1f || s.pan > 1f) throw new InvalidDataException(DataPath + ": " + s.name + " pan outside -1..1");
                if (s.sampleRate <= 0) throw new InvalidDataException(DataPath + ": " + s.name + " has no sample rate");
                if (s.clip == null || !s.clip.StartsWith(SoundsFolder, StringComparison.Ordinal)) throw new InvalidDataException(DataPath + ": " + s.name + " clip outside " + SoundsFolder + ": " + s.clip);
                if (Path.GetFileNameWithoutExtension(s.clip) != s.name) throw new InvalidDataException(DataPath + ": " + s.name + " clip is not named for its sound: " + s.clip);
            }
            foreach (var d in data.datafiles)
                if (string.IsNullOrEmpty(d.name) || d.asset == null || !d.asset.StartsWith(AudioFolder + "/", StringComparison.Ordinal))
                    throw new InvalidDataException(DataPath + ": data file " + d.name + " outside " + AudioFolder);
            return data;
        }
    }
}
