using System.Collections;
using NUnit.Framework;
using Unity.Hare.Content;
using Unity.Hare.Online;
using Unity.Hare.Player;
using Unity.Multiplayer;
using Unity.Multiplayer.Testing;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Unity.Hare.Tests.Online
{
    /// <summary>
    /// The generated online scene itself (U.HARE-T35): it hosts from its own NetBootstrap and seats the host's ship on
    /// the authority, and Play solo takes the player back to the local game with none of the kit left behind.
    /// </summary>
    public class OnlineScenePlayTests
    {
        /// <summary>The test loads whole scenes; leave only an empty one behind so later tests start clean.</summary>
        [UnityTearDown]
        public IEnumerator LeaveAnEmptyScene()
        {
            var empty = SceneManager.CreateScene("OnlineScenePlayTests-empty-" + System.Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene != empty && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            var bootstrap = Object.FindAnyObjectByType<NetBootstrap>();
            if (bootstrap != null) Object.Destroy(bootstrap.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheOnlineSceneHostsASeatedShip_AndPlaySoloGoesBackToTheLocalGame()
        {
            // A headless editor's NetBootstrap would serve by itself on the default port; switch it off before its
            // Start (sceneLoaded runs after Awake, before Start) and host on a free port instead, as the menu's Host would.
            NetBootstrap bootstrap = null;
            UnityEngine.Events.UnityAction<Scene, LoadSceneMode> grab = (scene, mode) =>
            {
                bootstrap = Object.FindAnyObjectByType<NetBootstrap>();
                if (bootstrap != null) bootstrap.enabled = false;
            };
            SceneManager.sceneLoaded += grab;
            yield return SceneManager.LoadSceneAsync("Online", LoadSceneMode.Single);
            SceneManager.sceneLoaded -= grab;
            yield return null; // HareOnlineGame.Start

            Assert.That(bootstrap, Is.Not.Null, "the online scene has a NetBootstrap");
            Assert.That(Object.FindAnyObjectByType<HareOnlineGame>(), Is.Not.Null);
            Assert.That(bootstrap.DefaultConfig.MaxPlayers, Is.EqualTo(GameContent.Current.Online.MaxPlayers), "sized from the online section");

            bootstrap.Configure(c => c.Port = FreePort.Udp());
            Assert.That(bootstrap.StartHost(), Is.True, "the host started");

            HareNetPlayer host = null;
            float until = Time.realtimeSinceStartup + 10f;
            while ((host == null || host.Seat != 0) && Time.realtimeSinceStartup < until)
            {
                host = Object.FindAnyObjectByType<HareNetPlayer>();
                yield return null;
            }
            Assert.That(host, Is.Not.Null, "the host's own player never spawned");
            Assert.That(host.Seat, Is.EqualTo(0));
            var authority = HareAuthority.Find(bootstrap.Session);
            Assert.That(authority, Is.Not.Null);
            Assert.That(authority.World.Seated, Is.EquivalentTo(new[] { new PlayerId(1) }));

            Vector2 start = host.Position;
            until = Time.realtimeSinceStartup + 5f;
            while (host.Position == start && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(host.Position, Is.Not.EqualTo(start), "the host's ship flies");
            Assert.That(host.GetComponent<SpriteRenderer>().sprite, Is.Not.Null, "drawn in the rabbit's sprite");

            HareOnlineGame.PlaySolo();
            until = Time.realtimeSinceStartup + 10f;
            while (SceneManager.GetActiveScene().name != HareOnlineGame.SoloScene && Time.realtimeSinceStartup < until) yield return null;
            yield return null;

            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(HareOnlineGame.SoloScene));
            Assert.That(Object.FindAnyObjectByType<NetBootstrap>(), Is.Null, "none of the kit follows into the local game");
            Assert.That(Object.FindAnyObjectByType<HareNetPlayer>(), Is.Null);
            Assert.That(Object.FindAnyObjectByType<PlayerShipController>(), Is.Not.Null, "the local game's own ship");
        }
    }
}
