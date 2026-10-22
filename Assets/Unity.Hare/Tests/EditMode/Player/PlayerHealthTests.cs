using NUnit.Framework;
using UnityEngine;
using Unity.Hare.Player;

namespace Unity.Hare.Tests.EditMode
{
    public class PlayerHealthTests
    {
        const float SpawnProtection = 180f / 120f; // flash_time
        const float Invulnerable = 180f / 120f;    // flash_time
        const float DeathWait = 320f / 120f;       // alarm_death_wait

        PlayerShipController ship;
        PlayerHealth health;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("Player");
            ship = go.AddComponent<PlayerShipController>();
            health = go.AddComponent<PlayerHealth>();
            // Awake doesn't run outside Play Mode; see PlayerShipController.ResetState.
            ship.ResetState();
            health.ResetState();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var p in Object.FindObjectsByType<PlayerHealth>())
                Object.DestroyImmediate(p.gameObject);
        }

        void PastSpawnProtection() => health.Tick(SpawnProtection + 0.01f);

        /// <summary>Hits until dead, waiting out invulnerability between hits but not after the last.</summary>
        void LoseLife()
        {
            while (!health.IsDead)
            {
                Assert.IsTrue(health.TryTakeHit());
                if (!health.IsDead) health.Tick(Invulnerable + 0.01f);
            }
        }

        [Test]
        public void StartsWithOriginalHealthAndLives()
        {
            Assert.AreEqual(6, health.Health);
            Assert.AreEqual(6, health.MaxHealth);
            Assert.AreEqual(3, health.Lives);
            Assert.IsTrue(health.IsSpawnProtected);
        }

        [Test]
        public void SpawnProtectionBlocksHitsExceptFromSourcesThatIgnoreIt()
        {
            Assert.IsFalse(health.TryTakeHit());
            Assert.AreEqual(6, health.Health);

            Assert.IsTrue(health.TryTakeHit(ignoresSpawnProtection: true)); // obj_rocket_explosion
            Assert.AreEqual(5, health.Health);
        }

        [Test]
        public void HitCostsOneHealthAndGrantsInvulnerability()
        {
            PastSpawnProtection();
            int reported = -1;
            health.Damaged += hp => reported = hp;

            Assert.IsTrue(health.TryTakeHit());
            Assert.AreEqual(5, health.Health);
            Assert.AreEqual(5, reported);
            Assert.IsTrue(health.IsInvulnerable);
            Assert.IsTrue(health.IsHurting);

            Assert.IsFalse(health.TryTakeHit(), "a second hit inside flash_time must not land");
            Assert.AreEqual(5, health.Health);
        }

        [Test]
        public void InvulnerabilityAndHurtPoseExpireOnTheOriginalTimers()
        {
            PastSpawnProtection();
            health.TryTakeHit();

            health.Tick(0.51f); // alarm_hurting = 60 steps
            Assert.IsFalse(health.IsHurting);
            Assert.IsTrue(health.IsInvulnerable);

            health.Tick(Invulnerable - 0.5f);
            Assert.IsFalse(health.IsInvulnerable);
            Assert.IsTrue(health.TryTakeHit());
            Assert.AreEqual(4, health.Health);
        }

        [Test]
        public void ReachingZeroHealthKillsTheShipAndCostsALife()
        {
            PastSpawnProtection();
            int livesReported = -1;
            health.Died += lives => livesReported = lives;

            LoseLife();

            Assert.IsTrue(health.IsDead);
            Assert.AreEqual(0, health.Health);
            Assert.AreEqual(2, health.Lives);
            Assert.AreEqual(2, livesReported);
            Assert.AreEqual(PlayerFlightState.Dead, ship.State);
            Assert.AreEqual(0f, ship.Speed);
            Assert.IsFalse(ship.TryBoost());
            Assert.IsFalse(health.TryTakeHit(ignoresSpawnProtection: true), "no hits land while dead");
        }

        [Test]
        public void RespawnsAfterDeathWaitWithFullHealthAndSpawnProtection()
        {
            PastSpawnProtection();
            bool respawned = false;
            health.Respawned += () => respawned = true;
            LoseLife();

            health.Tick(DeathWait - 0.1f);
            Assert.IsTrue(health.IsDead, "still waiting out alarm_death_wait");

            health.Tick(0.2f);
            Assert.IsTrue(respawned);
            Assert.IsFalse(health.IsDead);
            Assert.AreEqual(6, health.Health);
            Assert.IsTrue(health.IsSpawnProtected);
            Assert.AreEqual(PlayerFlightState.Moving, ship.State);
            Assert.AreEqual(5f, ship.Speed); // reset_player: speed = flight_speed
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(270f, ship.Direction)), Is.LessThanOrEqualTo(30f));
        }

        [Test]
        public void LosingTheLastLifeEndsTheGameInsteadOfRespawning()
        {
            bool gameOver = false;
            health.GameOver += () => gameOver = true;

            for (int life = 0; life < 3; life++)
            {
                PastSpawnProtection();
                LoseLife();
                health.Tick(DeathWait + 0.01f);
            }

            Assert.AreEqual(0, health.Lives);
            Assert.IsTrue(gameOver);
            Assert.IsTrue(health.IsGameOver);
            Assert.AreEqual(PlayerFlightState.Dead, ship.State);
        }
    }
}
