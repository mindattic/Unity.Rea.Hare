using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Unity.Rea.Hare.Enemies;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Weapons;
using static Unity.Rea.Hare.Tests.EditMode.EnemyTestKit;

namespace Unity.Rea.Hare.Tests.EditMode
{
    /// <summary>UH-T40: the seam between Core's enemy world and the views that show it.</summary>
    public class EnemyDirectorTests
    {
        [SetUp] public void SetUp() => EnemyDirector.Reset(4040);
        [TearDown] public void TearDown() => Cleanup();

        static PlayerHealth ShipWithHealth(Vector2 at)
        {
            var ship = Ship(at);
            var health = ship.gameObject.AddComponent<PlayerHealth>();
            health.ResetState();
            health.Tick(10f); // spawn protection is over
            return health;
        }

        [Test]
        public void ASeedDealsTheSameEnemiesAndAnotherDoesNot()
        {
            int[] Deal(int seed)
            {
                EnemyDirector.Reset(seed);
                var hydra = Spawn<Hydra>();
                hydra.Init(Vector2.zero, Gml.Room);
                return new[] { hydra.Arms, (int)hydra.TurretAngle(0), (int)hydra.TurretAngle(1), (int)hydra.TurretTarget(0), hydra.RotationDirection };
            }

            var first = Deal(77);
            Cleanup();
            CollectionAssert.AreEqual(first, Deal(77));
            Cleanup();
            CollectionAssert.AreNotEqual(first, Deal(78));
        }

        [Test]
        public void ViewsShowWhereCoreSaysTheBodyIs()
        {
            var ship = Ship(new Vector2(5f, 0f));
            var hydra = Spawn<Hydra>();
            hydra.Init(Vector2.zero, Gml.Room, ship, 2, 1);
            Steps(hydra, 10);
            Assert.AreEqual(hydra.Core.Position.X, hydra.transform.position.x, 1e-6f);
            Assert.AreEqual(hydra.Core.ImageAngle, hydra.transform.rotation.eulerAngles.z, 1e-3f);
            Assert.AreSame(hydra, EnemyDirector.ViewOf<Hydra>(hydra.Core.Id));
        }

        [Test]
        public void ContactHurtsTheShipThroughCoreAndItsSeatPlaysTheEvents()
        {
            var health = ShipWithHealth(Vector2.zero);
            int damaged = 0;
            health.Damaged += _ => damaged++;
            var hydra = Spawn<Hydra>();
            hydra.Init(Vector2.zero, Gml.Room);
            var source = hydra.GetComponent<PlayerDamageSource>();
            Assert.IsNotNull(source.HitOverride, "an enemy's contact runs through the enemy world's HurtPlayer");

            Assert.IsTrue(source.HitOverride(health));
            Assert.AreEqual(1, damaged);
            Assert.Less(health.Health, health.MaxHealth);
            Assert.IsFalse(source.HitOverride(health), "invulnerable after a hit");
            Assert.AreEqual(1, damaged);
        }

        [Test]
        public void EnemyRoundsHurtTheShipAndAreSpentOnlyWhenTheHitLands()
        {
            var health = ShipWithHealth(Vector2.zero);
            var round = EnemyRound(Vector2.zero);
            var source = round.GetComponent<PlayerDamageSource>();
            Assert.IsTrue(source.DestroyOnHit);
            Assert.IsTrue(source.HitOverride(health));
            Assert.IsFalse(source.HitOverride(health), "the ship is invulnerable: no hit, the round flies on");
        }

        [Test]
        public void AViewDestroyedFromOutsideLeavesTheWorld()
        {
            var a = Spawn<Hydra>();
            a.Init(Vector2.zero, Gml.Room);
            var b = Spawn<Hydra>();
            b.Init(Vector2.one, Gml.Room);
            Assert.AreEqual(2, EnemySpawner.Count(EnemyKind.Hydra));
            Object.DestroyImmediate(a.gameObject);
            Assert.AreEqual(1, EnemySpawner.Count(EnemyKind.Hydra));
            Assert.AreEqual(1, EnemyDirector.World.Enemies.Count);
        }

        [Test]
        public void ChildrenOfAnEnemyGetViewsAndGoWithIt()
        {
            var phalanx = Spawn<Phalanx>();
            phalanx.Init(Vector2.zero, Gml.Room);
            Assert.IsNotNull(phalanx.Shield, "the shield was announced as a spawn and given a view");
            Assert.IsNotNull(phalanx.Shield.GetComponent<Collider2D>());

            var worm = Spawn<Worm>();
            worm.Init(Vector2.one, Gml.Room, null, 5, 0f);
            Assert.AreEqual(6, worm.Segments.Count);
            Assert.IsTrue(worm.Segments.All(s => s != null));
            Assert.AreEqual(1, EnemySpawner.Count(EnemyKind.Worm), "a worm counts its head only");
        }

        [Test]
        public void EnemiesTheDirectorMakesForAnEmitterChaseItsShip()
        {
            var ship = Ship(new Vector2(5f, 0f));
            var emitter = new GameObject("Emitter").AddComponent<EnemyEmitter>();
            emitter.Init(EnemyKind.Hydra, Vector2.zero, 0, 1, 0.25f, Gml.Room, ship, 10, 10, 100);
            for (int i = 0; i < 1 + 10 + 120 && emitter != null; i++) emitter.Tick(Step);
            var hydra = (Hydra)emitter.LastSpawned;
            Assert.IsNotNull(hydra);
            Assert.AreSame(ship, hydra.Player, "the emitter hands its ship to what it releases");
        }
    }
}
