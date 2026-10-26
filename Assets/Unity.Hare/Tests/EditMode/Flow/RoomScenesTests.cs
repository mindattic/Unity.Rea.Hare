using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Unity.Hare.Arena;
using Unity.Hare.EditorTools;
using Unity.Hare.Flow;
using Unity.Hare.Input;
using Unity.Hare.Player;
using Unity.Hare.Rendering;
using Unity.Hare.Rooms;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>
    /// U.HARE-T50: the scenes ProjectSetup composes in code, one per room plus the bootstrap, are in Build Settings in
    /// HARE.project.gmx's room order, and each carries what the room flow needs.
    /// </summary>
    public class RoomScenesTests
    {
        [Test]
        public void EveryRoomHasAScene_ListedAfterTheBootstrapInRoomOrder()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
            Assert.That(scenes.First(), Is.EqualTo(ProjectSetup.ScenePath), "a launch starts at the bootstrap");
            var rooms = RoomList.All.Select(ProjectSetup.RoomScenePath).ToList();
            CollectionAssert.AreEqual(rooms, scenes.Skip(1).Take(rooms.Count), "every room, in HARE.project.gmx's order");
            foreach (var room in RoomList.All)
            {
                string path = ProjectSetup.RoomScenePath(room);
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, path);
                Assert.That(System.IO.Path.GetFileNameWithoutExtension(path), Is.EqualTo(RoomList.GmlName(room)),
                    "the flow loads a room's scene by its GML name");
            }
            Assert.That(scenes.Any(s => s.Contains("room_boss1")), Is.False, "room_boss1 is unreferenced in the original");
        }

        [Test]
        public void TheMainSceneIsTheBootstrapOnly()
        {
            var scene = EditorSceneManager.OpenScene(ProjectSetup.ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            Assert.That(roots.Length, Is.EqualTo(1), "the room flow's director and nothing else");
            var director = roots[0].GetComponent<RoomFlowDirector>();
            Assert.That(director, Is.Not.Null);
            Assert.That(new SerializedObject(director).FindProperty("enterFirstRoom").boolValue, Is.True,
                "the bootstrap sends the game to the first room");
            Assert.That(roots[0].GetComponentsInChildren<Component>().Length, Is.EqualTo(2), "a Transform and the director");
        }

        [Test]
        public void EachRoomSceneHasTheCameraRigTheFadeDrawsOn_AndOnlyThePlayRoomsHaveTheShip()
        {
            foreach (var room in RoomList.All)
            {
                var scene = EditorSceneManager.OpenScene(ProjectSetup.RoomScenePath(room), OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                var cameras = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).Where(c => c.CompareTag("MainCamera")).ToList();
                Assert.That(cameras.Count, Is.EqualTo(1), room + ": one main camera");
                Assert.That(cameras[0].GetComponent<LetterboxCamera>(), Is.Not.Null, room + ": letterboxed");
                Assert.That(cameras[0].GetComponentInChildren<ScreenOverlayLayer>(true), Is.Not.Null, room + ": the fade's overlay");
                Assert.That(roots.All(r => r.GetComponentInChildren<RoomFlowDirector>(true) == null), room + ": the director lives in the bootstrap");

                var ships = roots.SelectMany(r => r.GetComponentsInChildren<PlayerShipController>(true)).ToList();
                if (ProjectSetup.IsPlayRoom(room))
                {
                    Assert.That(ships.Count, Is.EqualTo(1), room + ": obj_player");
                    var vulcan = ships[0].GetComponent<VulcanWeapon>();
                    Assert.That(new SerializedObject(vulcan).FindProperty("loopClip").objectReferenceValue, Is.Not.Null, room + ": the vulcan's sounds");
                }
                else Assert.That(ships, Is.Empty, room + ": no ship outside the play rooms");
            }
        }

        [Test]
        public void TheStartupSceneHoldsObjStartup_AndNoOtherRoomDoes()
        {
            foreach (var room in RoomList.All)
            {
                var scene = EditorSceneManager.OpenScene(ProjectSetup.RoomScenePath(room), OpenSceneMode.Single);
                var startups = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StartupRoom>(true)).ToList();
                if (room == RoomId.Startup)
                {
                    Assert.That(startups.Count, Is.EqualTo(1), "startup.room.gmx places one obj_startup");
                    Assert.That(startups[0].name, Is.EqualTo(StartupRoom.ObjectName));
                    Assert.That(startups[0].transform.childCount, Is.EqualTo(0), "it builds its backgrounds and note itself, at runtime");
                }
                else Assert.That(startups, Is.Empty, room + ": obj_startup is startup's alone");
            }
            Assert.That(ProjectSetup.RoomControllerType(RoomId.Startup), Is.EqualTo(typeof(StartupRoom)));
        }

        [Test]
        public void EachPlayRoomHoldsObjGamesArena_AndNoOtherRoomDoes()
        {
            foreach (var room in RoomList.All)
            {
                var scene = EditorSceneManager.OpenScene(ProjectSetup.RoomScenePath(room), OpenSceneMode.Single);
                var arenas = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<ArenaRoom>(true)).ToList();
                if (ProjectSetup.IsPlayRoom(room))
                {
                    Assert.That(arenas.Count, Is.EqualTo(1), room + ": one obj_game");
                    Assert.That(arenas[0].name, Is.EqualTo(ArenaRoom.ObjectName));
                    Assert.That(arenas[0].transform.childCount, Is.EqualTo(0), room + ": it builds its backgrounds, minimap and HUD itself, at runtime");
                    Assert.That(ProjectSetup.RoomControllerType(room), Is.EqualTo(typeof(ArenaRoom)));
                }
                else Assert.That(arenas, Is.Empty, room + ": the arena is the play rooms' alone");
            }
        }
    }

    /// <summary>U.HARE-T50: the keys the room flow reads, as the original's controllers answer them.</summary>
    public class RoomFlowControlsTests : InputTestFixture
    {
        Keyboard keyboard;
        HareControls controls;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            controls = new HareControls();
            controls.Meta.Enable();
        }

        public override void TearDown()
        {
            controls.Meta.Disable();
            Object.DestroyImmediate(controls.asset);
            controls = null;
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            base.TearDown();
        }

        [Test]
        public void MTogglesTheMinimap()
        {
            Press(keyboard.mKey);
            Assert.That(controls.Meta.Map.WasPressedThisFrame(), Is.True, "obj_game: M key press runs show_map = !show_map");
            Release(keyboard.mKey);
            Press(keyboard.tabKey);
            Assert.That(controls.Meta.Map.WasPressedThisFrame(), Is.False, "Tab is not the original's key");
        }

        [Test]
        public void EscIsTheMenuKey()
        {
            Press(keyboard.escapeKey);
            Assert.That(controls.Meta.Menu.WasPressedThisFrame(), Is.True, "obj_game/obj_map/obj_story/obj_intro: Esc key press");
        }

        [Test]
        public void F1TogglesFullscreen_AndF12IsDebug()
        {
            Press(keyboard.f1Key);
            Assert.That(controls.Meta.Fullscreen.WasPressedThisFrame(), Is.True, "obj_title/obj_game: F1 key press");
            Assert.That(controls.Meta.Debug.WasPressedThisFrame(), Is.False);
            Release(keyboard.f1Key);

            Press(keyboard.f12Key);
            Assert.That(controls.Meta.Debug.WasPressedThisFrame(), Is.True, "obj_game: F12 key press runs debug = !debug");
            Assert.That(controls.Meta.Fullscreen.WasPressedThisFrame(), Is.False);
        }
    }
}
