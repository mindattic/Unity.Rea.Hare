using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Unity.Hare.Player;
using Unity.Hare.Weapons;

namespace Unity.Hare.Tests.PlayMode
{
    /// <summary>UH-T77: the main scene's ship wakes up armed under Unity's own loop and its weapons fire.</summary>
    public class PlayerShipPlayTests
    {
        const string MainScene = "Assets/Unity.Rea.Hare/Scenes/Rooms/room_gamescreen.unity"; // the arena room (U.HARE-T50: Main.unity is the bootstrap only)

        [UnityTest]
        public IEnumerator TheMainScenesShipWakesArmedAndFires()
        {
            yield return SceneManager.LoadSceneAsync(MainScene, LoadSceneMode.Additive);
            var scene = SceneManager.GetSceneByPath(MainScene);
            try
            {
                yield return null;
                var ship = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerShipController>()).Single();
                var core = ship.GetComponent<PlayerSeat>().Ship;

                Assert.AreEqual(7, core.Guns.Guns.Count, "every gun of obj_player's Create installed itself on Awake");
                Assert.AreEqual(GroundWeapon.Vulcan, core.Ground.Current);
                Assert.AreEqual(5, core.Ground.Weapons.Count);
                Assert.AreEqual(AirWeapon.Rocket, core.Air.Current);

                ship.SetAimDirection(0f);
                var air = ship.GetComponent<AirWeaponLoadout>();
                Assert.IsTrue(air.TryFire(), "the rocket in flight");
                Assert.IsTrue(ship.GetComponent<MineLauncher>().TryFire(), "the mine launcher in flight");

                ship.Land();
                var ground = ship.GetComponent<GroundWeaponLoadout>();
                Assert.IsTrue(ground.CurrentWeapon is VulcanWeapon vulcan && vulcan.TryFire(), "the vulcan perched");
                Assert.IsTrue(ground.NextGroundWeapon());
                Assert.AreEqual(GroundWeapon.Laser, ground.Current);
                Assert.IsTrue(((PerchedWeapon)ground.CurrentWeapon).TryFire(), "the laser perched");
                Assert.IsTrue(ship.GetComponents<Shotgun>().Single(s => s.Slot == WeaponSlot.Secondary).TryFire(), "the shotgun secondary perched");

                yield return new WaitForFixedUpdate();
                Assert.IsNotNull(Object.FindAnyObjectByType<Bullet>());
                Assert.IsNotNull(Object.FindAnyObjectByType<LaserBeam>());
            }
            finally
            {
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t != null && t.parent == null && t.gameObject.scene != scene && t.name != "EnemyDirector" && t.name != "HareSounds").ToList())
                    if (t != null && (t.GetComponent<Bullet>() || t.GetComponent<LaserBeam>() || t.GetComponent<Rocket>() || t.GetComponent<Mine>())) Object.Destroy(t.gameObject);
            }
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
