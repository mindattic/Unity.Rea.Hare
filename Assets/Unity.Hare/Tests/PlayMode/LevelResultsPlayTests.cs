using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Unity.Hare.Arena;
using Unity.Hare.Enemies;
using Unity.Hare.Flow;
using Unity.Hare.Player;
using Unity.Hare.Session;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T22, played: in room_gamescreen a laser kill counts towards KILLS, obj_healthbar's clock counts the room's
    /// seconds, and set_level_complete puts obj_level_results on the HUD ("Level Complete", the Time, the Enemies Killed,
    /// the Enter prompt) with the ship frozen. Enter sends the ship off: unpaused, invulnerable, turning up toward exit_x,
    /// and once it is off the top the score is zeroed and level_02 is set up in the same room, the panel gone.
    /// </summary>
    public class LevelResultsPlayTests
    {
        RoomFlowDirector director;

        [UnityTearDown]
        public IEnumerator LeaveNoRoomBehind()
        {
            Gml.Paused = false;
            if (RoomFlowDirector.Active != null) UnityEngine.Object.Destroy(RoomFlowDirector.Active.gameObject);
            var empty = SceneManager.CreateScene("LevelResultsPlayTests.Empty");
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != empty) yield return SceneManager.UnloadSceneAsync(scene);
            }
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

        static ArenaRoom Arena() => UnityEngine.Object.FindAnyObjectByType<ArenaRoom>();

        [UnityTest]
        public IEnumerator TheResultsPanelShowsTheLevelsTimeAndKills_AndEnterFliesTheShipOffToTheNextLevel()
        {
            director = RoomFlowDirector.Create(enterFirstRoom: false);
            director.GetFullscreen = () => false;
            director.SetFullscreen = _ => { };
            director.Execute(new GoToRoom(RoomFlowDirector.Seat, RoomId.GameScreen));
            string name = RoomList.GmlName(RoomId.GameScreen);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == name && Arena() != null && Arena().State != null && Arena().State.Created && Arena().Player != null,
                name + " and obj_game's Create");
            var arena = Arena();
            var session = director.Session;
            var ship = arena.Player;
            Assert.That(session.Level, Is.EqualTo(1));

            // A dagger the laser destroys is one of the "Enemies Killed" (BIBLE D11).
            int killsBefore = session.Kills;
            var spawned = EnemyDirector.Execute(new SpawnEnemy(EnemyKind.Dagger, new Vec2(0f, 0f)));
            Assert.That(spawned.Body, Is.Not.Null);
            EnemyDirector.Execute(new StrikeBeam(spawned.Body.Id, BeamKind.Laser));
            Assert.That(session.Kills, Is.EqualTo(killsBefore + 1), "KILLS += 1");

            // obj_healthbar's strTime counts the room's unpaused seconds.
            string before = arena.Clock.Text;
            yield return WaitFor(() => arena.Clock.Text != before, "a second on obj_healthbar's clock", 3f);
            Assert.That(arena.Clock.Text, Does.Match(@"^\d\d:\d\d$"));

            // set_level_complete: obj_level_results at (screen_width/2, screen_height/4), paused.
            director.ExecuteSession(new AddScore(RoomFlowDirector.Seat, 120));
            director.ExecuteSession(new CompleteLevel(RoomFlowDirector.Seat));
            Assert.That(arena.Results, Is.Not.Null, "instance_create(0,0, obj_level_results)");
            Assert.That(Gml.Paused, Is.True);
            var view = arena.Hud.Q<LevelResultsView>(LevelResultsView.ElementName);
            Assert.That(view, Is.SameAs(arena.ResultsView), "drawn on the GUI layer");
            Assert.That(view.Panel.style.left.value.value, Is.EqualTo(604f), "x - 356");
            Assert.That(view.Panel.style.top.value.value, Is.EqualTo(270f));
            Assert.That(view.Panel.style.opacity.value, Is.EqualTo(0.7f).Within(1e-4f));
            var texts = view.Texts();
            Assert.That(texts[0], Is.EqualTo("Level Complete"));
            Assert.That(texts[1], Is.EqualTo("Time"));
            Assert.That(texts[2], Is.EqualTo(arena.Clock.Text));
            Assert.That(texts[3], Is.EqualTo("Enemies Killed"));
            Assert.That(texts[4], Is.EqualTo(session.Kills.ToString()));
            Assert.That(texts[5], Is.EqualTo("Press [ENTER] to continue"));

            // Paused, the ship stands still (speed = 0).
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(ship.GetComponent<Rigidbody2D>().linearVelocity.magnitude, Is.LessThan(1e-4f), "paused: speed = 0");

            // Enter: paused = false, state_leave, invulnerable, the panel gone. Put the ship mid-room flying right so it has to turn.
            var body = ship.GetComponent<Rigidbody2D>();
            var middle = arena.ToWorld(arena.State.Width / 2f, arena.State.Height / 2f);
            ship.transform.position = new Vector3(middle.x, middle.y, ship.transform.position.z);
            body.position = middle;
            Assert.That(arena.ContinueFromResults(), Is.True);
            Assert.That(arena.Results, Is.Null, "instance_destroy()");
            Assert.That(arena.Hud.Q<LevelResultsView>(LevelResultsView.ElementName), Is.Null);
            Assert.That(Gml.Paused, Is.False);
            Assert.That(ship.State, Is.EqualTo(PlayerFlightState.Leaving));
            Assert.That(ship.GetComponent<PlayerHealth>().IsInvulnerable, Is.True);
            Assert.That(arena.State.PlayerLeaving, Is.True);
            Assert.That(arena.ContinueFromResults(), Is.False, "no panel left to press Enter on");

            // It turns up toward exit_x and flies off the top; then current_score = 0 and setup_level for level_02.
            yield return WaitFor(() => session.Level == 2, "the ship off the top and setup_level", 15f);
            Assert.That(session.Score, Is.EqualTo(0), "current_score = 0");
            Assert.That(session.IsLevelComplete, Is.False);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(name), "the original goes straight on, never to room_map");
            Assert.That(Arena().State.Setting.Level, Is.EqualTo(2));
            Assert.That(Arena().State.PlayerLeaving, Is.False);
            Assert.That(ship.State, Is.EqualTo(PlayerFlightState.Moving), "setup_level's new obj_player flies in");
            Assert.That(ship.GetComponent<PlayerHealth>().IsInvulnerable, Is.False);
            Assert.That(Gml.Paused, Is.False);
        }
    }
}
