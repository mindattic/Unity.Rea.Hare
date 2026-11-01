using NUnit.Framework;
using UnityEngine;
using Unity.Rea.Hare.Enemies;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Tests.EditMode
{
    /// <summary>
    /// Every object a weapon or an enemy spawns draws its GML sprite (U.HARE-T95): the views once held a serialized Sprite
    /// the generated scenes never set, so the pellet, the mines and others drew nothing.
    /// </summary>
    public class SpawnedSpriteTests
    {
        [TearDown]
        public void TearDown()
        {
            PerchedWeaponTestUtil.DestroyAll();
            foreach (var go in Object.FindObjectsByType<SpriteRenderer>())
                if (go != null) Object.DestroyImmediate(go.gameObject);
        }

        static void AssertDrawn(Component c, string sprite, int depth)
        {
            var renderer = c.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(renderer, c.name + " has no SpriteRenderer");
            Assert.IsNotNull(renderer.sprite, c.name + " draws no sprite (expected " + sprite + ")");
            Assert.AreEqual(-depth, renderer.sortingOrder, c.name + " depth");
        }

        [Test]
        public void ShotgunPelletsDrawSprPellet()
        {
            var shotgun = PerchedWeaponTestUtil.NewPerchedWeapon<Shotgun>();
            Assert.IsTrue(shotgun.TryFire());
            Assert.IsNotEmpty(shotgun.LastBlast);
            foreach (var pellet in shotgun.LastBlast) AssertDrawn(pellet, "spr_pellet", -3);
        }

        [Test]
        public void TheShipsRocketAndBothMinesDrawTheirSprites()
        {
            var go = Unity.Rea.Hare.Player.PlayerShipBuilder.Build(Vector3.zero);
            go.GetComponent<Unity.Rea.Hare.Player.PlayerShipController>().SetAimDirection(0f);
            var air = go.GetComponent<AirWeaponLoadout>();
            Assert.IsTrue(air.TryFire());
            AssertDrawn(Object.FindAnyObjectByType<Rocket>(), "spr_rocket_anime", -3);
            Assert.IsTrue(air.NextAirWeapon());
            Assert.IsTrue(air.TryFire());
            AssertDrawn(air.LastMine, "spr_mine", -3);
            var launcher = go.GetComponent<MineLauncher>();
            Assert.IsTrue(launcher.TryFire());
            AssertDrawn(launcher.LastMine, "spr_mine", -3);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void FlamesDrawSprPowderFire()
        {
            var flamer = PerchedWeaponTestUtil.NewPerchedWeapon<Flamethrower>();
            Assert.IsTrue(flamer.TryFire());
            AssertDrawn(flamer.LastFlame, "spr_powder_fire", -4);
        }

        [Test]
        public void EnemyRoundsDrawSprBullet()
        {
            var owner = new GameObject("Shooter");
            var round = EnemyRounds.Spawn(owner, Vector2.zero, 0f, 3f);
            AssertDrawn(round, "spr_bullet", -3);
            Object.DestroyImmediate(owner);
        }

        [TestCase(InsectKind.Butterfly)]
        [TestCase(InsectKind.Firefly)]
        [TestCase(InsectKind.Fly)]
        public void InsectsDrawTheirSprite(InsectKind kind)
        {
            var insect = InsectEmitter.Spawn(kind, new Rect(0f, 0f, 10f, 10f), new Vector2(5f, 5f));
            AssertDrawn(insect, kind.ToString(), 0);
        }

        [TestCase(EnemyKind.Hydra, -3)]
        [TestCase(EnemyKind.Sawblade, -3)]
        [TestCase(EnemyKind.Dagger, -3)]
        [TestCase(EnemyKind.Mimic, -3)]
        [TestCase(EnemyKind.Wraith, -3)]
        [TestCase(EnemyKind.Meatball, -2)]
        [TestCase(EnemyKind.Phalanx, -3)]
        [TestCase(EnemyKind.Worm, -3)]
        [TestCase(EnemyKind.Trilobyte, -3)]
        public void SetPieceEnemiesDrawTheirSprite(EnemyKind kind, int depth)
        {
            var enemy = EnemySpawner.Spawn(kind, new Vector2(5f, 5f), Gml.Room);
            AssertDrawn(enemy, kind.ToString(), depth);
        }

        [Test]
        public void TheEnemiesPartsDrawToo()
        {
            var phalanx = (Phalanx)EnemySpawner.Spawn(EnemyKind.Phalanx, Vector2.zero, Gml.Room);
            AssertDrawn(phalanx.Shield, "spr_shieldwall_anime", 0);
            var mimic = (Mimic)EnemySpawner.Spawn(EnemyKind.Mimic, Vector2.zero, Gml.Room);
            AssertDrawn(mimic.Illusion, "spr_rock", 0);
            var worm = (Worm)EnemySpawner.Spawn(EnemyKind.Worm, Vector2.zero, Gml.Room);
            Assert.IsNotNull(worm.Child);
            AssertDrawn(worm.Child, "spr_worm_segment", -3);
        }

        static SpriteRenderer Layer(Component c, string key)
        {
            var child = c.transform.Find("Layer_" + key);
            Assert.IsNotNull(child, c.name + " has no layer " + key);
            return child.GetComponent<SpriteRenderer>();
        }

        [Test]
        public void TheHydraDrawsATurretPerArmUnderItsBody()
        {
            var hydra = (Hydra)EnemySpawner.Spawn(EnemyKind.Hydra, new Vector2(5f, 5f), Gml.Room);
            int bodyOrder = hydra.GetComponent<SpriteRenderer>().sortingOrder;
            for (int i = 0; i < Hydra.MaxArms; i++)
            {
                var turret = Layer(hydra, "turret" + i);
                Assert.AreEqual(i < hydra.Arms, turret.gameObject.activeSelf, "turret " + i);
                if (i >= hydra.Arms) continue;
                Assert.IsNotNull(turret.sprite);
                Assert.Less(turret.sortingOrder, bodyOrder);
                Assert.AreEqual(hydra.TurretAngle(i), Mathf.Repeat(turret.transform.eulerAngles.z, 360f), 0.01f);
            }
        }

        [Test]
        public void TheMeatballDrawsEyesAndPupilsUnderItsBody()
        {
            var ball = (Meatball)EnemySpawner.Spawn(EnemyKind.Meatball, new Vector2(5f, 5f), Gml.Room);
            int bodyOrder = ball.GetComponent<SpriteRenderer>().sortingOrder;
            var eyes = Layer(ball, "eyes");
            var pupils = Layer(ball, "pupils");
            Assert.IsNotNull(eyes.sprite);
            Assert.IsNotNull(pupils.sprite);
            Assert.Less(eyes.sortingOrder, pupils.sortingOrder);
            Assert.Less(pupils.sortingOrder, bodyOrder);
            var expected = (Vector2)ball.transform.position + Unity.Rea.Hare.Effects.HareArt.GmlOffset(ball.EyeOffset.x, ball.EyeOffset.y);
            Assert.AreEqual(expected.x, pupils.transform.position.x, 0.001f);
            Assert.AreEqual(expected.y, pupils.transform.position.y, 0.001f);
        }

        [Test]
        public void ThePhalanxAndSawbladeDrawABackLayerAtBackAngle()
        {
            var phalanx = (Phalanx)EnemySpawner.Spawn(EnemyKind.Phalanx, new Vector2(5f, 5f), Gml.Room);
            AssertBackLayer(phalanx, phalanx.BackAngle);
            var saw = (Sawblade)EnemySpawner.Spawn(EnemyKind.Sawblade, new Vector2(7f, 5f), Gml.Room);
            AssertBackLayer(saw, saw.BackAngle);
        }

        [Test]
        public void TheWormDrawsItsHeadAndBackLayersAndSegmentsTheirBack()
        {
            var worm = (Worm)EnemySpawner.Spawn(EnemyKind.Worm, new Vector2(5f, 5f), Gml.Room);
            AssertBackLayer(worm, worm.BackAngle);
            var head = Layer(worm, "head");
            Assert.IsNotNull(head.sprite);
            Assert.Less(head.sortingOrder, Layer(worm, "back").sortingOrder);
            Assert.AreEqual(worm.JawPosition.x, head.transform.position.x, 0.001f);
            Assert.AreEqual(worm.JawPosition.y, head.transform.position.y, 0.001f);
            AssertBackLayer(worm.Child, worm.Child.BackAngle);
        }

        static void AssertBackLayer(SetPieceEnemy enemy, float backAngle)
        {
            var back = Layer(enemy, "back");
            Assert.IsNotNull(back.sprite);
            Assert.Less(back.sortingOrder, enemy.GetComponent<SpriteRenderer>().sortingOrder);
            Assert.AreEqual(backAngle, Mathf.Repeat(back.transform.eulerAngles.z, 360f), 0.01f);
        }
    }
}
