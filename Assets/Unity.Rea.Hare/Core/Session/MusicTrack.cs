using System;
using System.Collections.Generic;
using Unity.Rea.Hare.Content;

namespace Unity.Rea.Hare.Session
{
    /// <summary>
    /// One of play_random_music's tracks (U.HARE-T29): the GML sound it plays (<c>current_track</c>) and the name the
    /// debug overlay's "Current Track" line shows (<c>track_name</c>).
    /// </summary>
    [Serializable]
    public class MusicTrack
    {
        /// <summary>GML: current_track, the music sound's name ("music_pinballspring").</summary>
        public string Sound;
        /// <summary>GML: track_name ("Pinball Spring").</summary>
        public string Title;

        public MusicTrack() { }

        public MusicTrack(string sound, string title)
        {
            Sound = sound;
            Title = title;
        }

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Sound", Sound);
            ValidationHelpers.Required(errors, path + ".Title", Title);
        }
    }
}
