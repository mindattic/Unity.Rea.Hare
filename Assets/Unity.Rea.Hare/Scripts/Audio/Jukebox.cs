using System;
using UnityEngine;

namespace Unity.Rea.Hare.Audio
{
    /// <summary>
    /// obj_jukebox (U.HARE-T29): setup_level makes one in every play room (<c>jukebox = instance_create(0,0,obj_jukebox)</c>),
    /// and its Step runs play_random_music: whenever no music is playing, it asks for the next track
    /// (<see cref="TrackWanted"/>; the arena sends the session's <see cref="Unity.Rea.Hare.Session.PlayRandomMusic"/>, which picks it, and
    /// plays what that chose with <see cref="HareAudio.PlayMusic"/>). Like obj_jukebox it is not persistent: it goes with
    /// its room, while the track itself plays on to its end on the persistent audio host, and the next play room's
    /// jukebox picks again once it has finished. The original's check, <c>audio_is_playing(0)</c> (sound 0 is sfx_whoosh,
    /// which nothing plays), and its commented-out <c>audio_play_sound</c> are fixed defects (docs/BIBLE.md D33; the room-by-room jukebox is V51).
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class Jukebox : MonoBehaviour
    {
        /// <summary>The object's name, after the original's.</summary>
        public const string ObjectName = "obj_jukebox";

        bool silenced;

        /// <summary>Called on a step with no music playing: choose a track and play it. Returns false if it could not.</summary>
        public Func<bool> TrackWanted;

        /// <summary>The room's jukebox under <paramref name="parent"/>, made on first use (setup_level runs per level).</summary>
        public static Jukebox Ensure(Transform parent)
        {
            var existing = parent.GetComponentInChildren<Jukebox>(true);
            if (existing != null) return existing;
            var go = new GameObject(ObjectName);
            go.transform.SetParent(parent, false);
            return go.AddComponent<Jukebox>();
        }

        void Update()
        {
            if (silenced || TrackWanted == null || HareAudio.IsMusicPlaying) return;
            if (!TrackWanted())
            {
                // No music imported (or the track's clip missing): stop asking every frame, as GameMaker would play nothing.
                silenced = true;
                Debug.LogWarning("[Unity.Rea.Hare] obj_jukebox: the chosen music track has no clip, so the jukebox stays quiet.");
            }
        }
    }
}
