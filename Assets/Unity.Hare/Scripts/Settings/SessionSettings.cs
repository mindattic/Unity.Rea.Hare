using UnityEngine;
using Unity.Hare.Audio;
using Unity.Hare.Session;

namespace Unity.Hare
{
    /// <summary>
    /// The globals the player's profile keeps across runs (U.HARE-T60): <c>master_volume</c> and <c>use_metric</c>,
    /// which obj_game's numpad +/- and U key change. globals.gml starts every run (and game_restart) at 1 and imperial;
    /// here a new session starts from the profile's Hare block instead, and the keys save what they changed
    /// (docs/BIBLE.md). Nothing here reads or writes Unity.Shared's common volumes, so the CLI's session mute
    /// (<c>Profiles.SessionMuted</c>) can never be saved through it.
    /// </summary>
    public static class SessionSettings
    {
        /// <summary>Puts the profile's master volume (on the mixer too) and units on a new <paramref name="session"/>.</summary>
        public static void Seed(GameSession session, PlayerId seat)
        {
            if (session == null) return;
            float volume = HareAudio.SavedMasterVolume();
            session.Execute(new SetMasterVolume(seat, volume));
            HareAudio.MasterVolume = volume; // sound_global_volume(master_volume), so the mixer and the session agree
            session.Execute(new SetUnits(seat, SavedUseMetric()));
        }

        /// <summary>The units the current profile saved (<see cref="HareSettings.DefaultUseMetric"/> without one).</summary>
        public static bool SavedUseMetric()
        {
            try
            {
                return ActiveProfile.Hare.UseMetric;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Unity.Hare] No profile units, using imperial: " + e.Message);
                return HareSettings.DefaultUseMetric;
            }
        }

        /// <summary>Writes <paramref name="useMetric"/> into the current profile's Hare block and saves it. False without a profile.</summary>
        public static bool SaveUseMetric(bool useMetric)
        {
            try
            {
                var profile = ActiveProfile.Current;
                new HareSettings(profile.Settings.Game).UseMetric = useMetric;
                profile.SaveSettings();
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Unity.Hare] The units were not saved: " + e.Message);
                return false;
            }
        }
    }
}
