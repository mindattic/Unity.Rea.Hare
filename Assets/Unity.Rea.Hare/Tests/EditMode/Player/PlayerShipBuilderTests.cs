using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Tests.EditMode
{
    /// <summary>UH-T77: the player ship is built with every weapon of obj_player's Create and the main scene carries it.</summary>
    public class PlayerShipBuilderTests
    {
        HashSet<GameObject> before;

        [SetUp]
        public void SetUp() => before = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());

        [TearDown]
        public void TearDown()
        {
            foreach (var go in SceneManager.GetActiveScene().GetRootGameObjects())
                if (go != null && !before.Contains(go)) Object.DestroyImmediate(go);
        }

        [Test]
        public void TheShipCarriesEveryWeaponOfObjPlayersCreate()
        {
            var ship = PlayerShipBuilder.Build(Vector3.zero);
            AssertCarriesEveryWeapon(ship);
        }

        [Test]
        public void PerchedEveryGroundPrimaryOfQsCycleFiresAndTheShotgunSecondaryToo()
        {
            var go = PlayerShipBuilder.Build(Vector3.zero);
            var ship = go.GetComponent<PlayerShipController>();
            var ground = go.GetComponent<GroundWeaponLoadout>();
            ship.SetAimDirection(0f);
            ship.Land();

            var fired = new List<GroundWeapon>();
            for (int i = 0; i < 5; i++)
            {
                var weapon = ground.CurrentWeapon;
                Assert.IsTrue(((Behaviour)weapon).enabled, weapon.GroundKind + " is the one equipped");
                bool shot = weapon is VulcanWeapon vulcan ? vulcan.TryFire() : ((PerchedWeapon)weapon).TryFire();
                Assert.IsTrue(shot, weapon.GroundKind + " fires while perched");
                fired.Add(weapon.GroundKind);
                ground.NextGroundWeapon();
            }
            CollectionAssert.AreEqual(
                new[] { GroundWeapon.Vulcan, GroundWeapon.Laser, GroundWeapon.Railgun, GroundWeapon.Shotgun, GroundWeapon.Flamer }, fired);
            Assert.AreEqual(GroundWeapon.Vulcan, ground.Current, "next_ground_weapon wraps");
            Assert.IsNotNull(Object.FindAnyObjectByType<Bullet>());
            Assert.IsNotNull(Object.FindAnyObjectByType<LaserBeam>());
            Assert.IsNotNull(Object.FindAnyObjectByType<RailgunShot>());
            Assert.IsNotNull(Object.FindAnyObjectByType<Flame>());

            var secondary = go.GetComponents<Shotgun>().Single(s => s.Slot == WeaponSlot.Secondary);
            Assert.IsTrue(secondary.TryFire(), "Ctrl + left mouse fires the shotgun while perched");
            Assert.AreEqual(16, secondary.LastBlast.Count);
            Assert.IsFalse(go.GetComponent<MineLauncher>().TryFire(), "the mine launcher is the air secondary");
            Assert.IsFalse(go.GetComponent<AirWeaponLoadout>().TryFire(), "the air primary waits for flight");
        }

        [Test]
        public void InFlightTheRocketTheMineAndTheMineLauncherFire()
        {
            var go = PlayerShipBuilder.Build(Vector3.zero);
            go.GetComponent<PlayerShipController>().SetAimDirection(0f);
            var air = go.GetComponent<AirWeaponLoadout>();

            Assert.AreEqual(AirWeapon.Rocket, air.Current, "air_weapon = weapon_rocket");
            Assert.IsTrue(air.TryFire());
            Assert.IsNotNull(Object.FindAnyObjectByType<Rocket>());
            Assert.IsTrue(air.NextAirWeapon());
            Assert.AreEqual(AirWeapon.Mine, air.Current);
            Assert.IsTrue(air.TryFire());
            Assert.IsNotNull(air.LastMine);

            var launcher = go.GetComponent<MineLauncher>();
            Assert.IsTrue(launcher.TryFire(), "air_weapon_secondary = weapon_mine");
            Assert.IsNotNull(launcher.LastMine);
            Assert.IsFalse(go.GetComponent<VulcanWeapon>().TryFire(), "ground weapons wait for a perch");
        }

        [Test]
        public void AShipBuiltForAnAuthoritySitsInItsWorld()
        {
            var world = new PlayerWorld((min, max) => min);
            var go = PlayerShipBuilder.Build(Vector3.zero, world: world, id: new PlayerId(2));
            Assert.IsTrue(world.Has(new PlayerId(2)));
            Assert.AreEqual(7, world.Get(new PlayerId(2)).Guns.Guns.Count);
            Assert.AreSame(world, go.GetComponent<PlayerSeat>().World);
        }

        [Test]
        public void ABadLoadoutIsRefusedBeforeAnythingIsBuilt()
        {
            int count = SceneManager.GetActiveScene().rootCount;
            var bad = new ShipLoadout { Guns = new[] { new GunMount(GunType.MineLauncher, WeaponSlot.Primary) } };
            Assert.Throws<System.InvalidOperationException>(() => PlayerShipBuilder.Build(Vector3.zero, bad));
            Assert.AreEqual(count, SceneManager.GetActiveScene().rootCount);
        }

        [Test]
        public void RoomGamescreenCarriesTheArmedShip()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Unity.Rea.Hare/Scenes/Rooms/room_gamescreen.unity", OpenSceneMode.Additive);
            try
            {
                var ships = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerShipController>(true)).ToList();
                Assert.AreEqual(1, ships.Count, "one player ship in room_gamescreen");
                AssertCarriesEveryWeapon(ships[0].gameObject);
                var vulcan = ships[0].GetComponent<VulcanWeapon>();
                var so = new UnityEditor.SerializedObject(vulcan);
                Assert.IsNotNull(so.FindProperty("loopClip").objectReferenceValue, "the vulcan's sounds are wired in");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static void AssertCarriesEveryWeapon(GameObject ship)
        {
            Assert.IsNotNull(ship.GetComponent<PlayerShipController>());
            Assert.IsNotNull(ship.GetComponent<PlayerHealth>());
            Assert.IsNotNull(ship.GetComponent<PlayerAmmo>());
            Assert.AreEqual(1, ship.GetComponents<VulcanWeapon>().Length);
            Assert.AreEqual(1, ship.GetComponents<LaserGun>().Length);
            Assert.AreEqual(1, ship.GetComponents<RailgunGun>().Length);
            Assert.AreEqual(1, ship.GetComponents<Flamethrower>().Length);
            Assert.AreEqual(1, ship.GetComponents<MineLauncher>().Length);
            CollectionAssert.AreEquivalent(new[] { WeaponSlot.Primary, WeaponSlot.Secondary },
                ship.GetComponents<Shotgun>().Select(s => s.Slot));
            Assert.AreEqual(WeaponSlot.Secondary, ship.GetComponent<MineLauncher>().Slot);
            Assert.IsNotNull(ship.GetComponent<GroundWeaponLoadout>());
            Assert.IsNotNull(ship.GetComponent<AirWeaponLoadout>());
        }
    }
}
