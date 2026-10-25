using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Unity.Hare.Audio;

namespace Unity.Hare.EditorTools
{
    /// <summary>
    /// Turns Audio/gml-audio.json (written by tools/import-gml-audio.ps1) into the Unity side of the original's sound:
    /// makes sure every clip carries <see cref="GmlAudioImporter"/>'s settings, builds the AudioMixer
    /// (Audio/HareMixer.mixer: Master, with Music and Sfx under it, each group's volume exposed as MasterVolume,
    /// MusicVolume and SfxVolume) and writes the runtime <see cref="HareAudioManifest"/>
    /// (Resources/Hare/HareAudioManifest) mapping every GML sound name to its clip and channel, with the mixer and the
    /// original's credits. Rerunning it on the same data changes nothing; an existing mixer keeps its file and GUIDs
    /// and only gains whatever group or parameter is missing.
    ///
    /// Unity has no public API for authoring an AudioMixer, so <see cref="MixerAuthoring"/> drives the editor's own
    /// AudioMixerController (the same calls the Audio Mixer window makes) by reflection, pinned to this project's
    /// Unity version and checked by the EditMode tests.
    ///
    /// Batch: tools/unity.ps1 -Method Unity.Hare.EditorTools.GmlAudioBuilder.BuildAll (tools/import-gml-audio.ps1 runs it).
    /// </summary>
    public static class GmlAudioBuilder
    {
        public const string ManifestPath = "Assets/Unity.Hare/Resources/" + HareAudioManifest.ResourcePath + ".asset";
        const string CreditsName = "credits.txt";

