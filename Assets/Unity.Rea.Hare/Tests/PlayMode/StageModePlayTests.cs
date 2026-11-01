using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Rea.Hare.Arena;
using Unity.Rea.Hare.Enemies;
using Unity.Rea.Hare.Flow;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Tests.PlayMode
{
    /// <summary>
    /// U.HARE-T21, played: the two stage rooms beside the open arena. room_sidescroll (level_05, 12000x1080) and
    /// room_shaft (level_04, 1920x12000) each play one ship (the original's rooms also pre-place an obj_player that
    /// obj_game's setup_level cannot clear, BIBLE D19), flown in by reset_player at room_width / 2 above the room;
    /// view 0 follows it with no speed limit; the side-scroll wraps it sideways and scatters everything with
    /// random_positions, and costs a life off its top or bottom; the shaft wraps it vertically past the buffer zone
    /// without scattering; and a bullet or rocket goes only past the 400 px buffer zone of the room it is in, so a shot
    /// crosses the whole 12000 px stage.
    /// </summary>
    public class StageModePlayTests : InputTestFixture
    {
        [UnityTearDown]
        public IEnumerator LeaveNoRoomBehind()
        {
            yield return PlayModeRooms.TearDown();
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

        static IEnumerator Enter(RoomId room)
        {
            var director = RoomFlowDirector.Create(enterFirstRoom: false);
            director.GetFullscreen = () => false;
            director.SetFullscreen = _ => { };
            director.Execute(new GoToRoom(RoomFlowDirector.Seat, room));
            string name = RoomList.GmlName(room);
            yield return WaitFor(() => SceneManager.GetActiveScene().name == name && Arena() != null && Arena().State.Created && Arena().Player != null,
                name + " and obj_game's Create");
        }

        /// <summary>Takes the level's enemies and rocks away so nothing hurts the ship or meets a shot during the test.</summary>
        static IEnumerator Quiet(ArenaRoom arena)
        {
            EnemyDirector.ClearAll();
            arena.Rocks.ClearAll();
            yield return null;
        }

        static void Put(ArenaRoom arena, float x, float y)
        {
            var world = arena.ToWorld(x, y);
            var ship = arena.Player;
            ship.transform.position = new Vector3(world.x, world.y, ship.transform.position.z);
            ship.GetComponent<Rigidbody2D>().position = world;
        }

        [UnityTest]
        public IEnumerator TheSideScroll_PlaysOneShip_FlownInAboveItsMiddle_AndView0KeepsUpWithIt()
        {
            yield return Enter(RoomId.SideScroll);
            var arena = Arena();
            Assert.That(ArenaRoom.Active, Is.SameAs(arena));
            Assert.That(arena.State.Setting.Script, Is.EqualTo("level_05"));
            Assert.That((arena.State.Width, arena.State.Height), Is.EqualTo((12000, 1080)));
            Assert.That(arena.Layers[0].sprite.texture.name, Is.EqualTo("bg_tropicalocean"));
            Assert.That(UnityEngine.Object.FindObjectsByType<PlayerShipController>(FindObjectsSortMode.None).Length, Is.EqualTo(1),
                "one ship: not setup_level's and the room's pre-placed obj_player (D19)");

            var at = arena.PlayerRoomPosition();
            Assert.That(at.x, Is.EqualTo(6000f).Within(80f), "reset_player: x = room_width / 2");
            Assert.That(at.y, Is.LessThan(200f), "dropping in from just above the room");

            yield return Quiet(arena);
            arena.Player.Land();
            Put(arena, 9000f, 500f);
            yield return WaitFor(() => arena.State.View.X == 9000f - 960f, "view 0 centring on the ship");
            Assert.That(arena.State.View.Y, Is.EqualTo(0f), "the 1080 px room is exactly one view tall");
        }

        [UnityTest]
        public IEnumerator TheSideScroll_WrapsSidewaysScatteringEverything_AndCostsALifeOffItsBottom()
        {
            yield return Enter(RoomId.SideScroll);
            var arena = Arena();
            yield return Quiet(arena);
            var health = arena.Player.GetComponent<PlayerHealth>();
            int lives = health.Lives;
            int wraps = 0, scatters = 0, lost = 0;
            arena.EventPlayed += e =>
            {
                if (e is PlayerWrapped) wraps++;
                if (e is PositionsScattered) scatters++;
                if (e is PlayerOutOfBounds) lost++;
            };

            arena.Player.Land();
            Put(arena, 12000f + 150f, 500f);
            yield return WaitFor(() => wraps > 0, "the loop_horizontal wrap");
            Assert.That(arena.PlayerRoomPosition().x, Is.EqualTo(-96f).Within(1f), "x = -sprite_width");
            Assert.That(scatters, Is.EqualTo(1), "level_05: randomize_on_loop runs random_positions");
            Assert.That(health.Lives, Is.EqualTo(lives));

            Put(arena, 3000f, 1080f + 450f);
            yield return WaitFor(() => lost > 0, "the vertical buffer zone rule");
            Assert.That(health.Lives, Is.EqualTo(lives - 1), "loop_vertical is false: LIVES -= 1");
            Assert.That(arena.PlayerRoomPosition().x, Is.EqualTo(6000f).Within(80f), "reset_player");
        }

        [UnityTest]
        public IEnumerator TheShaft_WrapsTheShipVerticallyPastTheBufferZone_WithoutScattering()
        {
            yield return Enter(RoomId.Shaft);
            var arena = Arena();
            Assert.That((arena.State.Width, arena.State.Height), Is.EqualTo((1920, 12000)));
            Assert.That(UnityEngine.Object.FindObjectsByType<PlayerShipController>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(arena.ReflectionView, Is.Null, "only room_sidescroll has an obj_surface_reflection");
            yield return Quiet(arena);
            var health = arena.Player.GetComponent<PlayerHealth>();
            int lives = health.Lives;
            int wraps = 0, scatters = 0;
            arena.EventPlayed += e =>
            {
                if (e is PlayerWrapped) wraps++;
                if (e is PositionsScattered) scatters++;
            };

            arena.Player.Land();
            Put(arena, 960f, -450f);
            yield return WaitFor(() => wraps > 0, "the loop_vertical wrap");
            Assert.That(arena.PlayerRoomPosition().y, Is.EqualTo(12000f + 96f).Within(1f), "y = room_height + sprite_height");
            Assert.That(scatters, Is.EqualTo(0), "level_04: randomize_on_loop is false");
            Assert.That(health.Lives, Is.EqualTo(lives));
            yield return WaitFor(() => arena.State.View.Y == 12000f - 1080f, "view 0 following to the shaft's bottom");
        }

        [UnityTest]
        public IEnumerator TheSideScrollsWater_ReflectsTheShip_SplashesUnderIt_AndIsAFloor()
        {
            // U.HARE-T58: obj_surface_reflection, 250 px above room_sidescroll's bottom.
            yield return Enter(RoomId.SideScroll);
            var arena = Arena();
            yield return Quiet(arena);
            Assert.That(arena.ReflectionView, Is.Not.Null, "room_sidescroll places an obj_surface_reflection");
            int skims = 0, splashes = 0;
            ShipSkimmedSurface skim = null;
            arena.EventPlayed += e =>
            {
                if (e is ShipSkimmedSurface s) { skims++; skim = s; }
                if (e is SurfaceSplashed) splashes++;
            };

            // Perched high above the water, the reflection hangs as far below the line as the ship is above it.
            arena.Player.Land();
            Put(arena, 3000f, 630f);
            yield return WaitFor(() => Mathf.Approximately(arena.State.Reflection.Y, 1030f), "the reflection following the ship");
            yield return null;
            var drawn = arena.ReflectionView.Sprite;
            Assert.That(drawn.enabled, Is.True);
            Assert.That(drawn.sprite, Is.SameAs(arena.Player.GetComponent<PlayerLook>().Body.sprite), "player.sprite_index, frame 0");
            Assert.That(drawn.color.a, Is.EqualTo(0.3f).Within(1e-4f), "alpha = 0.3");
            Assert.That((Vector2)drawn.transform.localScale, Is.EqualTo(new Vector2(1f, 0.5f)), "xscale 1, yscale 0.5");
            Vector2 expected = arena.ToWorld(3000f, 1030f);
            Assert.That(Vector2.Distance(drawn.transform.position, expected), Is.LessThan(0.01f));
            Assert.That(drawn.transform.eulerAngles.z, Is.EqualTo(arena.Player.Facing).Within(0.01f), "angle = player.image_angle");
            Assert.That(skims, Is.EqualTo(0));

            // Below the line, the ship is put back on it, skimming level, and the water splashes every 5 steps.
            Put(arena, 3000f, 900f);
            yield return WaitFor(() => skims > 0, "the water line's floor");
            Assert.That(skim.Y, Is.EqualTo(830f), "player.y = surface_y");
            Assert.That(skim.Direction == 0f || skim.Direction == 180f, Is.True, "direction > 270 ? 0 : 180");
            Assert.That(arena.Player.Facing, Is.EqualTo(skim.Direction).Within(0.01f), "image_angle = direction");
            Assert.That(arena.PlayerRoomPosition().y, Is.LessThanOrEqualTo(830.5f), "the ship is never left under the water");
            yield return WaitFor(() => splashes > 0, "Alarm 0's ef_firework on the water line");
        }

        static Bullet StillBullet(ArenaRoom arena, float x, float y)
        {
            var go = new GameObject("TestBullet", typeof(Rigidbody2D), typeof(CircleCollider2D));
            go.transform.position = arena.ToWorld(x, y);
            var bullet = go.AddComponent<Bullet>();
            bullet.Init(null, 0f, 0f, 1, 0.01f, Gml.Px(4f)); // a lifetime that lapses at once: only the room may end it
            return bullet;
        }

        [UnityTest]
        public IEnumerator ABulletGoesOnlyPastItsRoomsBufferZone_SoItCrossesTheWholeSideScroll()
        {
            yield return Enter(RoomId.SideScroll);
            var arena = Arena();
            yield return Quiet(arena);

            var deep = StillBullet(arena, 11000f, 500f);
            var buffered = StillBullet(arena, 12000f + 350f, 500f);
            var gone = StillBullet(arena, 12000f + 450f, 500f);
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(0.2f);
            Assert.That(deep != null, Is.True, "11000 px in: inside the room, whatever room_gamescreen's size");
            Assert.That(buffered != null, Is.True, "inside the 400 px buffer zone");
            Assert.That(gone == null, Is.True, "x - buffer_zone > room_width: instance_destroy");
            UnityEngine.Object.Destroy(deep.gameObject);
            UnityEngine.Object.Destroy(buffered.gameObject);
        }

        [UnityTest]
        public IEnumerator ARocketGoesOncePastTheShaftsBufferZone()
        {
            yield return Enter(RoomId.Shaft);
            var arena = Arena();
            yield return Quiet(arena);

            var go = new GameObject("TestRocket", typeof(Rigidbody2D), typeof(CircleCollider2D));
            var rocket = go.AddComponent<Rocket>();
            rocket.Init(arena.ToWorld(960f, 12000f + 380f), 270f); // heading down, 20 px inside the buffer zone
            yield return new WaitForSeconds(0.2f);
            Assert.That(rocket != null, Is.True, "not yet lit (50 steps), still inside the buffer zone");
            yield return WaitFor(() => rocket == null, "obj_rocket's room check once it is lit and moving");
        }
    }
}
