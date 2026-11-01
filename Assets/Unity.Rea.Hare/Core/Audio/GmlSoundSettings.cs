using System;

namespace Unity.Rea.Hare.Audio
{
    /// <summary>A sound.gmx <c>kind</c>: how GameMaker: Studio 1.x plays the sound.</summary>
    public enum GmlSoundKind
    {
        /// <summary>A normal sound effect.</summary>
        Normal = 0,
        /// <summary>Background music.</summary>
        BackgroundMusic = 1,
        /// <summary>A 3D (positional) sound.</summary>
        ThreeD = 2,
        /// <summary>Played through the multimedia player: GameMaker's choice for long, streamed music.</summary>
        MultimediaPlayer = 3,
    }

    /// <summary>The mixer channel a sound plays through; the master channel carries both.</summary>
    public enum SoundChannel
    {
        Sfx = 0,
        Music = 1,
    }

    /// <summary>How the clip's audio data is held at run time (mirrors Unity's AudioClipLoadType).</summary>
    public enum SoundLoad
    {
        DecompressOnLoad = 0,
        CompressedInMemory = 1,
        Streaming = 2,
    }

    /// <summary>The format the clip is stored in (the subset of Unity's AudioCompressionFormat the import uses).</summary>
    public enum SoundFormat
    {
        Pcm = 0,
        Vorbis = 1,
    }

    /// <summary>
    /// What a GML sound resource becomes in Unity, decided from its sound.gmx fields alone so the rule is testable
    /// outside Unity (U.HARE-T47, BIBLE §6):
    ///
    /// <list type="bullet">
    /// <item>Music (kind background music or multimedia player) plays through the music channel, everything else
    /// through the sfx channel.</item>
    /// <item><c>streamed</c> streams from disk as Vorbis, the way GameMaker streamed the six music tracks.</item>
    /// <item><c>compressed</c> without <c>streamed</c> stays Vorbis in memory, or is decompressed when it loads if
    /// <c>uncompressOnLoad</c> is set.</item>
    /// <item>An uncompressed sound (every sfx_* in HARE.gmx) is PCM, decompressed when it loads: exactly the
    /// latency and quality GameMaker gave it. The one exception is a source file that is itself already compressed
    /// (sfx_whoosh.wav is really an MP3): "uncompressed" in GameMaker meant "store the WAV as it is", never
    /// "inflate an MP3 to PCM", so it stays Vorbis in memory.</item>
    /// <item>GameMaker's target type 0 is mono, so those clips are forced to mono (sfx_hurt8, sfx_vulcan_spindown).</item>
    /// <item><c>preload</c> is GameMaker's "preload" box (-1 checked).</item>
    /// </list>
    /// </summary>
    public readonly struct GmlSoundSettings
    {
        public readonly SoundChannel Channel;
        public readonly SoundLoad Load;
        public readonly SoundFormat Format;
        public readonly bool ForceToMono;
        public readonly bool Preload;

        public GmlSoundSettings(SoundChannel channel, SoundLoad load, SoundFormat format, bool forceToMono, bool preload)
        {
            Channel = channel; Load = load; Format = format; ForceToMono = forceToMono; Preload = preload;
        }

        /// <summary>A sound.gmx <c>types/type</c> of 0: GameMaker's mono target.</summary>
        public const int MonoType = 0;

        /// <summary>The settings for a sound.gmx's fields.</summary>
        /// <param name="sourceCompressed">The source file is already compressed audio (MP3, Ogg), not a PCM WAV.</param>
        public static GmlSoundSettings From(int kind, bool compressed, bool streamed, bool uncompressOnLoad, int type, int preload,
            bool sourceCompressed = false)
        {
            if (!Enum.IsDefined(typeof(GmlSoundKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown sound.gmx kind");
            var k = (GmlSoundKind)kind;
            var channel = k == GmlSoundKind.BackgroundMusic || k == GmlSoundKind.MultimediaPlayer ? SoundChannel.Music : SoundChannel.Sfx;
            SoundLoad load;
            SoundFormat format;
            if (streamed) { load = SoundLoad.Streaming; format = SoundFormat.Vorbis; }
            else if (compressed || sourceCompressed) { load = uncompressOnLoad ? SoundLoad.DecompressOnLoad : SoundLoad.CompressedInMemory; format = SoundFormat.Vorbis; }
            else { load = SoundLoad.DecompressOnLoad; format = SoundFormat.Pcm; }
            return new GmlSoundSettings(channel, load, format, type == MonoType, preload != 0);
        }
    }

    /// <summary>
    /// Volume as the mixer wants it. A player's 0-1 volume (Unity.Shared's music and sfx volumes, the original's
    /// <c>master_volume</c>) is linear gain, which <c>sound_global_volume</c> also took; an AudioMixer attenuates in
    /// decibels, so the exposed parameters get <c>20 log10(volume)</c>, floored at the mixer's -80 dB (silence).
    /// </summary>
    public static class MixerLevels
    {
        /// <summary>The quietest an AudioMixer group goes: silence.</summary>
        public const float SilentDecibels = -80f;

        /// <summary>The decibels for a linear 0-1 volume (clamped), or silence when muted or at 0.</summary>
        public static float ToDecibels(float volume, bool muted = false)
        {
            if (muted || float.IsNaN(volume) || volume <= 0f) return SilentDecibels;
            if (volume >= 1f) return 0f;
            float db = 20f * (float)Math.Log10(volume);
            return db < SilentDecibels ? SilentDecibels : db;
        }

        /// <summary>The linear 0-1 volume for a mixer level in decibels (the inverse of <see cref="ToDecibels"/>).</summary>
        public static float ToVolume(float decibels)
        {
            if (decibels <= SilentDecibels) return 0f;
            if (decibels >= 0f) return 1f;
            return (float)Math.Pow(10.0, decibels / 20.0);
        }
    }
}
