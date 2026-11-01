using Unity.Shared.Core;
using UnityEngine;

namespace Unity.Rea.Hare
{
    /// <summary>
    /// The player profile this run uses, from the shared MindAttic profile store (the Unity.Shared package,
    /// <c>Profiles</c> in the player's data folder): <c>current.txt</c> names it, its one <c>settings.json</c>
    /// holds the common block (display name, colour, music and SFX volume, mutes, reduce motion) and Hare's own
    /// block (<see cref="Hare"/>: difficulty, metric units, fullscreen), and its <c>Saves</c> folder
    /// (<see cref="SavesFolder"/>) is where a campaign save belongs (U.HARE-T65).
    /// </summary>
    public static class ActiveProfile
    {
        /// <summary>
        /// The store in use. Unity.Shared's own <c>ProfilesBootstrap</c> roots it at
        /// <see cref="Application.persistentDataPath"/> before the first scene loads (and on every editor script
        /// reload); this sets it the same way for code that runs ahead of that (tests, tools).
        /// </summary>
        public static ProfileStore Store
        {
            get
            {
                if (!Profiles.IsConfigured) Profiles.UseDataPath(Application.persistentDataPath);
                return Profiles.Store;
            }
        }

        /// <summary>The profile in use.</summary>
        public static Profile Current => Store.Current;

        /// <summary>Hare's own settings block of the current profile's settings.json.</summary>
        public static HareSettings Hare => new HareSettings(Current.Settings.Game);

        /// <summary>Where the current profile's campaign save belongs (made if it does not exist yet).</summary>
        public static string SavesFolder => Current.EnsureSavesFolder();
    }
}
