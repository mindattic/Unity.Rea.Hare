using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.EditMode
{
    /// <summary>UH-T11: the mine (obj_mine/shoot_mine), obj_grenade_explosion and next_ground_weapon.</summary>
    public class GroundWeaponTests
    {
        const float Step = 1f / 120f;

        [TearDown]
        public void TearDown()
        {
            DestroyAll<Mine>();
            DestroyAll<RocketExplosion>();
            DestroyAll<GrenadeExplosion>();
            DestroyAll<EnemyWeaponTarget>();
            PerchedWeaponTestUtil.DestroyAll();
        }

        static void DestroyAll<T>() where T : Component
        {
            foreach (var c in Object.FindObjectsByType<T>(FindObjectsSortMode.None))
                if (c != null) Object.DestroyImmediate(c.gameObject);
        }

        static Mine DropMine(Vector2 position, float facing = 0f, int fuseSeconds = 10)
        {
            var go = new GameObject("Mine");
            var mine = go.AddComponent<Mine>();
            mine.Init(null, position, facing, fuseSeconds);
            return mine;
        }

        static EnemyWeaponTarget SpawnEnemy(Vector2 position, float radius = 0.1f)
        {
            var go = new GameObject("TestEnemy");
            go.transform.position = position;
            go.AddComponent<CircleCollider2D>().radius = radius;
            return go.AddComponent<EnemyWeaponTarget>();
        }

        static void Steps(Mine mine, int steps)
        {
            for (int i = 0; i < steps && !mine.IsGone; i++) mine.Tick(Step);
        }

        static int Count<T>() where T : Object => Object.FindObjectsByType<T>(FindObjectsSortMode.None).Length;

        // ---- Mine ----

        [Test]
        public void MineDriftsAtOnePixelPerStepUntilItArmsAfterOneSecond()
        {
            var mine = DropMine(Vector2.zero, facing: 0f);
            int activations = 0;
            mine.Activated += _ => activations++;

            Steps(mine, 119);
            Assert.IsFalse(mine.IsActive);
            Assert.AreEqual(Gml.Px(119f), mine.transform.position.x, 1e-3f); // speed = 1 along image_angle

            Steps(mine, 1);
            Assert.IsTrue(mine.IsActive);
            Assert.AreEqual(1, activations); // Alarm 0: ring + sfx_beep
        }

        [Test]
        public void ArmedMineWithNoEnemyInRangeHoldsStill()
        {
            SpawnEnemy(new Vector2(10f, 0f)); // 1000 px away, outside range = 300
            var mine = DropMine(Vector2.zero);
            Steps(mine, 120);
            Vector3 armedAt = mine.transform.position;

            Steps(mine, 60);
            Assert.AreEqual(0f, mine.SpeedPxPerStep);
            Assert.AreEqual(armedAt, mine.transform.position);
        }

        [Test]
        public void ArmedMineChasesTheNearestEnemyWithinRangeAtTwoPixelsPerStep()
        {
            var enemy = SpawnEnemy(new Vector2(0f, 2f)); // 200 px straight up
            var mine = DropMine(Vector2.zero, facing: 0f);
            Steps(mine, 120);
            Vector2 armedAt = mine.transform.position;

            Steps(mine, 1);
            Assert.AreEqual(Mine.ChaseSpeedPxPerStep, mine.SpeedPxPerStep);
            Vector2 to = (Vector2)enemy.transform.position - armedAt;
            Assert.AreEqual(Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg, mine.Direction, 1e-2f); // move_towards_point
            Assert.AreEqual(Gml.Px(2f), ((Vector2)mine.transform.position - armedAt).magnitude, 1e-4f);
            // The sprite turns 3 degrees a step: once on the arming step (Alarm 0 then Step), once now.
            Assert.AreEqual(2f * Mine.TurnRateDegreesPerStep, mine.ImageAngle, 1e-3f);
        }

        [Test]
        public void MineDetonatesIntoARocketExplosionNearAnEnemy()
        {
            var enemy = SpawnEnemy(new Vector2(0f, 1f));
            var hits = new List<WeaponHit>();
            enemy.Hit += hits.Add;
            var mine = DropMine(Vector2.zero, facing: 270f); // drifts away first

            Steps(mine, 1200);
            Assert.IsTrue(mine == null, "the mine is gone");
            var explosions = Object.FindObjectsByType<RocketExplosion>(FindObjectsSortMode.None);
            Assert.AreEqual(1, explosions.Length);
            float gap = Vector2.Distance(explosions[0].transform.position, enemy.transform.position) - 0.1f - Gml.Px(Mine.RadiusPx);
            Assert.Less(gap, Gml.Px(Mine.DetonateDistancePx));

            explosions[0].Touch(enemy.GetComponent<Collider2D>());
            Assert.AreEqual(WeaponHitKind.RocketExplosion, hits[0].Kind);
        }

        [Test]
        public void MineSelfDetonatesWhenItsFuseRunsOut()
        {
            var mine = DropMine(Vector2.zero, fuseSeconds: 8);
            Steps(mine, 8 * 120 - 1);
            Assert.IsFalse(mine.IsGone);
            Steps(mine, 1);
            Assert.IsTrue(mine == null);
            Assert.AreEqual(1, Count<RocketExplosion>());
        }

        [Test]
        public void MineFuseIsEightToTenSecondsAndFacingIsRandomWholeDegrees()
        {
            for (int i = 0; i < 50; i++)
            {
                var go = new GameObject("Mine");
                var mine = go.AddComponent<Mine>();
                mine.Init(null, Vector2.zero);
                int seconds = Mathf.RoundToInt(mine.DetonateAfter);
                Assert.That(seconds, Is.InRange(Mine.MinDetonateSeconds, Mine.MaxDetonateSeconds));
                Assert.AreEqual(Mathf.Round(mine.ImageAngle), mine.ImageAngle);
                Assert.AreEqual(mine.ImageAngle, mine.Direction);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BulletDestroysTheMineAndItselfWithoutABlast()
        {
            var mine = DropMine(Vector2.zero);
            var go = new GameObject("Bullet");
            var bullet = go.AddComponent<Bullet>();
            bullet.Init(null, 0f, 8f, 1, 3f, 0.05f);

            Assert.IsTrue(bullet.TryHit(mine.GetComponent<Collider2D>()));
            Assert.IsTrue(mine == null);
            Assert.IsTrue(bullet == null);
            Assert.AreEqual(0, Count<RocketExplosion>());
        }

        // ---- Mine launcher ----

        static MineLauncher NewLauncher()
        {
            var go = new GameObject("Player");
            var ship = go.AddComponent<PlayerShipController>();
            ship.ResetState(); // in flight (Moving)
            ship.SetAimDirection(0f);
            var launcher = go.AddComponent<MineLauncher>();
            launcher.ResetState();
            return launcher;
        }

        [Test]
        public void LauncherDropsAMineFromTheMuzzleOnlyInFlight()
        {
            var launcher = NewLauncher();
            var ship = launcher.GetComponent<PlayerShipController>();
            Assert.AreEqual(WeaponSlot.Secondary, launcher.Slot); // Ctrl + left mouse

            Vector2 muzzle = launcher.MuzzlePosition;
            Assert.IsTrue(launcher.TryFire());
            Assert.IsNotNull(launcher.LastMine);
            Assert.AreEqual(muzzle, (Vector2)launcher.LastMine.transform.position);
            Assert.AreEqual(launcher.gameObject, launcher.LastMine.Owner);

            launcher.Tick(Gml.Steps(Mine.RearmSteps));
            ship.Land();
            Assert.IsFalse(launcher.TryFire()); // perched: fire_secondary's ground branch fires the shotgun instead
            ship.Die();
            Assert.IsFalse(launcher.TryFire());
            Assert.AreEqual(1, Count<Mine>());
        }

        // ---- Grenade explosion ----

        [Test]
        public void GrenadeExplosionLastsFortyFiveStepsAndHurtsNothing()
        {
            int exploded = 0;
            System.Action<GrenadeExplosion> onExploded = _ => exploded++;
            GrenadeExplosion.Exploded += onExploded;
            try
            {
                var blast = GrenadeExplosion.Spawn(new Vector2(1f, 2f));
                Assert.AreEqual(1, exploded); // sfx_explode
                Assert.AreEqual(new Vector2(1f, 2f), (Vector2)blast.transform.position);
                Assert.IsNull(blast.GetComponent<Collider2D>(), "obj_grenade_explosion has no collision events");

                // spr_grenade_explode has 9 frames at image_speed 0.2.
                for (int i = 0; i < 44; i++) Assert.IsTrue(blast.Tick(Step));
                Assert.IsFalse(blast.Tick(Step));
                Assert.IsTrue(blast == null);
            }
            finally
            {
                GrenadeExplosion.Exploded -= onExploded;
            }
        }

        // ---- next_ground_weapon ----

        static GroundWeaponLoadout NewGroundShip(out VulcanWeapon vulcan, out Shotgun shotgun, out Flamethrower flamer, out Shotgun secondary)
        {
            var go = new GameObject("Player");
            var ship = go.AddComponent<PlayerShipController>();
            ship.ResetState();
            ship.Land();
            vulcan = go.AddComponent<VulcanWeapon>();
            vulcan.ResetState();
            flamer = go.AddComponent<Flamethrower>();
            flamer.ResetState();
            shotgun = go.AddComponent<Shotgun>();
            shotgun.Slot = WeaponSlot.Primary;
            shotgun.ResetState();
            secondary = go.AddComponent<Shotgun>(); // the default Ctrl + left mouse shotgun
            secondary.ResetState();
            var loadout = go.AddComponent<GroundWeaponLoadout>();
            loadout.ResetState();
            return loadout;
        }

        [Test]
        public void NextGroundWeaponCyclesVulcanShotgunFlamerAndWraps()
        {
            var loadout = NewGroundShip(out var vulcan, out var shotgun, out var flamer, out var secondary);
            var switched = new List<GroundWeapon>();
            loadout.Switched += switched.Add;

            Assert.AreEqual(GroundWeapon.Vulcan, loadout.Current);
            Assert.AreEqual(3, loadout.Weapons.Count, "the secondary shotgun is not in the cycle");
            Assert.IsFalse(loadout.Has(GroundWeapon.Laser));

            Assert.IsTrue(loadout.NextGroundWeapon());
            Assert.AreEqual(GroundWeapon.Shotgun, loadout.Current);
            Assert.IsTrue(loadout.NextGroundWeapon());
            Assert.AreEqual(GroundWeapon.Flamer, loadout.Current);
            Assert.IsTrue(loadout.NextGroundWeapon());
            Assert.AreEqual(GroundWeapon.Vulcan, loadout.Current);
            CollectionAssert.AreEqual(new[] { GroundWeapon.Shotgun, GroundWeapon.Flamer, GroundWeapon.Vulcan }, switched);
        }

        [Test]
        public void OnlyTheEquippedGroundPrimaryIsEnabled()
        {
            var loadout = NewGroundShip(out var vulcan, out var shotgun, out var flamer, out var secondary);
            Assert.IsTrue(vulcan.enabled);
            Assert.IsFalse(shotgun.enabled);
            Assert.IsFalse(flamer.enabled);
            Assert.IsTrue(secondary.enabled, "the secondary slot is untouched");

            loadout.Equip(GroundWeapon.Flamer);
            Assert.IsFalse(vulcan.enabled);
            Assert.IsTrue(flamer.enabled);
        }

        [Test]
        public void RefreshPicksUpAGroundPrimaryAddedLater()
        {
            var go = new GameObject("Player");
            go.AddComponent<PlayerShipController>().ResetState();
            go.AddComponent<VulcanWeapon>().ResetState();
            var loadout = go.AddComponent<GroundWeaponLoadout>();
            loadout.ResetState();

            var flamer = go.AddComponent<Flamethrower>();
            flamer.ResetState();
            loadout.Refresh();
            Assert.AreEqual(GroundWeapon.Vulcan, loadout.Current);
            Assert.IsFalse(flamer.enabled);
            Assert.IsTrue(loadout.NextGroundWeapon());
            Assert.AreEqual(GroundWeapon.Flamer, loadout.Current);
        }
    }
}
