using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Unity.Rea.Hare.Input
{
    /// <summary>
    /// The player's rebound controls (U.HARE-T120, a deliberate addition, docs/BIBLE.md V55; the original has no
    /// rebinding): <see cref="HareControls"/>' binding overrides, as the Input System's own
    /// <c>SaveBindingOverridesAsJson</c> document, kept in <c>controls.json</c> in the current profile's Saves folder
    /// (<see cref="ActiveProfile.SavesFolder"/>, beside <c>campaign.json</c>). The file is read once at boot
    /// (<see cref="Boot"/>); every runtime <see cref="HareControls"/> comes from <see cref="Create"/>, which applies
    /// the overrides on top of the asset's defaults (the original's keys), and <see cref="Save"/> writes a new set and
    /// reapplies it to every live instance, so a rebinding made on the title is in force everywhere at once.
    /// Writes go to a temporary file first and replace the old one; a file that cannot be read is moved aside
    /// (<c>controls.json.bad</c>) and the defaults stand.
    /// </summary>
    public static class HareBindings
    {
        public const string FileName = "controls.json";

        /// <summary>A path to use instead of the profile's (tests); null for the profile's.</summary>
        public static string PathOverride;

        /// <summary>The file in use.</summary>
        public static string FilePath => PathOverride ?? Path.Combine(ActiveProfile.SavesFolder, FileName);

        static string overridesJson;
        static bool loaded;
        static readonly List<WeakReference<HareControls>> live = new List<WeakReference<HareControls>>();

        /// <summary>The overrides in force, as JSON (empty when every binding is the default).</summary>
        public static string OverridesJson
        {
            get
            {
                EnsureLoaded();
                return overridesJson ?? "";
            }
        }

        /// <summary>Reads the saved overrides before the first scene loads, so the first controls made already carry them.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            live.Clear();
            loaded = false;
            EnsureLoaded();
        }

        static void EnsureLoaded()
        {
            if (loaded) return;
            loaded = true;
            overridesJson = Read();
        }

        /// <summary>A new <see cref="HareControls"/> with the player's saved overrides applied; kept in step with every later <see cref="Save"/>.</summary>
        public static HareControls Create()
        {
            var controls = new HareControls();
            Apply(controls);
            live.RemoveAll(r => !r.TryGetTarget(out var c) || c.asset == null);
            live.Add(new WeakReference<HareControls>(controls));
            return controls;
        }

        /// <summary>Puts <paramref name="controls"/> back on the defaults and lays the saved overrides over them.</summary>
        public static void Apply(HareControls controls)
        {
            if (controls == null || controls.asset == null) return;
            controls.RemoveAllBindingOverrides();
            string json = OverridesJson;
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                controls.LoadBindingOverridesFromJson(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Unity.Rea.Hare] The saved controls were not applied, using the defaults: " + e.Message);
                controls.RemoveAllBindingOverrides();
            }
        }

        /// <summary>
        /// Takes <paramref name="source"/>'s overrides as the player's, writes them to <see cref="FilePath"/> and applies
        /// them to every live <see cref="HareControls"/>. False when the file could not be written (the overrides still
        /// hold for this run).
        /// </summary>
        public static bool Save(HareControls source)
        {
            if (source == null || source.asset == null) return false;
            EnsureLoaded();
            overridesJson = source.SaveBindingOverridesAsJson();
            foreach (var reference in live)
                if (reference.TryGetTarget(out var controls) && !ReferenceEquals(controls, source)) Apply(controls);
            return Write(overridesJson);
        }

        /// <summary>Drops every override (the original's keys and the stock gamepad layout again) and saves that.</summary>
        public static bool ResetToDefaults(HareControls source)
        {
            if (source == null || source.asset == null) return false;
            source.RemoveAllBindingOverrides();
            return Save(source);
        }

        static string Read()
        {
            string path = null;
            try
            {
                path = FilePath;
                if (!File.Exists(path)) return "";
                string json = File.ReadAllText(path);
                if (!string.IsNullOrWhiteSpace(json)) JsonUtility.FromJson<OverrideDocument>(json); // throws on a file that is not JSON
                return json;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Unity.Rea.Hare] The saved controls were not read, using the defaults: " + e.Message);
                if (path != null && File.Exists(path)) SetAside(path);
                return "";
            }
        }

        static bool Write(string json)
        {
            try
            {
                string path = FilePath;
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                string temp = path + ".tmp";
                File.WriteAllText(temp, json ?? "");
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Unity.Rea.Hare] The controls were not saved: " + e.Message);
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
                Debug.LogWarning("[Unity.Rea.Hare] The unreadable controls file was left in place: " + e.Message);
            }
        }

        /// <summary>The shape of <c>SaveBindingOverridesAsJson</c>'s document, only to check a file parses.</summary>
        [Serializable]
        sealed class OverrideDocument
        {
            public List<OverrideEntry> bindings;
        }

        [Serializable]
        sealed class OverrideEntry
        {
            public string action;
            public string id;
            public string path;
            public string interactions;
            public string processors;
        }
    }
}