        [MenuItem("Unity.Hare/Build Audio From gml-audio.json")]
        public static void BuildAll()
        {
            var data = GmlAudioData.Load() ?? throw new FileNotFoundException(
                GmlAudioData.DataPath + " is missing; run tools/import-gml-audio.ps1 first", GmlAudioData.DataPath);

            AssetDatabase.Refresh();
            int reimported = EnsureImportSettings(data);
            var mixer = BuildMixer();
            BuildManifest(data, mixer);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Unity.Hare] GML audio built: {data.sounds.Length} sounds, {data.datafiles.Length} data files, " +
                      $"{reimported} clips reimported, {data.skipped.Length} skipped");
            foreach (var s in data.skipped) Debug.Log($"[Unity.Hare] Not imported: {s.kind} {s.name}: {s.reason}");
        }

        /// <summary>Reimports any clip whose settings are not yet the importer's (e.g. imported before it compiled).</summary>
        static int EnsureImportSettings(GmlAudioData data)
        {
            var stale = new List<string>();
            foreach (var s in data.sounds)
            {
                var importer = AssetImporter.GetAtPath(s.clip) as AudioImporter
                    ?? throw new FileNotFoundException(s.clip + " is in " + GmlAudioData.DataPath + " but not imported; rerun tools/import-gml-audio.ps1", s.clip);
                if (GmlAudioImporter.Configure(importer, s, data.vorbisQuality)) stale.Add(s.clip);
            }
            if (stale.Count == 0) return 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in stale)
                {
                    AssetDatabase.WriteImportSettingsIfDirty(path);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            return stale.Count;
        }

        /// <summary>The mixer at <see cref="GmlAudioData.MixerPath"/>, made or completed: Master > Music, Sfx, volumes exposed.</summary>
        public static AudioMixer BuildMixer()
        {
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(GmlAudioData.MixerPath);
            var authoring = mixer == null ? MixerAuthoring.Create(GmlAudioData.MixerPath) : new MixerAuthoring(mixer);
            var master = authoring.MasterGroup;
            authoring.EnsureChild(master, HareAudioManifest.MusicGroupName);
            authoring.EnsureChild(master, HareAudioManifest.SfxGroupName);
            authoring.ExposeVolume(master, HareAudioManifest.MasterVolumeParameter);
            authoring.ExposeVolume(authoring.FindChild(master, HareAudioManifest.MusicGroupName), HareAudioManifest.MusicVolumeParameter);
            authoring.ExposeVolume(authoring.FindChild(master, HareAudioManifest.SfxGroupName), HareAudioManifest.SfxVolumeParameter);
            authoring.Save();
            return authoring.Mixer;
        }

        static void BuildManifest(GmlAudioData data, AudioMixer mixer)
        {
            var music = mixer.FindMatchingGroups(HareAudioManifest.MasterGroupName + "/" + HareAudioManifest.MusicGroupName).FirstOrDefault();
            var sfx = mixer.FindMatchingGroups(HareAudioManifest.MasterGroupName + "/" + HareAudioManifest.SfxGroupName).FirstOrDefault();
            if (music == null || sfx == null) throw new InvalidOperationException(GmlAudioData.MixerPath + " has no Master/Music or Master/Sfx group");

            var sounds = new List<GmlSoundAsset>(data.sounds.Length);
            foreach (var s in data.sounds)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(s.clip)
                    ?? throw new FileNotFoundException(s.clip + " did not import as an AudioClip", s.clip);
                sounds.Add(new GmlSoundAsset { name = s.name, group = s.group, clip = clip, channel = s.Settings.Channel, volume = s.volume, pan = s.pan });
            }
            var creditsFile = data.datafiles.FirstOrDefault(d => d.name == CreditsName);
            var credits = creditsFile == null ? null : AssetDatabase.LoadAssetAtPath<TextAsset>(creditsFile.asset);
            if (creditsFile != null && credits == null) throw new FileNotFoundException(creditsFile.asset + " did not import as a TextAsset", creditsFile.asset);

            var manifest = AssetDatabase.LoadAssetAtPath<HareAudioManifest>(ManifestPath);
            if (manifest == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath));
                manifest = ScriptableObject.CreateInstance<HareAudioManifest>();
                AssetDatabase.CreateAsset(manifest, ManifestPath);
            }
            manifest.Set(sounds, mixer, music, sfx, credits);
            EditorUtility.SetDirty(manifest);
        }

        /// <summary>
        /// Authoring access to an AudioMixer asset through UnityEditor.Audio.AudioMixerController, which is internal:
        /// the same methods the Audio Mixer window calls to add a group and expose a parameter.
        /// </summary>
        public sealed class MixerAuthoring
        {
            const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            static readonly Assembly EditorAssembly = typeof(UnityEditor.Editor).Assembly;
            static readonly Type ControllerType = Required("UnityEditor.Audio.AudioMixerController");
            static readonly Type GroupType = Required("UnityEditor.Audio.AudioMixerGroupController");
            static readonly Type GroupPathType = Required("UnityEditor.Audio.AudioGroupParameterPath");
            static readonly Type ExposedType = Required("UnityEditor.Audio.ExposedAudioParameter");

            readonly object controller;

            public AudioMixer Mixer => (AudioMixer)controller;

            public MixerAuthoring(AudioMixer mixer)
            {
                if (mixer == null || !ControllerType.IsInstanceOfType(mixer)) throw new ArgumentException("not an editor AudioMixerController", nameof(mixer));
                controller = mixer;
            }

            /// <summary>A new mixer asset at <paramref name="path"/> with its Master group and one snapshot.</summary>
            public static MixerAuthoring Create(string path)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var made = Method(ControllerType, "CreateMixerControllerAtPath").Invoke(null, new object[] { path });
                AssetDatabase.ImportAsset(path);
                return new MixerAuthoring((AudioMixer)made);
            }

            public AudioMixerGroup MasterGroup => (AudioMixerGroup)Property(ControllerType, "masterGroup").GetValue(controller);

            public AudioMixerGroup FindChild(AudioMixerGroup parent, string name) =>
                Children(parent).FirstOrDefault(g => g.name == name);

            AudioMixerGroup[] Children(AudioMixerGroup parent) =>
                ((Array)Property(GroupType, "children").GetValue(parent)).Cast<AudioMixerGroup>().ToArray();

            /// <summary>The child group <paramref name="name"/> under <paramref name="parent"/>, added if missing.</summary>
            public AudioMixerGroup EnsureChild(AudioMixerGroup parent, string name)
            {
                var existing = FindChild(parent, name);
                if (existing != null) return existing;
                var group = Method(ControllerType, "CreateNewGroup").Invoke(controller, new object[] { name, false });
                Method(ControllerType, "AddChildToParent").Invoke(controller, new[] { group, (object)parent });
                // The Audio Mixer window's group views are only editor UI; a mixer made headless has none to add to.
                if (((Array)Property(ControllerType, "views").GetValue(controller)).Length > 0)
                    Method(ControllerType, "AddGroupToCurrentView").Invoke(controller, new[] { group });
                Method(ControllerType, "OnSubAssetChanged").Invoke(controller, null);
                EditorUtility.SetDirty((UnityEngine.Object)controller);
                return (AudioMixerGroup)group;
            }

            /// <summary>The exposed parameters' names.</summary>
            public string[] ExposedNames =>
                ((Array)Property(ControllerType, "exposedParameters").GetValue(controller)).Cast<object>()
                    .Select(p => (string)ExposedType.GetField("name").GetValue(p)).ToArray();

            /// <summary>Exposes <paramref name="group"/>'s volume as <paramref name="name"/>, unless it already is.</summary>
            public void ExposeVolume(AudioMixerGroup group, string name)
            {
                if (group == null) throw new ArgumentNullException(nameof(group));
                var guid = Method(GroupType, "GetGUIDForVolume").Invoke(group, null);
                var exposed = (Array)Property(ControllerType, "exposedParameters").GetValue(controller);
                int index = IndexOf(exposed, guid);
                if (index < 0)
                {
                    var path = Activator.CreateInstance(GroupPathType, group, guid);
                    Method(ControllerType, "AddExposedParameter").Invoke(controller, new[] { path });
                    exposed = (Array)Property(ControllerType, "exposedParameters").GetValue(controller);
                    index = IndexOf(exposed, guid);
                    if (index < 0) throw new InvalidOperationException("AudioMixerController did not expose " + group.name + "'s volume");
                }
                var entry = exposed.GetValue(index);
                if ((string)ExposedType.GetField("name").GetValue(entry) == name) return;
                ExposedType.GetField("name").SetValue(entry, name);
                exposed.SetValue(entry, index); // a struct: write the renamed copy back
                Property(ControllerType, "exposedParameters").SetValue(controller, exposed);
                Method(ControllerType, "OnChangedExposedParameter").Invoke(controller, null);
                EditorUtility.SetDirty((UnityEngine.Object)controller);
            }

            static int IndexOf(Array exposed, object guid)
            {
                for (int i = 0; i < exposed.Length; i++)
                    if (Equals(ExposedType.GetField("guid").GetValue(exposed.GetValue(i)), guid)) return i;
                return -1;
            }

            public void Save()
            {
                EditorUtility.SetDirty((UnityEngine.Object)controller);
                AssetDatabase.SaveAssetIfDirty((UnityEngine.Object)controller);
            }

            static Type Required(string name) =>
                EditorAssembly.GetType(name) ?? throw new MissingMemberException("UnityEditor has no " + name + " in this Unity version");

            static MethodInfo Method(Type type, string name) =>
                type.GetMethod(name, Any) ?? throw new MissingMethodException(type.FullName, name);

            static PropertyInfo Property(Type type, string name) =>
                type.GetProperty(name, Any) ?? throw new MissingMemberException(type.FullName, name);
        }
    }
}
