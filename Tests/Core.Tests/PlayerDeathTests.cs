using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using Unity.Rea.Hare.Content;
using Unity.Rea.Hare.Player;
using Unity.Rea.Hare.Session;

namespace Unity.Rea.Hare.Tests.Core
{
    /// <summary>
    /// The death and respawn flow (U.HARE-T54): player_hurt's death and the out-of-bounds rule spending the session's
    /// LIVES, reset_player's whole-degree heading, Alarm 5's colour cycle, the hurt pose and sound, and obj_losthealth.
    /// </summary>
    public class PlayerDeathTests
    {
        const float Step = 1f / 120f;
        const float FlashTime = 180f / 120f;
        const float DeathWait = 320f / 120f;

        PlayerRig rig;
        GameSession session;
        List<SessionEvent> sessionEvents;

        [SetUp]
        public void SetUp()
        {
            rig = new PlayerRig();
            session = new GameSession(ContentRegistry.Default);
            sessionEvents = new List<SessionEvent>();
            rig.Do(new UseLives(rig.Id, new SessionLives(() => session, rig.Id, r => sessionEvents.AddRange(r.Events))));
        }

        ShipHealth Health => rig.Health;

        void Tick(float seconds) => rig.Do(new TickHealth(rig.Id, seconds));

        void Die()
        {
            Tick(FlashTime + 0.01f); // past justspawned
            while (!Health.IsDead)
            {
                Assert.IsTrue(rig.Do(new TakeHit(rig.Id)).Accepted);
                if (!Health.IsDead) Tick(FlashTime + 0.01f);
            }
        }

        [Test]
        public void DeathSpendsTheSessionsLives()
        {
            Assert.IsTrue(Health.UsesSharedLives);
            Assert.AreEqual(3, Health.Lives);

            Die();

            Assert.AreEqual(2, session.Lives, "player_hurt: LIVES -= 1 on the game's LIVES");
            Assert.AreEqual(2, rig.Seen<PlayerDied>().Single().Lives);
            Assert.AreEqual(2, sessionEvents.OfType<LifeLost>().Single().Lives);
        }

        [Test]
        public void LeavingTheArenaSpendsTheSameLives()
        {
            Assert.IsTrue(rig.Do(new LeaveArena(rig.Id)).Accepted);
            Assert.AreEqual(2, session.Lives);
            Assert.AreEqual(2, rig.Seen<PlayerLeftArena>().Single().Lives);
            Assert.AreEqual(1, rig.Seen<PlayerRespawned>().Count());

            Die();
            Assert.AreEqual(1, session.Lives, "both deaths count against one LIVES");
        }

        [Test]
        public void DeathWaitsThreeHundredTwentyStepsThenResets()
        {
            Die();
            for (int i = 0; i < 319; i++) Tick(Step);
            Assert.IsTrue(Health.IsDead, "alarm_death_wait is 320 steps");
            Tick(Step);
            Assert.IsFalse(Health.IsDead);
            Assert.AreEqual(6, Health.Health);
            Assert.IsTrue(Health.IsSpawnProtected, "reset_player: justspawned");
            Assert.IsFalse(Health.IsInvulnerable, "reset_player: invulnerable = false");
            Assert.AreEqual(ShipColor.White, Health.Color);
            Assert.AreEqual(5f, rig.Flight.Speed);
        }

        [Test]
        public void TheLastLifeEndsTheGameAfterTheDeathWait()
        {
            session = new GameSession(ContentRegistry.Default);
            rig.Do(new LeaveArena(rig.Id));
            rig.Do(new LeaveArena(rig.Id));
            Assert.AreEqual(1, session.Lives);

            Die();
            Assert.AreEqual(0, session.Lives);
            Assert.AreEqual(0, rig.Seen<GameEnded>().Count(), "the death wait runs first");

            Tick(DeathWait + 0.01f);
            Assert.AreEqual(1, rig.Seen<GameEnded>().Count());
            Assert.IsTrue(Health.IsGameOver);
            Assert.AreEqual(0, session.Lives, "LIVES never goes below 0 (BIBLE D14)");
        }

        [Test]
        public void LeavingTheArenaWithNoLivesLeftRestartsTheGame()
        {
            for (int i = 0; i < 3; i++) rig.Do(new LeaveArena(rig.Id));
            Assert.AreEqual(0, session.Lives);

            Assert.IsTrue(rig.Do(new LeaveArena(rig.Id)).Accepted);
            Assert.AreEqual(1, rig.Seen<GameEnded>().Count());
            Assert.AreEqual(0, session.Lives);
        }

        [Test]
        public void ARoomReloadKeepsTheSessionsLives()
        {
            rig.Do(new LeaveArena(rig.Id));
            rig.Do(new ResetHealth(rig.Id));
            Assert.AreEqual(2, Health.Lives, "only game_restart gives the lives back");
        }

        [Test]
        public void ANewSessionIsGameRestart()
        {
            rig.Do(new LeaveArena(rig.Id));
            session = new GameSession(ContentRegistry.Default);
            Assert.AreEqual(3, Health.Lives, "the ledger reads whichever session is current");
        }

