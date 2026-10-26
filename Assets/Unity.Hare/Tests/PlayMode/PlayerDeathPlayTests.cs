using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Unity.Hare.Arena;
using Unity.Hare.Effects;
using Unity.Hare.Flow;
using Unity.Hare.Player;
using Unity.Hare.Session;
using Unity.Hare.UI;

namespace Unity.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T54, played through the room flow: the ship in room_gamescreen spends the session's LIVES whether it dies
    /// or flies out past the buffer zone; a hit leaves obj_losthealth on the HUD where the lost pip was; at 0 HP the death
    /// particles scatter where it was, it waits out alarm_death_wait and reset_player drops it in above the room's middle;
    /// with no lives left the game restarts (a new GameSession, back at the first room).
    /// </summary>
    public class PlayerDeathPlayTests
    {
        const float FlashTime = 180f / 120f;
        const float DeathWait = 320f / 120f;

        [UnityTearDown]
        public IEnumerator LeaveNoRoomBehind()
        {
            if (RoomFlowDirector.Active != null) UnityEngine.Object.Destroy(RoomFlowDirector.Active.gameObject);
            var empty = SceneManager.CreateScene("PlayerDeathPlayTests.Empty");
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

        static RoomFlowDirector director;

        static IEnumerator EnterGameScreen()
        {
            director = RoomFlowDirector.Create(enterFirstRoom: false);
            director.GetFullscreen = () => false;
            director.SetFullscreen = _ => { };
            director.Execute(new GoToRoom(RoomFlowDirector.Seat, RoomId.GameScreen));
            string name = RoomList.GmlName(RoomId.GameScreen);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == name && Arena() != null && Arena().State.Created && Arena().Player != null,
                name + " and obj_game's Create");
        }

        static void Put(ArenaRoom arena, float x, float y)
        {
            var world = arena.ToWorld(x, y);
            var ship = arena.Player;
            ship.transform.position = new Vector3(world.x, world.y, ship.transform.position.z);
            ship.GetComponent<Rigidbody2D>().position = world;
        }

        /// <summary>Hits a perched ship in the room's middle until it dies, waiting out flash_time between hits.</summary>
        static void KillTheShip(ArenaRoom arena, PlayerHealth health)
        {
            arena.Player.Land();
            Put(arena, 1920f, 1080f);
            health.Tick(FlashTime + 0.01f); // past justspawned
            while (!health.IsDead)
            {
                Assert.That(health.TryTakeHit(), Is.True);
                if (!health.IsDead) health.Tick(FlashTime + 0.01f);
            }
        }

        [UnityTest]
        public IEnumerator LeavingTheArena_SpendsTheSessionsLives()
        {
            yield return EnterGameScreen();
            var arena = Arena();
            var health = arena.Player.GetComponent<PlayerHealth>();
            Assert.That(health.Lives, Is.EqualTo(3));
            Assert.That(director.Session.Lives, Is.EqualTo(3));

            int lifeLost = 0;
            director.SessionEventPlayed += e => { if (e is LifeLost) lifeLost++; };
            arena.Player.Land();
            Put(arena, 3840f + 450f, 1000f);
            yield return WaitFor(() => lifeLost > 0, "the buffer zone rule spending a life");
            Assert.That(director.Session.Lives, Is.EqualTo(2), "the session's LIVES -= 1");
            Assert.That(health.Lives, Is.EqualTo(2), "the ship counts the session's LIVES");
        }

        [UnityTest]
        public IEnumerator AHit_LeavesTheLostHealthPip_AndDeath_ScattersParticles_ThenDropsTheShipBackIn()
        {
            yield return EnterGameScreen();
            var arena = Arena();
            var health = arena.Player.GetComponent<PlayerHealth>();

            arena.Player.Land();
            Put(arena, 1920f, 1080f);
            health.Tick(FlashTime + 0.01f);
            Assert.That(health.TryTakeHit(), Is.True);
            Assert.That(health.Pose, Is.EqualTo(ShipPose.IdleHurt), "player_hurt's hurt sprite on a perch");
            var pip = arena.Hud.Query<LostHealthView>().ToList().Single();
            Assert.That(pip.Pip.X, Is.EqualTo(140f + 5 * 44f), "column2 + HP * width");
            Assert.That(pip.Pip.Y, Is.EqualTo(1080f - 64f - 36f).Within(0.5f));
            yield return WaitFor(() => pip.parent == null, "obj_losthealth fading out (100 steps)", 3f);

            health.Tick(FlashTime + 0.01f);
            int scattered = 0;
            Vector2 scatteredAt = default;
            Action<Vector2, int> onScatter = (at, n) => { scattered = n; scatteredAt = at; };
            DeathParticle.Scattered += onScatter;
            try
            {
                KillTheShip(arena, health);
            }
            finally
            {
                DeathParticle.Scattered -= onScatter;
            }
            Assert.That(health.IsDead, Is.True);
            Assert.That(scattered, Is.InRange(47, 72), "spawn_death_particles: 3-6 large, 12-18 medium, 32-48 small");
            Assert.That(Vector2.Distance(scatteredAt, arena.ToWorld(1920f, 1080f)), Is.LessThan(0.05f), "where the ship died");
            Assert.That(director.Session.Lives, Is.EqualTo(2), "LIVES -= 1");
            Assert.That(arena.Player.State, Is.EqualTo(PlayerFlightState.Dead));

            bool respawned = false;
            health.Respawned += () => respawned = true;
            health.Tick(DeathWait - 0.1f);
            Assert.That(health.IsDead, Is.True, "still waiting out alarm_death_wait");
            health.Tick(0.2f);
            Assert.That(respawned, Is.True);
            var at = arena.PlayerRoomPosition();
            Assert.That(at.x, Is.EqualTo(1920f).Within(1f), "reset_player: x = room_width / 2");
            Assert.That(at.y, Is.EqualTo(-96f).Within(1f), "y = -sprite_get_height(asset_leap)");
            Assert.That(arena.Player.State, Is.EqualTo(PlayerFlightState.Moving));
            Assert.That(arena.Player.Speed, Is.EqualTo(5f));
            Assert.That(Mathf.Abs(arena.Player.Direction - 270f), Is.LessThanOrEqualTo(30f));
            Assert.That(arena.Player.Direction, Is.EqualTo(Mathf.Round(arena.Player.Direction)), "irandom_range: whole degrees");
            Assert.That(health.Health, Is.EqualTo(6));
            Assert.That(health.IsSpawnProtected, Is.True, "justspawned");
            Assert.That(health.Tint, Is.EqualTo(Color.white), "reset_player: color = c_white");

            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(arena.PlayerRoomPosition().y, Is.GreaterThan(0f), "flying down into the room");
        }

        [UnityTest]
        public IEnumerator DyingWithNoLivesLeft_RestartsTheGame()
        {
            yield return EnterGameScreen();
            var arena = Arena();
            var health = arena.Player.GetComponent<PlayerHealth>();
            var first = director.Session;
            for (int i = 0; i < 2; i++) director.ExecuteSession(new SpendLife(RoomFlowDirector.Seat));
            Assert.That(health.Lives, Is.EqualTo(1));

            bool restarted = false;
            director.GameRestarted += () => restarted = true;
            KillTheShip(arena, health);
            Assert.That(director.Session.Lives, Is.EqualTo(0));
            Assert.That(restarted, Is.False, "the death wait runs first");

            health.Tick(DeathWait + 0.01f);
            Assert.That(restarted, Is.True, "game_restart");
            Assert.That(director.Session, Is.Not.SameAs(first), "a new GameSession");
            Assert.That(director.Session.Lives, Is.EqualTo(3));
            string startup = RoomList.GmlName(director.Flow.Table.FirstRoom);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == startup, "the first room");
        }
    }
}
