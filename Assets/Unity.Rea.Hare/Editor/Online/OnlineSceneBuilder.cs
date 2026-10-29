using System.Collections.Generic;
using System.IO;
using Unity.Hare.Online;
using Unity.Multiplayer;        // NetPrefabList
using Unity.Multiplayer.Editor; // NetFrontEndSetup, NetSceneSetupOptions
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unity.Hare.EditorTools.Online
{
    /// <summary>
    /// Writes the online scene (U.HARE-T35) in code, beside the local game's Main.unity: the arena's camera and light,
    /// the kit's NetworkManager with Unity Transport, NetBootstrap and the drop-in NetFrontEnd (through the kit's own
    /// Add Front End), the <see cref="HareNetPlayer"/> prefab as the Player Prefab and in the scene's prefab list, and
    /// <see cref="HareOnlineGame"/>. The scene goes second in Build Settings, so a launch still plays the local game
    /// first. Like <see cref="ProjectSetup"/>, the prefab, the prefab list, the scene and mp-host.json are written only
    /// when missing (Unity gives every object a fresh file ID, so a rerun would leave a no-op diff); to change one,
    /// delete it, run the menu item and commit the result.
    ///
    /// Batch: Unity -batchmode -quit -projectPath . -executeMethod Unity.Hare.EditorTools.Online.OnlineSceneBuilder.BuildAll
    /// </summary>
    public static class OnlineSceneBuilder
    {
        public const string ScenePath = "Assets/Unity.Hare/Scenes/Online.unity";
        public const string PrefabPath = "Assets/Unity.Hare/Resources/" + HareNetPlayer.PrefabResource + ".prefab";
        public const string PrefabListPath = "Assets/Unity.Hare/Online/HareNetPrefabs.asset";
        public const string MpHostPath = "mp-host.json";

        /// <summary>mp-host's defaults: the server and the smoke test's bots run the online scene; Hare has no scenarios online yet.</summary>
        public const string MpHostJson = "{\n  \"name\": \"unity-hare\",\n  \"scene\": \"" + ScenePath + "\",\n  \"bots\": \"A,B\"\n}\n";

        /// <summary>Batch entry point: the prefab, the prefab list, the scene and mp-host.json, each only if missing.</summary>
        public static void BuildAll()
        {
            var prefab = EnsurePlayerPrefab();
            var list = EnsurePrefabList(prefab);
            if (!File.Exists(ScenePath)) WriteScene(prefab, list);
            EnsureBuildSettings();
            if (!File.Exists(MpHostPath))
                File.WriteAllText(MpHostPath, MpHostJson);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Unity.Hare/Rebuild Online Scene")]
        public static void RebuildOnlineScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var prefab = EnsurePlayerPrefab();
            WriteScene(prefab, EnsurePrefabList(prefab));
            EnsureBuildSettings();
            AssetDatabase.SaveAssets();
        }

        /// <summary>The online player: a NetworkObject, a SpriteRenderer and <see cref="HareNetPlayer"/>.</summary>
        public static GameObject EnsurePlayerPrefab()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var go = new GameObject("HareNetPlayer");
            go.AddComponent<NetworkObject>();
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<HareNetPlayer>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            Debug.Log("[Unity.Hare] Online player prefab written to " + PrefabPath);
            return prefab;
        }

        /// <summary>The scene's NetPrefabList, holding the player prefab.</summary>
        public static NetPrefabList EnsurePrefabList(GameObject prefab)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetPrefabList>(PrefabListPath);
            if (list == null)
            {
                list = ScriptableObject.CreateInstance<NetPrefabList>();
                AssetDatabase.CreateAsset(list, PrefabListPath);
            }
            bool listed = false;
            foreach (var p in list.Prefabs) listed |= p == prefab;
            if (!listed)
            {
                list.Add(prefab);
                EditorUtility.SetDirty(list);
            }
            return list;
        }

        static void WriteScene(GameObject prefab, NetPrefabList list)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ProjectSetup.AddArenaCameraAndLight();

            var setup = NetFrontEndSetup.Run(scene, new NetSceneSetupOptions { PrefabList = list, RegisterProjectPrefabs = false });
            if (!setup.Succeeded) throw new System.InvalidOperationException("[Unity.Hare] Add Front End failed: " + setup.Message);
            setup.Scene.Manager.NetworkConfig.PlayerPrefab = prefab;
            EditorUtility.SetDirty(setup.Scene.Manager);

            new GameObject("HareOnlineGame").AddComponent<HareOnlineGame>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[Unity.Hare] Online scene written to " + ScenePath);
        }

        /// <summary>Main.unity first (a launch plays the local game), the online scene after it.</summary>
        static void EnsureBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != ScenePath) scenes.Add(s);
            if (scenes.Count == 0 || scenes[0].path != ProjectSetup.ScenePath)
            {
                scenes.RemoveAll(s => s.path == ProjectSetup.ScenePath);
                scenes.Insert(0, new EditorBuildSettingsScene(ProjectSetup.ScenePath, true));
            }
            scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