        [Test]
        public void WithoutASessionTheShipCountsItsOwnLives()
        {
            var own = new PlayerRig();
            Assert.IsFalse(own.Health.UsesSharedLives);
            own.Do(new LeaveArena(own.Id));
            Assert.AreEqual(2, own.Health.Lives);
            own.Do(new ResetHealth(own.Id));
            Assert.AreEqual(3, own.Health.Lives);
        }

        [Test]
        public void SpendLifeNeverRestartsTheSession()
        {
            var s = new GameSession(ContentRegistry.Default);
            for (int i = 0; i < 3; i++) Assert.IsTrue(s.Execute(new SpendLife(new PlayerId(1))).Accepted);
            var last = s.Execute(new SpendLife(new PlayerId(1)));
            Assert.IsFalse(last.Accepted);
            Assert.AreEqual(0, s.Lives);
            Assert.IsNull(last.Find<SessionReset>());
        }

        [Test]
        public void RespawnHeadingIsAWholeNumberOfDegrees()
        {
            var r = new PlayerRig(jitter: 12.7f);
            r.Do(new LeaveArena(r.Id));
            Assert.AreEqual(282f, r.Seen<PlayerRespawned>().Single().Direction, "270 + irandom_range(-30, 30)");

            var top = new PlayerRig(jitter: 31f); // a float random that reaches its upper bound stays within 30
            top.Do(new LeaveArena(top.Id));
            Assert.AreEqual(300f, top.Seen<PlayerRespawned>().Single().Direction);
        }

        [Test]
        public void HitsFlashWhitePinkAmberCornflowerEveryThreeSteps()
        {
            Tick(FlashTime + 0.01f);
            rig.Do(new TakeHit(rig.Id));
            Assert.AreEqual(ShipColor.White, Health.Color, "the colour changes when Alarm 5 first fires, a step later");

            var seen = new List<ShipColor>();
            Tick(Step);
            seen.Add(Health.Color);
            for (int i = 0; i < 5; i++)
            {
                for (int s = 0; s < 3; s++) Tick(Step);
                seen.Add(Health.Color);
            }
            CollectionAssert(new[]
            {
                ShipColor.Pink, ShipColor.Amber, ShipColor.CornflowerBlue, ShipColor.CornflowerBlue, ShipColor.White, ShipColor.Pink,
            }, seen);

            Tick(FlashTime);
            Assert.IsFalse(Health.IsInvulnerable);
            Assert.AreEqual(ShipColor.White, Health.Color, "Alarm 1: color = c_white");
        }

        [Test]
        public void ARespawnDoesNotFlash()
        {
            rig.Do(new LeaveArena(rig.Id));
            for (int i = 0; i < 30; i++)
            {
                Tick(Step);
                Assert.AreEqual(ShipColor.White, Health.Color);
            }
            Assert.IsTrue(Health.IsSpawnProtected);
        }

        [Test]
        public void HurtingShowsTheHurtPoseForSixtySteps()
        {
            Assert.AreEqual(ShipPose.Leap, rig.Ship.Pose);
            Tick(FlashTime + 0.01f);
            rig.Do(new TakeHit(rig.Id));
            Assert.AreEqual(ShipPose.LeapHurt, rig.Ship.Pose);
            for (int i = 0; i < 60; i++) Tick(Step);
            Assert.AreEqual(ShipPose.Leap, rig.Ship.Pose);

            Assert.AreEqual(ShipPose.Idle, ShipPose.SpriteFor(PlayerFlightState.Idle, false, false, false));
            Assert.AreEqual(ShipPose.IdleHurt, ShipPose.SpriteFor(PlayerFlightState.Idle, false, false, true));
            Assert.AreEqual(ShipPose.CrouchHurt, ShipPose.SpriteFor(PlayerFlightState.Idle, true, false, true));
            Assert.AreEqual(ShipPose.Crouch, ShipPose.SpriteFor(PlayerFlightState.Idle, false, true, false));
        }

        [Test]
        public void HurtSoundIsOneOfEight()
        {
            var r = new PlayerRig(jitter: 8.9f);
            r.Do(new TickHealth(r.Id, FlashTime + 0.01f));
            r.Do(new TakeHit(r.Id));
            Assert.AreEqual(8, r.Seen<PlayerDamaged>().Single().HurtSound);

            Tick(FlashTime + 0.01f);
            rig.Do(new TakeHit(rig.Id));
            Assert.AreEqual(1, rig.Seen<PlayerDamaged>().Single().HurtSound);
        }

        [Test]
        public void LostHealthPipRisesAndFadesWhereTheLostPipWas()
        {
            var pip = new LostHealthPip(5, 1080f, new HealthTuning());
            Assert.AreEqual(140f + 5 * 44f, pip.X);
            Assert.AreEqual(1080f - 64f - 36f, pip.Y);
            Assert.AreEqual(1f, pip.Alpha);

            Assert.IsTrue(pip.Step(Step, paused: true));
            Assert.AreEqual(980f, pip.Y, "obj_losthealth stands still while paused");

            for (int i = 0; i < 99; i++) Assert.IsTrue(pip.Step(Step, paused: false));
            Assert.AreEqual(881f, pip.Y, 1e-3f);
            Assert.IsFalse(pip.Step(Step, paused: false), "gone at image_alpha 0, 100 steps");
        }

        static void CollectionAssert(ShipColor[] expected, List<ShipColor> actual) =>
            NUnit.Framework.Legacy.CollectionAssert.AreEqual(expected, actual);
    }
}
