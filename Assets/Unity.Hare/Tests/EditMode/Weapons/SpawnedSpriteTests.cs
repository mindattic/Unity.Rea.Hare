using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Enemies;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.EditMode
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
            foreach (var go in Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
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
            var go = Unity.Hare.Player.PlayerShipBuilder.Build(Vector3.zero);
            go.GetComponent<Unity.Hare.Player.PlayerShipController>().SetAimDirection(0f);
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
    }
}
