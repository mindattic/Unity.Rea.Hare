using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Hare.Arena;
using Unity.Hare.Enemies;
using Unity.Hare.Flow;
using Unity.Hare.Session;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T70, played end to end: the bootstrap loads, the campaign flows from the title through the story and the
    /// map to a level, the level starts, a laser kill scores, completing the level shows its results. The one run
    /// tools/capture.ps1 repeats with screenshots (Unity.Hare.Demo.ScriptedRun).
    /// </summary>
    public class CampaignFlowPlayTests : InputTestFixture
    {
        Keyboard keyboard;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
        }

        [UnityTearDown]
        public IEnumerator LeaveNoRoomBehind()
        {
            Gml.Paused = false;
            yield return PlayModeRooms.TearDown();
        }

        static IEnumerator WaitFor(Func<bool> condition, string what, float seconds = 10f)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > until) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        IEnumerator Tap(KeyControl key)
        {
            Press(key, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(key, queueEventOnly: true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheCampaignFlowsFromTheTitleToTheLevelsResults()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return WaitFor(() => RoomFlowDirector.Active != null && SceneManager.GetActiveScene().name == "startup", "the bootstrap to start the room flow");
            var director = RoomFlowDirector.Active;
            director.GetFullscreen = () => false;
            director.SetFullscreen = _ => { };

            director.Execute(new GoToRoom(RoomFlowDirector.Seat, RoomId.Title));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_title", "room_title");

            // The campaign button's work: the mode, then the fade to the story; any key there goes on to the map.
            director.ExecuteSession(new ChooseCampaign(RoomFlowDirector.Seat));
            Assert.That(director.Session.Mode, Is.EqualTo(GameMode.Campaign));
            Assert.That(director.Execute(new FadeToRoom(RoomFlowDirector.Seat, RoomId.Story, onlyWhenIdle: true)).Accepted, Is.True);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_story", "room_story");
            yield return WaitFor(() => director.Flow.Fade.Mode == FadeMode.Off, "the story's fade-out");
            yield return Tap(keyboard.spaceKey);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_map", "room_map");
            Assert.That(director.Session.Mode, Is.EqualTo(GameMode.Campaign));

            // A level starts.
            director.Execute(new GoToRoom(RoomFlowDirector.Seat, RoomId.GameScreen));
            yield return WaitFor(() => SceneManager.GetActiveScene().name == "room_gamescreen", "room_gamescreen");
            ArenaRoom arena = null;
            yield return WaitFor(() =>
            {
                arena = UnityEngine.Object.FindAnyObjectByType<ArenaRoom>();
                return arena != null && arena.State != null && arena.State.Created && arena.Player != null;
            }, "obj_game's Create");
            Assert.That(director.Session.Level, Is.EqualTo(1));

            // A kill scores.
            int kills = director.Session.Kills;
            var spawned = EnemyDirector.Execute(new SpawnEnemy(EnemyKind.Dagger, new Vec2(0f, 0f)));
            Assert.That(spawned.Body, Is.Not.Null);
            EnemyDirector.Execute(new StrikeBeam(spawned.Body.Id, BeamKind.Laser));
            Assert.That(director.Session.Kills, Is.EqualTo(kills + 1));

            // The level completes.
            Assert.That(arena.Results, Is.Null);
            director.ExecuteSession(new CompleteLevel(RoomFlowDirector.Seat));
            Assert.That(arena.Results, Is.Not.Null, "obj_level_results");
            Assert.That(Gml.Paused, Is.True);
        }
    }
}
