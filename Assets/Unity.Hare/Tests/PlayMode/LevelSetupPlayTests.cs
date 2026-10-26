using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Unity.Hare.Arena;
using Unity.Hare.Flow;
using Unity.Hare.Player;
using Unity.Hare.Rocks;
using Unity.Hare.Session;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T57, played: obj_game's setup_level in room_gamescreen places level_01's rocks (clear of the spawn point),
    /// its vortices and mimics, flies the ship in from above the room's middle and puts obj_levelstart's "Hydras &amp; Saws" /
    /// "Accumulate 500 points" on the HUD, which fades away on the room clock; level_05's time limit beeps its last ten
    /// seconds and completes the level (paused); generator mode's wormhole emitter completes the level when its list is
    /// spent; and the room flow's fade-in advances the level, level_04 sending the game to room_shaft.
    /// </summary>
    public class LevelSetupPlayTests
    {
        RoomFlowDirector director;

        [UnityTearDown]
        public IEnumerator LeaveNoRoomBehind()
        {
            Gml.Paused = false;
            if (RoomFlowDirector.Active != null) UnityEngine.Object.Destroy(RoomFlowDirector.Active.gameObject);
            var empty = SceneManager.CreateScene("LevelSetupPlayTests.Empty");
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != empty) yield return SceneManager.UnloadSceneAsync(scene);
            }
        }

        static IEnumerator WaitFor(Func<bool> condition, string what, float seconds = 5f)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > until) Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        static ArenaRoom Arena() => UnityEngine.Object.FindAnyObjectByType<ArenaRoom>();

        IEnumerator Enter(RoomId room)
        {
            director = RoomFlowDirector.Create(enterFirstRoom: false);
            director.GetFullscreen = () => false;
            director.SetFullscreen = _ => { };
            director.Execute(new GoToRoom(RoomFlowDirector.Seat, room));
            string name = RoomList.GmlName(room);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == name && Arena() != null && Arena().State != null && Arena().State.Created && Arena().Player != null,
                name + " and obj_game's Create");
        }

        [UnityTest]
        public IEnumerator Level1_PlacesItsRocksVorticesAndMimics_AndFliesTheShipIn()
        {
            yield return Enter(RoomId.GameScreen);
            var arena = Arena();
            var session = director.Session;
            Assert.That(arena.State.Setting.Script, Is.EqualTo("level_01"));

            // Generate Rocks: irandom_range(15, 30), less what move_rocks cleared over the spawn point.
            var rocks = arena.Rocks.Rocks.Where(r => r != null && !r.IsGone).ToList();
            Assert.That(rocks.Count, Is.InRange(1, 30));
            foreach (var rock in rocks)
            {
                var at = arena.ToRoom(rock.Position);
                Assert.That(at.y < RockField.SpawnClearDepthPx && Mathf.Abs(at.x - arena.State.Width / 2f) <= RockField.SpawnClearHalfWidthPx, Is.False,
                    "move_rocks cleared the spawn point");
            }

            // hydra 1-3, saw 1-2, dagger 1-2 as par_enemy_emitter; mimic 1-3 made directly.
            Assert.That(arena.Enemies.Emitters.Count, Is.InRange(3, 7));
            Assert.That(arena.Enemies.Mimics.Count, Is.InRange(1, 3));

            // The ship: a new obj_player flown in by reset_player.
            var health = arena.Player.GetComponent<PlayerHealth>();
            Assert.That(health.IsDead, Is.False);
            Assert.That(health.Health, Is.EqualTo(health.MaxHealth));
            Assert.That(health.RespawnPoint, Is.SameAs(arena.SpawnPoint));

            // The level script's objective.
            Assert.That(session.Objective, Is.EqualTo(ObjectiveKind.Points));
            Assert.That(session.GoalScore, Is.EqualTo(500));
            Assert.That(session.LevelName, Is.EqualTo("Hydras & Saws"));
            Assert.That(session.IsPaused, Is.False);
            Assert.That(Gml.Paused, Is.False);
        }

        [UnityTest]
        public IEnumerator TheLevelStartBannerShowsTheLevelAndFadesOnTheRoomClock()
        {
            yield return Enter(RoomId.GameScreen);
            var arena = Arena();
            var banner = arena.Banner;
            Assert.That(banner, Is.Not.Null, "setup_level: instance_create(0,0, obj_levelstart)");
            Assert.That(banner.LevelName, Is.EqualTo("Hydras & Saws"));
            Assert.That(banner.Objective, Is.EqualTo("Accumulate 500 points"));
            var view = arena.Hud.Q<LevelStartView>(LevelStartView.ElementName);
            Assert.That(view, Is.Not.Null, "drawn on the GUI layer");
            Assert.That(view.NameLine.Q<Label>().text, Is.EqualTo("Hydras & Saws"));
            Assert.That(view.ObjectiveLine.Q<Label>().text, Is.EqualTo("Accumulate 500 points"));
            Assert.That(view.NameLine.style.top.value.value, Is.EqualTo(540f), "y = screen_height / 2");
            Assert.That(view.ObjectiveLine.style.top.value.value, Is.EqualTo(605f), "y + 65");

            // The room clock: 120 steps before the fade, then a hundredth every other step.
            for (int i = 0; i < 400 && arena.Banner != null; i++) arena.Step();
            Assert.That(arena.Banner, Is.Null, "faded out and destroyed");
            Assert.That(banner.Gone, Is.True);
            Assert.That(arena.Hud.Q<LevelStartView>(LevelStartView.ElementName), Is.Null);
        }

        [UnityTest]
        public IEnumerator Level5sTimeLimitBeepsItsLastTenSecondsThenCompletesTheLevel()
        {
            yield return Enter(RoomId.SideScroll);
            var arena = Arena();
            var session = director.Session;
            Assert.That(arena.State.Setting.Script, Is.EqualTo("level_05"));
            Assert.That(session.Objective, Is.EqualTo(ObjectiveKind.TimeLimit));
            Assert.That(session.ObjectiveText, Is.EqualTo("Survive 1 minute, 30 seconds"));

            var warnings = new List<int>();
            int completed = 0;
            director.SessionEventPlayed += e => { if (e is TimeLimitWarning w) warnings.Add(w.SecondsLeft); };
            arena.LevelCompleted += _ => completed++;

            // Ninety seconds of room steps, run here in one go.
            for (int i = 0; i < 120 * 91 && !session.IsLevelComplete; i++) arena.Step();
            Assert.That(session.IsLevelComplete, Is.True, "time_elapsed_seconds >= time_limit_seconds: set_level_complete");
            Assert.That(completed, Is.EqualTo(1));
            Assert.That(warnings.Distinct().OrderByDescending(s => s).ToArray(), Is.EqualTo(Enumerable.Range(1, 10).Reverse().ToArray()), "sfx_beep at 10..1 seconds left");
            Assert.That(session.IsPaused, Is.True);
            Assert.That(Gml.Paused, Is.True, "set_level_complete: paused = true");
        }

        [UnityTest]
        public IEnumerator GeneratorMode_TheWormholeEmitterCompletesTheLevelWhenItsListIsSpent()
        {
            yield return Enter(RoomId.GameScreen);
            var arena = Arena();
            director.ExecuteSession(new ChooseGenerator(RoomFlowDirector.Seat, 0)); // an empty generate_enemy_list
            arena.Begin(director.Session.Level);
            var session = director.Session;
            Assert.That(arena.State.Setting.Script, Is.EqualTo("level_enemygenerator"));
            Assert.That(session.Objective, Is.EqualTo(ObjectiveKind.Survive));
            Assert.That(session.LevelName, Is.EqualTo("Enemy Generator"));
            Assert.That(arena.Banner.Objective, Is.EqualTo("Survive the Alien Onslaught!"));
            Assert.That(arena.Generator, Is.Not.Null, "setup_level's obj_wormhole_emitter");
            Assert.That(arena.Enemies.Emitters.Count + arena.Enemies.Mimics.Count, Is.EqualTo(0), "level_enemygenerator places no vortices");

            yield return WaitFor(() => session.IsLevelComplete, "the emitter's first alarm (at most 4 s) finding its list spent", 10f);
            Assert.That(session.IsPaused, Is.True);
            Assert.That(arena.Generator, Is.Null);
        }

        [UnityTest]
        public IEnumerator TheFadeInAdvancesTheLevel()
        {
            yield return Enter(RoomId.GameScreen);
            var session = director.Session;
            director.Execute(new StartFadeIn(RoomFlowDirector.Seat));
            yield return WaitFor(() => session.Level == 2 && Arena().State.Setting.Level == 2, "obj_fade_game: current_level += 1; setup_level()");
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(RoomList.GmlName(RoomId.GameScreen)), "level_02 plays in room_gamescreen too");
            Assert.That(session.LevelName, Is.EqualTo("Meatballs & Phalanx"));
            Assert.That(session.StationaryPercent, Is.EqualTo(25));
            Assert.That(Arena().Banner.LevelName, Is.EqualTo("Meatballs & Phalanx"));
        }

        [UnityTest]
        public IEnumerator Level4sScriptSendsTheGameToTheShaft()
        {
            yield return Enter(RoomId.GameScreen);
            var session = director.Session;
            // current_level 3, as level_03 left it (its stationary_percent 15 too).
            director.ExecuteSession(new AdvanceLevel(RoomFlowDirector.Seat, false));
            director.ExecuteSession(new AdvanceLevel(RoomFlowDirector.Seat, false));
            Arena().Begin(session.Level);
            Assert.That(Arena().State.Setting.Script, Is.EqualTo("level_03"));

            director.Execute(new StartFadeIn(RoomFlowDirector.Seat));
            string shaft = RoomList.GmlName(RoomId.Shaft);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == shaft && Arena() != null && Arena().State != null && Arena().State.Created,
                "level_04: room_goto(room_shaft)");
            Assert.That(session.Level, Is.EqualTo(4));
            Assert.That(Arena().State.Setting.Script, Is.EqualTo("level_04"));
            Assert.That(session.LevelName, Is.EqualTo("Storm Shaft"));
            Assert.That(session.StationaryPercent, Is.EqualTo(15), "level_04 keeps level_03's stationary_percent");
        }
    }
}
