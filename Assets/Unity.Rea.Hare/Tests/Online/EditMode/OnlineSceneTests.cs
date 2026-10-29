using System.Linq;
using NUnit.Framework;
using Unity.Hare.EditorTools;
using Unity.Hare.EditorTools.Online;
using Unity.Hare.Online;
using Unity.Hare.Player;
using Unity.Multiplayer;
using Unity.Multiplayer.Editor;
using Unity.Multiplayer.UI;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Hare.Tests.Online.EditMode
{
    /// <summary>
    /// The online scene (U.HARE-T35) is wired the way the kit needs it, beside a local game that has none of it: a
    /// broken reference or an unregistered network prefab fails here instead of in a build.
    /// </summary>
    public class OnlineSceneTests
    {
        [Test]
        public void TheOnlineSceneHasTheKitsNetworkManagerFrontEndAndThePlayerPrefab()
        {
            Scene scene = EditorSceneManager.OpenScene(OnlineSceneBuilder.ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            NetworkManager manager = roots.Select(r => r.GetComponentInChildren<NetworkManager>()).First(m => m != null);

            Assert.That(manager.GetComponent<NetBootstrap>(), Is.Not.Null, "NetBootstrap");
            Assert.That(manager.GetComponent<NetFrontEnd>(), Is.Not.Null, "NetFrontEnd (Host, Join by IP, Join by code, the lobby)");
            Assert.That(manager.NetworkConfig.PlayerPrefab, Is.Not.Null, "the Player Prefab");
            Assert.That(manager.NetworkConfig.PlayerPrefab.GetComponent<HareNetPlayer>(), Is.Not.Null, "the player prefab is a HareNetPlayer");
            Assert.That(manager.NetworkConfig.PlayerPrefab, Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(OnlineSceneBuilder.PrefabPath)));

            var lists = new SerializedObject(manager.GetComponent<NetBootstrap>()).FindProperty("prefabLists");
            Assert.That(Enumerable.Range(0, lists.arraySize)
                .Select(i => lists.GetArrayElementAtIndex(i).objectReferenceValue as NetPrefabList)
                .Any(l => l != null && l.Prefabs.Contains(manager.NetworkConfig.PlayerPrefab)), "the player prefab is in the scene's prefab list");

            Assert.That(roots.Any(r => r.GetComponentInChildren<HareOnlineGame>() != null), "HareOnlineGame");
            Assert.That(roots.Any(r => r.GetComponentInChildren<Camera>() != null), "the arena camera");
        }

        [Test]
        public void TheLocalGameStaysFirstInBuildSettings_AndHasNoneOfTheKit()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            Assert.That(scenes.First(), Is.EqualTo(ProjectSetup.ScenePath), "a launch plays the local game");
            Assert.That(scenes, Does.Contain(OnlineSceneBuilder.ScenePath));
            Assert.That(scenes.Last(), Is.EqualTo(OnlineSceneBuilder.ScenePath));

            Scene main = EditorSceneManager.OpenScene(ProjectSetup.ScenePath, OpenSceneMode.Single);
            var roots = main.GetRootGameObjects();
            Assert.That(roots.All(r => r.GetComponentInChildren<NetworkManager>(true) == null), "the local game has no NetworkManager");
            Assert.That(roots.Any(r => r.GetComponentInChildren<Unity.Hare.Flow.RoomFlowDirector>(true) != null),
                "the local game's bootstrap starts the room flow (U.HARE-T50)");
            Scene arena = EditorSceneManager.OpenScene(ProjectSetup.RoomScenePath(Unity.Hare.Flow.RoomId.GameScreen), OpenSceneMode.Single);
            Assert.That(arena.GetRootGameObjects().Any(r => r.GetComponentInChildren<PlayerShipController>(true) != null), "the local game still has its ship");
            Assert.That(arena.GetRootGameObjects().All(r => r.GetComponentInChildren<NetworkManager>(true) == null), "the local arena has no NetworkManager");
            Assert.That(HareOnlineGame.SoloScene, Is.EqualTo(System.IO.Path.GetFileNameWithoutExtension(ProjectSetup.ScenePath)),
                "Play solo opens the local game's scene");
        }

        [Test]
        public void EveryNetworkPrefabPassesTheKitsValidator()
        {
            NetPrefabValidationReport report = NetPrefabValidator.ValidateProject(new[] { "Assets/Unity.Rea.Hare" });
            report.Log();
            Assert.That(report.HasErrors, Is.False, "see the validator's messages in the log");
        }
    }
}
