using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Player;

namespace Unity.Hare.Tests.EditMode
{
    public class PlayerShieldTests
    {
        PlayerShipController NewController()
        {
            var go = new GameObject("Player");
            var player = go.AddComponent<PlayerShipController>();
            player.ResetState(); // Awake doesn't run outside Play Mode; see PlayerShipController.ResetState
            return player;
        }

        PlayerShipController NewPerchedController()
        {
            var player = NewController();
            player.Land();
            return player;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var p in Object.FindObjectsByType<PlayerShipController>(FindObjectsSortMode.None))
                Object.DestroyImmediate(p.gameObject);
            foreach (var w in Object.FindObjectsByType<ShieldWall>(FindObjectsSortMode.None))
                Object.DestroyImmediate(w.gameObject);
        }

        [Test]
        public void ShieldCannotBeRaisedWhileFlying()
        {
            var player = NewController();
            Assert.IsFalse(player.TryShield());
            Assert.IsFalse(player.IsShielding);
            Assert.IsNull(player.Shield);
        }

        [Test]
        public void ShieldRaisesFromIdleAndSpawnsWall()
        {
            var player = NewPerchedController();
            Assert.IsTrue(player.TryShield());
            Assert.IsTrue(player.IsShielding);
            Assert.IsTrue(player.BlocksDamage);
            Assert.IsNotNull(player.Shield);
            Assert.AreSame(player, player.Shield.Owner);
            Assert.IsTrue(player.Shield.GetComponent<CircleCollider2D>().isTrigger);
        }

        [Test]
        public void ShieldDoesNotStackWhileAlreadyRaised()
        {
            var player = NewPerchedController();
            Assert.IsTrue(player.TryShield());
            Assert.IsFalse(player.TryShield());
            Assert.AreEqual(1, Object.FindObjectsByType<ShieldWall>(FindObjectsSortMode.None).Length);
        }

        [Test]
        public void ShieldRefusedBelowAThirdEnergy()
        {
            var player = NewPerchedController();
            Assert.IsTrue(player.TryShield());
            // Drain to just under 33% (40/s placeholder drain), then release and retry.
            while (player.Energy >= 33f) player.Step(0.1f);
            player.DropShield();
            Assert.IsFalse(player.TryShield());
        }

        [Test]
        public void ShieldDrainsEnergyAndCollapsesWhenEmpty()
        {
            var player = NewPerchedController();
            Assert.IsTrue(player.TryShield());

            player.Step(0.5f);
            Assert.Less(player.Energy, 100f);
            Assert.IsTrue(player.IsShielding);

            for (int i = 0; i < 100 && player.IsShielding; i++) player.Step(0.1f);
            Assert.IsFalse(player.IsShielding);
            Assert.IsFalse(player.BlocksDamage);
            Assert.IsNull(player.Shield);
            Assert.AreEqual(0, Object.FindObjectsByType<ShieldWall>(FindObjectsSortMode.None).Length);
        }

        [Test]
        public void DroppingShieldLetsEnergyRefill()
        {
            var player = NewPerchedController();
            player.TryShield();
            player.Step(1f);
            float drained = player.Energy;

            player.DropShield();
            player.Step(1f);
            Assert.Greater(player.Energy, drained);
        }

        [Test]
        public void LeapDropsShield()
        {
            var player = NewPerchedController();
            player.TryShield();
            Assert.IsTrue(player.TryLeap(270f));
            Assert.IsFalse(player.IsShielding);
            Assert.IsNull(player.Shield);
        }

        [Test]
        public void ShieldWallFollowsPlayerAndFacesAim()
        {
            var player = NewPerchedController();
            player.transform.position = new Vector3(3f, -2f, 0f);
            player.SetAimDirection(45f);
            player.TryShield();

            player.Shield.Follow();
            Assert.AreEqual((Vector2)player.transform.position, (Vector2)player.Shield.transform.position);
            Assert.AreEqual(45f, player.Shield.transform.eulerAngles.z, 0.01f);
        }

        [Test]
        public void ShieldFlickerStaysInOriginalAlphaBand()
        {
            var player = NewPerchedController();
            player.TryShield();
            for (int step = 0; step <= 10; step++)
            {
                player.Shield.Flicker(step);
                Assert.That(player.Shield.Alpha, Is.InRange(0.3f, 0.6f));
            }
        }

        [Test]
        public void ShieldNormalPointsFromCentreToContact()
        {
            var player = NewPerchedController();
            player.TryShield();
            Vector2 n = player.Shield.NormalAt((Vector2)player.transform.position + Vector2.right * 2f);
            Assert.AreEqual(1f, n.x, 1e-4f);
            Assert.AreEqual(0f, n.y, 1e-4f);
        }
    }
}
