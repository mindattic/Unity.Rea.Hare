using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.Hare.Audio;
using Unity.Hare.EditorTools.Rendering;
using Unity.Hare.Flow;
using Unity.Hare.Player;
using Unity.Hare.Rendering;
using Unity.Hare.Weapons;

namespace Unity.Hare.EditorTools
{
    /// <summary>
    /// Writes the project's URP 2D pipeline, its sorting layers and its scenes: one per room of the original
    /// (U.HARE-T50, <see cref="RoomList"/>, under <see cref="RoomsFolder"/>, named after the GML room so the room flow
    /// loads them by name) and the bootstrap, Main.unity, which holds only the <see cref="RoomFlowDirector"/> that
    /// sends the game to the first room. All are generated rather than hand-made so they stay reproducible, and all
    /// are committed: the pipeline assets and scenes are written only when missing, because Unity gives every object
    /// a fresh file ID and regenerating on each run would leave a no-op diff. To change them, edit this file, run the
    /// menu item, and commit the result. This file never references UnityEngine.Rendering.Universal itself
    /// (U.HARE-T44): the 2D pipeline assets, camera data and Light2D are created through <see cref="UrpSeam"/>, the
    /// one place that does.
    ///
    /// Batch: Unity -batchmode -quit -projectPath . -executeMethod Unity.Hare.EditorTools.ProjectSetup.SetUpAll
    /// </summary>
    public static class ProjectSetup
    {
        public const string SettingsFolder = "Assets/Unity.Hare/Settings";
        public const string RendererPath = SettingsFolder + "/Renderer2D.asset";
        public const string PipelinePath = SettingsFolder + "/UniversalRP-2D.asset";
        /// <summary>The bootstrap scene, first in Build Settings: a launch starts here.</summary>
        public const string ScenePath = "Assets/Unity.Hare/Scenes/Main.unity";
        /// <summary>Where the room scenes live.</summary>
        public const string RoomsFolder = "Assets/Unity.Hare/Scenes/Rooms";

        /// <summary>The night-sky backdrop of the play rooms (room_gamescreen, room_sidescroll, room_shaft).</summary>
        static readonly Color Backdrop = new Color(0.02f, 0.02f, 0.06f, 1f);

        /// <summary>The scene of <paramref name="room"/>: Rooms/startup.unity, Rooms/room_title.unity, ...</summary>
        public static string RoomScenePath(RoomId room) => RoomsFolder + "/" + RoomList.GmlName(room) + ".unity";

        /// <summary>The rooms obj_player is placed in, whose controller is obj_game.</summary>
        public static bool IsPlayRoom(RoomId room) => room == RoomId.GameScreen || room == RoomId.SideScroll || room == RoomId.Shaft;

        /// <summary>Batch entry point: pipeline, project settings, the room scenes and the bootstrap, each only if missing.</summary>
        public static void SetUpAll()
        {
            SetUpRendering();
            SetUpProjectSettings();
            foreach (var room in RoomList.All)
                if (!File.Exists(RoomScenePath(room))) RebuildRoomScene(room);
            if (!IsBootstrap(ScenePath)) RebuildMainScene();
            EnsureBuildSettings();
            AssetDatabase.SaveAssets();
        }

        /// <summary>A URP asset whose only renderer is the 2D renderer, made the default for every quality level.</summary>
        [MenuItem("Unity.Hare/Set Up URP 2D")]
        public static void SetUpRendering()
        {
            Directory.CreateDirectory(SettingsFolder);
            UrpSeam.EnsureUrp2DPipeline(RendererPath, PipelinePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Unity.Hare] URP 2D pipeline: " + PipelinePath);
        }

        /// <summary>
        /// Zero-gravity 2D world (the player flies under thrust/rotate, not gravity, per the GML
        /// original's player_boost/player_brake); product identity; the Input System as the only
        /// input backend; the original's 1920x1080 reference resolution (<see cref="LetterboxCamera"/>
        /// scales it to the window at runtime); the sorting layers' back-to-front order
        /// (<see cref="HareSortingLayers"/>).
        /// </summary>
        [MenuItem("Unity.Hare/Set Up Project Settings")]
        public static void SetUpProjectSettings()
        {
            Physics2D.gravity = Vector2.zero;
            PlayerSettings.companyName = "MindAttic";
            PlayerSettings.productName = "Unity.Hare";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = (int)LetterboxCamera.ReferenceWidth;
            PlayerSettings.defaultScreenHeight = (int)LetterboxCamera.ReferenceHeight;

            var player = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input = player.FindProperty("activeInputHandler");
            if (input != null)
            {
                input.intValue = 1; // Input System package only
                player.ApplyModifiedPropertiesWithoutUndo();
            }

            SortingLayerSetup.EnsureSortingLayers(HareSortingLayers.Order);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// The bootstrap scene: the <see cref="RoomFlowDirector"/> and nothing else. On Start it sends the game to
        /// the rooms table's first room (startup), whose scene then replaces this one.
        /// </summary>
        [MenuItem("Unity.Hare/Rebuild Main Scene (Bootstrap)")]
        public static void RebuildMainScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RoomFlowDirector.Create(enterFirstRoom: true);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Unity.Hare] Bootstrap scene written to " + ScenePath);
        }

