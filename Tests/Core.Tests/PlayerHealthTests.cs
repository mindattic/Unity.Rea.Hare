using System;
using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using CollectionAssert = NUnit.Framework.Legacy.CollectionAssert;
using Unity.Hare.Player;

namespace Unity.Hare.Tests.Core
{
    /// <summary>obj_player's health and lives (player_hurt, reset_player, Alarms 1/4/5/6/7), run outside Unity.</summary>
    public class PlayerHealthTests
    {
        const float SpawnProtection = 180f / 120f; // flash_time
        const float Invulnerable = 180f / 120f;    // flash_time
        const float DeathWait = 320f / 120f;       // alarm_death_wait

        PlayerRig rig;

        [SetUp]
        public void SetUp() => rig = new PlayerRig();

        ShipHealth Health => rig.Health;

        CommandResult Hit(bool ignoresSpawnProtection = false) => rig.Do(new TakeHit(rig.Id, ignoresSpawnProtection));
        void Tick(float seconds) => rig.Do(new TickHealth(rig.Id, seconds));
        void PastSpawnProtection() => Tick(SpawnProtection + 0.01f);

        /// <summary>Hits until dead, waiting out invulnerability between hits but not after the last.</summary>
        void LoseLife()
        {
            while (!Health.IsDead)
            {
                Assert.IsTrue(Hit().Accepted);
                if (!Health.IsDead) Tick(Invulnerable + 0.01f);
            }
        }

        [Test]
        public void StartsWithOriginalHealthAndLives()
        {
            Assert.AreEqual(6, Health.Health);
            Assert.AreEqual(6, Health.MaxHealth);
            Assert.AreEqual(3, Health.Lives);
            Assert.IsTrue(Health.IsSpawnProtected);
        }

        [Test]
        public void SpawnProtectionBlocksHitsExceptFromSourcesThatIgnoreIt()
        {
            Assert.IsFalse(Hit().Accepted);
            Assert.AreEqual(6, Health.Health);

            Assert.IsTrue(Hit(ignoresSpawnProtection: true).Accepted); // obj_rocket_explosion
            Assert.AreEqual(5, Health.Health);
        }

        [Test]
        public void HitCostsOneHealthAndGrantsInvulnerability()
        {
            PastSpawnProtection();

            var result = Hit();
            Assert.IsTrue(result.Accepted);
            Assert.AreEqual(5, Health.Health);
            Assert.AreEqual(5, result.Find<PlayerDamaged>().Health);
            Assert.IsTrue(Health.IsInvulnerable);
            Assert.IsTrue(Health.IsHurting);

            Assert.IsFalse(Hit().Accepted, "a second hit inside flash_time must not land");
            Assert.AreEqual(5, Health.Health);
        }

        [Test]
        public void InvulnerabilityAndHurtPoseExpireOnTheOriginalTimers()
        {
            PastSpawnProtection();
            Hit();

            Tick(0.51f); // alarm_hurting = 60 steps
            Assert.IsFalse(Health.IsHurting);
            Assert.IsTrue(Health.IsInvulnerable);

            Tick(Invulnerable - 0.5f);
            Assert.IsFalse(Health.IsInvulnerable);
            Assert.IsTrue(Hit().Accepted);
            Assert.AreEqual(4, Health.Health);
        }

        [Test]
        public void ReachingZeroHealthKillsTheShipAndCostsALife()
        {
            PastSpawnProtection();

            LoseLife();

            Assert.IsTrue(Health.IsDead);
            Assert.AreEqual(0, Health.Health);
            Assert.AreEqual(2, Health.Lives);
            Assert.AreEqual(2, rig.Seen<PlayerDied>().Single().Lives);
            Assert.AreEqual(1, rig.Seen<ShipHalted>().Count());
            Assert.AreEqual(PlayerFlightState.Dead, rig.Flight.State);
            Assert.AreEqual(0f, rig.Flight.Speed);
            Assert.IsFalse(rig.Do(new Boost(rig.Id)).Accepted);
            Assert.IsFalse(Hit(ignoresSpawnProtection: true).Accepted, "no hits land while dead");
        }

        [Test]
        public void RespawnsAfterDeathWaitWithFullHealthAndSpawnProtection()
        {
            PastSpawnProtection();
            LoseLife();

            Tick(DeathWait - 0.1f);
            Assert.IsTrue(Health.IsDead, "still waiting out alarm_death_wait");

            Tick(0.2f);
            Assert.AreEqual(1, rig.Seen<PlayerRespawned>().Count());
            Assert.IsFalse(Health.IsDead);
            Assert.AreEqual(6, Health.Health);
            Assert.IsTrue(Health.IsSpawnProtected);
            Assert.AreEqual(PlayerFlightState.Moving, rig.Flight.State);
            Assert.AreEqual(5f, rig.Flight.Speed); // reset_player: speed = flight_speed
            Assert.AreEqual(270f, rig.Flight.Direction, 1e-4f, "the injected random gave no jitter");
        }

        [Test]
        public void RespawnHeadsDownGiveOrTakeThirtyDegrees()
        {
            var jittery = new PlayerRig(jitter: 30f);
            jittery.Do(new TickHealth(jittery.Id, SpawnProtection + 0.01f));
            while (!jittery.Health.IsDead)
            {
                jittery.Do(new TakeHit(jittery.Id));
                jittery.Do(new TickHealth(jittery.Id, jittery.Health.IsDead ? 0f : Invulnerable + 0.01f));
            }
            jittery.Do(new TickHealth(jittery.Id, DeathWait + 0.01f));

            var respawn = jittery.Seen<PlayerRespawned>().Single();
            Assert.That(Math.Abs(respawn.Direction - 270f), Is.LessThanOrEqualTo(30f));
            Assert.AreEqual(respawn.Direction, jittery.Flight.Direction);
        }

        [Test]
        public void LosingTheLastLifeEndsTheGameInsteadOfRespawning()
        {
            for (int life = 0; life < 3; life++)
            {
                PastSpawnProtection();
                LoseLife();
                Tick(DeathWait + 0.01f);
            }

            Assert.AreEqual(0, Health.Lives);
            Assert.AreEqual(1, rig.Seen<GameEnded>().Count());
            Assert.IsTrue(Health.IsGameOver);
            Assert.AreEqual(PlayerFlightState.Dead, rig.Flight.State);
        }

        [Test]
        public void ADeadShipDropsItsShield()
        {
            rig.Landed();
            Assert.IsTrue(rig.Do(new RaiseShield(rig.Id)).Accepted);
            PastSpawnProtection();
            LoseLife();
            Assert.IsFalse(rig.Flight.IsShielding);
            Assert.AreEqual(1, rig.Seen<ShieldDropped>().Count());
        }

        [Test]
        public void SeatsAreHurtSeparately()
        {
            var other = rig.World.AddPlayer(new PlayerId(2));
            PastSpawnProtection();
            Hit();
            Assert.AreEqual(5, rig.Health.Health);
            Assert.AreEqual(6, other.Health.Health);
        }
    }
}
