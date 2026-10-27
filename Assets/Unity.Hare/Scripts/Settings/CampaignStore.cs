using System;
using System.IO;
using UnityEngine;
using Unity.Hare.Session;

namespace Unity.Hare
{
    /// <summary>
    /// Where the map's won missions are kept (U.HARE-T19, owner-directed, docs/BIBLE.md V42): <c>campaign.json</c> in
    /// the current profile's Saves folder (<see cref="ActiveProfile.SavesFolder"/>), Core's
    /// <see cref="CampaignProgress"/> document. Writes go to a temporary file first and replace the old one, so a crash
    /// mid-write never leaves half a file; a file that cannot be read is moved aside (<c>campaign.json.bad</c>) and the
    /// campaign starts with nothing won. U.HARE-T65 folds this into the full campaign save.
    /// </summary>
    public static class CampaignStore
    {
        public const string FileName = "campaign.json";

        /// <summary>A path to use instead of the profile's (tests); null for the profile's.</summary>
        public static string PathOverride;

        /// <summary>The file in use.</summary>
        public static string FilePath => PathOverride ?? Path.Combine(ActiveProfile.SavesFolder, FileName);

        /// <summary>The saved progress, or an empty one (no file, no profile, or a file that is not a campaign document).</summary>
        public static CampaignProgress Load()
        {
            string path = null;
            try
            {
                path = FilePath;
                if (!File.Exists(path)) return new CampaignProgress();
                return CampaignProgress.FromJson(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Unity.Hare] The campaign's won missions were not read, starting with none: " + e.Message);
                if (path != null && e is FormatException) SetAside(path);
                return new CampaignProgress();
            }
        }

        /// <summary>Writes <paramref name="progress"/> safely. False when it could not be written.</summary>
        public static bool Save(CampaignProgress progress)
        {
            if (progress == null) return false;
            try
            {
                string path = FilePath;
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                string temp = path + ".tmp";
                File.WriteAllText(temp, progress.ToJson());
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Unity.Hare] The campaign's won missions were not saved: " + e.Message);
                return false;
            }
        }

        static void SetAside(string path)
        {
            try
            {
                string bad = path + ".bad";
                if (File.Exists(bad)) File.Delete(bad);
                File.Move(path, bad);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Unity.Hare] The unreadable campaign file was left in place: " + e.Message);
            }
        }
    }
}