        /// <summary>Rewrites every room scene and the bootstrap, then the build settings.</summary>
        [MenuItem("Unity.Hare/Rebuild Room Scenes")]
        public static void RebuildAllScenes()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (var room in RoomList.All) RebuildRoomScene(room);
            RebuildMainScene();
        }

        /// <summary>
        /// One room's scene: the 2D camera rig (letterboxed to the reference resolution, carrying the screen overlay
        /// the room's obj_fade_* draws on) and a global 2D light; the play rooms also get room_gamescreen's night-sky
        /// backdrop and obj_player. What else each room holds (its controller, menus, emitters) is its own ticket's.
        /// </summary>
        public static void RebuildRoomScene(RoomId room)
        {
            string path = RoomScenePath(room);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            bool play = IsPlayRoom(room);
            AddCameraAndLight(play ? Backdrop : Color.black);
            if (play) AddPlayerShip();
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[Unity.Hare] Room scene written to " + path);
        }

        /// <summary>
        /// Build Settings: the bootstrap first (a launch plays the local game), then every room scene in
        /// HARE.project.gmx's order, then whatever else was listed (the online scene stays last).
        /// </summary>
        public static void EnsureBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            var ours = new HashSet<string> { ScenePath };
            foreach (var room in RoomList.All)
            {
                scenes.Add(new EditorBuildSettingsScene(RoomScenePath(room), true));
                ours.Add(RoomScenePath(room));
            }
            foreach (var other in EditorBuildSettings.scenes)
                if (!ours.Contains(other.path)) scenes.Add(other);
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Whether the scene at <paramref name="path"/> exists and is the bootstrap (it holds the room flow's director).</summary>
        static bool IsBootstrap(string path)
        {
            if (!File.Exists(path)) return false;
            var script = MonoScriptFor(typeof(RoomFlowDirector));
            if (script == null) return false;
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script));
            return File.ReadAllText(path).Contains("guid: " + guid);
        }

        static MonoScript MonoScriptFor(System.Type type)
        {
            foreach (var guid in AssetDatabase.FindAssets(type.Name + " t:MonoScript"))
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (script != null && script.GetClass() == type) return script;
            }
            return null;
        }

        /// <summary>
        /// The arena's 2D camera rig (<see cref="CameraRig"/>, letterboxed to the reference resolution, over
        /// room_gamescreen's backdrop) and a global 2D light, in the open scene. The play rooms and the online scene
        /// (U.HARE-T35) start from it.
        /// </summary>
        public static void AddArenaCameraAndLight() => AddCameraAndLight(Backdrop);

        static void AddCameraAndLight(Color backdrop)
        {
            var cameras = CameraRig.Build(new Vector3(0f, 0f, -10f), 5.4f, backdrop);
            UrpSeam.AddCameraData(cameras.Bars.gameObject);
            UrpSeam.AddCameraData(cameras.Main.gameObject);

            var lightGo = new GameObject("Global Light 2D");
            UrpSeam.AddGlobalLight2D(lightGo, intensity: 1f);
        }

        /// <summary>
        /// obj_player with every weapon of its Create (<see cref="ShipLoadout"/>'s default) at the arena's centre, in
        /// the open scene, with the ported weapon sounds wired in.
        /// </summary>
        static GameObject AddPlayerShip()
        {
            var ship = PlayerShipBuilder.Build(Vector3.zero, new ShipLoadout(), resetInEditMode: false);
            foreach (var vulcan in ship.GetComponents<VulcanWeapon>())
                Wire(vulcan, ("spinUpClip", "sfx_vulcan_spinup"), ("loopClip", "sfx_vulcan_loop"),
                    ("spinDownClip", "sfx_vulcan_spindown"), ("emptyClipClip", "sfx_empty_clip"));
            Wire(ship.GetComponent<PlayerAmmo>(), ("reloadHalfClip", "sfx_reload_half"), ("reloadFullClip", "sfx_reload_full"));
            return ship;
        }

        static void Wire(Object component, params (string field, string clip)[] sounds)
        {
            var so = new SerializedObject(component);
            foreach (var (field, clip) in sounds)
            {
                var property = so.FindProperty(field);
                var asset = HareAudio.Clip(clip);
                if (property == null || asset == null)
                {
                    Debug.LogWarning("[Unity.Hare] Could not wire " + clip + " into " + component.GetType().Name + "." + field);
                    continue;
                }
                property.objectReferenceValue = asset;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
