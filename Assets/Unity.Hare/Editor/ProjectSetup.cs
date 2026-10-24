using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.EditorTools
{
    /// <summary>
    /// Writes the project's URP 2D pipeline and its main scene. Both are generated rather than
    /// hand-made so they stay reproducible, and both are committed: they are written only when
    /// missing, because Unity gives every object a fresh file ID and regenerating on each run would
    /// leave a no-op diff. To change them, edit this file, run the menu item, and commit both.
    ///
    /// Batch: Unity -batchmode -quit -projectPath . -executeMethod Unity.Hare.EditorTools.ProjectSetup.SetUpAll
    /// </summary>
    public static class ProjectSetup
    {
        public const string SettingsFolder = "Assets/Unity.Hare/Settings";
        public const string RendererPath = SettingsFolder + "/Renderer2D.asset";
        public const string PipelinePath = SettingsFolder + "/UniversalRP-2D.asset";
        public const string ScenePath = "Assets/Unity.Hare/Scenes/Main.unity";

        /// <summary>The night-sky backdrop room_gamescreen uses for the main open-arena combat room.</summary>
        static readonly Color Backdrop = new Color(0.02f, 0.02f, 0.06f, 1f);

        /// <summary>Batch entry point: pipeline, project settings and the main scene, each only if missing.</summary>
        public static void SetUpAll()
        {
            SetUpRendering();
            SetUpProjectSettings();
            if (!File.Exists(ScenePath)) RebuildMainScene();
            else EnsurePlayerShip();
        }

        /// <summary>A URP asset whose only renderer is the 2D renderer, made the default for every quality level.</summary>
        [MenuItem("Unity.Hare/Set Up URP 2D")]
        public static void SetUpRendering()
        {
            Directory.CreateDirectory(SettingsFolder);
            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            GraphicsSettings.defaultRenderPipeline = pipeline;
            for (int level = 0; level < QualitySettings.names.Length; level++)
            {
                QualitySettings.SetQualityLevel(level, false);
                QualitySettings.renderPipeline = null; // every level falls back to the default above
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Unity.Hare] URP 2D pipeline: " + PipelinePath);
        }

        /// <summary>
        /// Zero-gravity 2D world (the player flies under thrust/rotate, not gravity, per the GML
        /// original's player_boost/player_brake); product identity; the Input System as the only
        /// input backend.
        /// </summary>
        [MenuItem("Unity.Hare/Set Up Project Settings")]
        public static void SetUpProjectSettings()
        {
            Physics2D.gravity = Vector2.zero;
            PlayerSettings.companyName = "MindAttic";
            PlayerSettings.productName = "Unity.Hare";
            PlayerSettings.colorSpace = ColorSpace.Linear;

            var player = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var input = player.FindProperty("activeInputHandler");
            if (input != null)
            {
                input.intValue = 1; // Input System package only
                player.ApplyModifiedPropertiesWithoutUndo();
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>The main scene: an orthographic camera over the open arena (room_gamescreen) and a global 2D light.</summary>
        [MenuItem("Unity.Hare/Rebuild Main Scene")]
        public static void RebuildMainScene()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraGo.transform.SetPositionAndRotation(new Vector3(0f, 0f, -10f), Quaternion.identity);
            var camera = cameraGo.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5.4f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Backdrop;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 100f;
            cameraGo.AddComponent<UniversalAdditionalCameraData>();
            cameraGo.AddComponent<AudioListener>();

            var lightGo = new GameObject("Global Light 2D");
            var light = lightGo.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;

            AddPlayerShip();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Unity.Hare] Scene written to " + ScenePath);
        }

        /// <summary>
        /// Adds the player ship to the existing main scene if it has none, leaving everything else in the scene (and
        /// its file IDs) as it was.
        /// Batch: Unity -batchmode -quit -projectPath . -executeMethod Unity.Hare.EditorTools.ProjectSetup.EnsurePlayerShip
        /// </summary>
        [MenuItem("Unity.Hare/Add Player Ship To Main Scene")]
        public static void EnsurePlayerShip()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<PlayerShipController>(true) != null) return;
            AddPlayerShip();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Unity.Hare] Player ship added to " + ScenePath);
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

        const string WeaponSounds = "Assets/Unity.Hare/Audio/Weapons/";

        static void Wire(Object component, params (string field, string clip)[] sounds)
        {
            var so = new SerializedObject(component);
            foreach (var (field, clip) in sounds)
            {
                var property = so.FindProperty(field);
                var asset = AssetDatabase.LoadAssetAtPath<AudioClip>(WeaponSounds + clip + ".wav");
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
