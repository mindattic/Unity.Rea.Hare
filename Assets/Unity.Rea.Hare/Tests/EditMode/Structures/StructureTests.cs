using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Arena;
using Unity.Hare.Effects;
using Unity.Hare.Enemies;
using Unity.Hare.Player;
using Unity.Hare.Rocks;
using Unity.Hare.Structures;
using Unity.Hare.Weapons;
using static Unity.Hare.Tests.EditMode.EnemyTestKit;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>
    /// U.HARE-T56: par_structure and par_structure_cover as setup_level places them, the bullets, rockets and laser they
    /// stop, the ship perching on one through <see cref="IPerchSurface"/>, the cover following its structure, and
    /// setup_level's Clear Objects (<see cref="LevelReset"/>) taking them away with the rocks, enemies, projectiles and
    /// pickups.
    /// </summary>
    public class StructureTests
    {
        readonly List<TinyExplosion> puffs = new List<TinyExplosion>();
        readonly List<GmlEffectBurst> effects = new List<GmlEffectBurst>();

        void OnPuff(TinyExplosion t) => puffs.Add(t);
        void OnEffect(GmlEffectBurst b) => effects.Add(b);

        [SetUp]
        public void SetUp()
        {
            Random.InitState(5656);
            Gml.Paused = false;
            puffs.Clear();
            effects.Clear();
            TinyExplosion.Spawned += OnPuff;
            GmlEffects.Created += OnEffect;
        }

        [TearDown]
        public void TearDown()
        {
            TinyExplosion.Spawned -= OnPuff;
            GmlEffects.Created -= OnEffect;
            DestroyAll<StructureField>();
            DestroyAll<Structure>();
            DestroyAll<StructureCover>();
            DestroyAll<Rock>();
            DestroyAll<Mine>();
            DestroyAll<ShieldWall>();
            Cleanup();
        }

        static Structure Upright(Vector2 at) => Structure.Create(at, 0f);

        // ---- Create and the cover ----

        [Test]
        public void CreateTurnsItWithinThirtyDegreesUnderItsCover()
        {
            var angles = new HashSet<int>();
            for (int i = 0; i < 80; i++)
            {
                var structure = Structure.Create(Vector2.zero);
                float signed = Mathf.DeltaAngle(0f, structure.ImageAngle);
                Assert.That(signed, Is.InRange(-30f, 30f), "image_angle = 0 + irandom_range(-30, 30)");
                Assert.AreEqual(Mathf.Round(signed), signed, 1e-3f, "whole degrees");
                angles.Add(Mathf.RoundToInt(signed));

                var cover = structure.Cover;
                Assert.IsNotNull(cover, "setup_level: structure.cover = cover");
                Assert.AreSame(structure, cover.Parent, "cover.parent = structure");
                Assert.AreEqual(structure.ImageAngle, Mathf.Repeat(cover.transform.eulerAngles.z, 360f), 1e-3f, "cover.image_angle = structure.image_angle");
                Assert.AreEqual((Vector2)structure.transform.position, (Vector2)cover.transform.position);
                structure.Clear();
            }
            Assert.Greater(angles.Count, 20, "the angle is rolled");
        }

        [Test]
        public void TheCoverDrawsOverTheStructureAndTheShip()
        {
            var structure = Upright(Vector2.zero);
            Assert.AreEqual(HareArt.SortingOrderForDepth(0), structure.Renderer.sortingOrder, "par_structure depth 0");
            Assert.AreEqual(HareArt.SortingOrderForDepth(-1), structure.Cover.Renderer.sortingOrder, "par_structure_cover depth -1");
            Assert.Greater(structure.Cover.Renderer.sortingOrder, structure.Renderer.sortingOrder, "the cover hides the structure");
            Assert.Greater(structure.Cover.Renderer.sortingOrder, HareArt.SortingOrderForDepth(0), "and draws over obj_player (depth 0)");
            Assert.IsNull(structure.Cover.GetComponent<Collider2D>(), "the cover has no mask");
            Assert.IsNotNull(structure.GetComponent<Collider2D>(), "the structure's mask is what is hit and landed on");
            if (structure.Renderer.sprite != null)
            {
                Assert.AreEqual(Structure.SpriteName, structure.Renderer.sprite.name.Replace("_0", ""));
                Assert.AreEqual(StructureCover.SpriteName, structure.Cover.Renderer.sprite.name.Replace("_0", ""));
            }
        }

        [Test]
        public void TheCoverFollowsItsStructure()
        {
            var structure = Upright(Vector2.zero);
            structure.MoveTo(new Vector2(3f, -2f));
            Assert.AreEqual(new Vector2(3f, -2f), (Vector2)structure.Cover.transform.position, "fixed: random_positions takes the cover along");
        }

        // ---- Collisions ----

        [Test]
        public void ABulletIsSpentInATinyExplosionAndTheStructureIsUntouched()
        {
            var structure = Upright(Vector2.zero);
            var ship = Ship(new Vector2(-8f, 0f));
            var bullet = ShipRound(ship, new Vector2(-0.5f, 0f), 0f);
            Assert.IsTrue(bullet.TryHit(structure.GetComponent<Collider2D>()));
            Assert.IsTrue(bullet == null, "with(other) instance_destroy()");
            Assert.AreEqual(1, puffs.Count);
            Assert.AreEqual(new Vector2(-0.5f, 0f), (Vector2)puffs[0].transform.position, "obj_tinyexplosion at other.x, other.y");
            Assert.IsFalse(structure.IsGone);
            Assert.AreEqual(Vector2.zero, structure.Position);

            var enemyRound = EnemyRound(new Vector2(0.5f, 0f), 180f);
            Assert.IsTrue(enemyRound.TryHit(structure.GetComponent<Collider2D>()), "any obj_bullet");
            Assert.AreEqual(2, puffs.Count);
        }

        [Test]
        public void ARocketGoesOffWhereItIs()
        {
            var structure = Upright(Vector2.zero);
            var rocket = FireRocket(new Vector2(-0.5f, 0f), 0f);
            rocket.Touch(structure.GetComponent<Collider2D>());
            Assert.IsTrue(rocket == null, "with(other) instance_destroy()");
            var explosion = Object.FindAnyObjectByType<RocketExplosion>();
            Assert.IsNotNull(explosion, "obj_rocket_explosion");
            Assert.AreEqual(new Vector2(-0.5f, 0f), (Vector2)explosion.transform.position, "at other.x, other.y");
            Assert.IsFalse(structure.IsGone);
        }

        [Test]
        public void TheLaserIsDrawnOnlyUpToAStructure()
        {
            var structure = Upright(Vector2.zero);
            var blocker = Gml.FirstAlong<IBeamBlocker>(new Vector2(-5f, 0f), new Vector2(5f, 0f), out var at);
            Assert.AreSame(structure, blocker, "draw_beam: collision_point(xx, yy, par_structure, 1, 1)");
            Assert.Less(at.x, 0f);
            Assert.Greater(at.x, -2f);
        }

        // ---- Landing ----

        [Test]
        public void AFlyingShipPerchesOnAStructureWithoutRidingIt()
        {
            var structure = Upright(Vector2.zero);
            var ship = Ship(new Vector2(0f, 2.4f));
            ship.ResetState(); // reset_player: flying straight down
            ship.InViewCheck = _ => false; // par_structure's landing has no in_view check
            for (int i = 0; i < 240 && ship.State == PlayerFlightState.Moving; i++)
            {
                Vector2 before = ship.transform.position;
                ship.Step(Step);
                if (ship.State == PlayerFlightState.Moving)
                    ship.transform.position = before + ship.GetComponent<Rigidbody2D>().linearVelocity * Step;
            }

            Assert.AreEqual(PlayerFlightState.Idle, ship.State, "obj_player's par_structure collision: state_idle");
            Assert.AreSame(structure, ship.Perch, "current_structure");
            Assert.AreEqual(((IPerchSurface)structure).SurfaceId, ship.GetComponent<PlayerSeat>().Ship.Flight.CurrentStructure);
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(ship.Facing, 90f)), 45f, "image_angle = normal_detect's normal, up off its top");
            Assert.IsFalse(PerchProbe.PerchMaskTouches(ship.transform.position, ship.Facing, structure.GetComponent<Collider2D>()),
                "moved out of collision along the normal");
            Assert.IsTrue(effects.Any(e => e.Kind == GmlEffectKind.Firework), "kick up dirt: ef_firework");
            Assert.AreEqual(0, Pickups.PickupDirector.Field.Scraps.Count, "no spawn_chips off a structure");

            Vector2 perched = ship.transform.position;
            for (int i = 0; i < 30; i++) ship.Step(Step);
            Assert.AreEqual(perched, (Vector2)ship.transform.position, "a structure never moves, so neither does the ship");
        }

        // ---- The field and the level reset ----

        [Test]
        public void TheFieldPlacesStructureMinToMaxInsideTheRoom()
        {
            var room = new Rect(-9.6f, -10.8f, 19.2f, 21.6f);
            var field = StructureField.CreateField(room);
            var counts = new HashSet<int>();
            for (int i = 0; i < 40; i++)
            {
                field.Setup(4, 6);
                // move_rocks's structure half may take some away from over the spawn point.
                Assert.That(field.Structures.Count, Is.InRange(0, 6));
                counts.Add(field.Structures.Count);
                foreach (var s in field.Structures)
                {
                    Assert.IsNotNull(s.Cover);
                    Assert.AreSame(field, s.Field);
                    Assert.That(s.Position.x, Is.InRange(room.xMin + 0.96f - 1e-4f, room.xMax - 0.96f + 1e-4f), "irandom_range(player_size, room_width - player_size)");
                    Assert.That(s.Position.y, Is.InRange(room.yMin + 0.96f - 1e-4f, room.yMax - 0.96f + 1e-4f));
                    bool overSpawn = s.Position.y > room.yMax - 8f && Mathf.Abs(s.Position.x - room.center.x) <= 4f;
                    Assert.IsFalse(overSpawn, "move_rocks: y < 800 within 400 px of room_width / 2 is cleared");
                }
                Assert.AreEqual(field.Structures.Count, Object.FindObjectsByType<StructureCover>().Length,
                    "the last setup's structures and covers went before these");
            }
            Assert.That(counts.Max(), Is.GreaterThanOrEqualTo(4));

            field.Setup(0, 0);
            Assert.AreEqual(0, Object.FindObjectsByType<Structure>().Length);
            Assert.AreEqual(0, Object.FindObjectsByType<StructureCover>().Length);
        }

        [Test]
        public void TheSpawnPointIsClearedOfStructuresAndTheirCovers()
        {
            var room = new Rect(-9.6f, -10.8f, 19.2f, 21.6f);
            var field = StructureField.CreateField(room);
            field.Setup(0, 0);
            var overSpawn = field.Place(field.FromGml(960f, 300f));
            var aside = field.Place(field.FromGml(200f, 300f));
            var below = field.Place(field.FromGml(960f, 1500f));
            var cover = overSpawn.Cover;

            field.MoveStructures();
            Assert.IsTrue(overSpawn == null, "with(cover) instance_destroy(); instance_destroy()");
            Assert.IsTrue(cover == null);
            Assert.IsFalse(aside == null);
            Assert.IsFalse(below == null);
            Assert.AreEqual(2, field.Structures.Count);
        }

        [Test]
        public void TheLevelResetTakesEverythingAway()
        {
            var structure = Upright(new Vector2(5f, 5f));
            var cover = structure.Cover;
            var rock = Rock.Create(new Vector2(-5f, 5f), 100, null, 0);
            var ship = Ship(new Vector2(-8f, -8f));
            ShipRound(ship, new Vector2(-6f, -6f));
            EnemyRound(new Vector2(-6f, -5f));
            FireRocket(new Vector2(-6f, -4f));
            new GameObject("Mine").AddComponent<Mine>();
            var hydra = Spawn<Hydra>();
            hydra.Init(new Vector2(4f, -4f), Gml.Room, ship);
            Pickups.PickupDirector.DropRandom(new Vector2(2f, 2f));
            Pickups.PickupDirector.Drop(Pickups.PickupKind.Carrot, new Vector2(1f, 1f));
            Assert.Greater(Pickups.PickupDirector.Field.Pickups.Count, 0);

            LevelReset.ClearObjects();

            Assert.IsTrue(structure == null && cover == null, "with(par_structure) and with(par_structure_cover) instance_destroy()");
            Assert.IsTrue(rock == null, "with(par_rock)");
            Assert.AreEqual(0, Count<Rock>(), "no create_new_rock for a cleared rock");
            Assert.AreEqual(0, Count<Bullet>(), "with(par_projectile): obj_bullet");
            Assert.AreEqual(0, Count<Rocket>(), "obj_rocket");
            Assert.AreEqual(0, Count<Mine>(), "obj_mine");
            Assert.IsTrue(hydra == null, "with(par_enemy)");
            Assert.AreEqual(0, EnemyDirector.World.Enemies.Count(e => !e.IsDead));
            Assert.AreEqual(0, Pickups.PickupDirector.Field.Pickups.Count, "with(par_pickup)");
            Assert.IsFalse(ship == null, "the ship is the respawn flow's");
        }
    }
}
