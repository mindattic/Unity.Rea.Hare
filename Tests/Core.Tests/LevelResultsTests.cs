using System.Linq;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;
using Unity.Rea.Hare.Arena;
using Unity.Rea.Hare.Content;
using Unity.Rea.Hare.Enemies;
using Unity.Rea.Hare.Flow;
using Unity.Rea.Hare.Player;

namespace Unity.Rea.Hare.Tests.Core
{
    /// <summary>
    /// U.HARE-T22: obj_level_results (its panel, five lines and obj_healthbar's strTime), the Enter that sends the ship off
    /// (state_leave: invulnerable, the leap pose, twice flight_speed, turning 3 degrees a step toward exit_x above the room,
    /// the black and orange smoke behind it), the arena's leave step (exit_angle, then the one-time exit off the top) and
    /// which enemy deaths KILLS counts (docs/BIBLE.md D11).
    /// </summary>
    public class LevelResultsTests
    {
        static readonly PlayerId P1 = new PlayerId(1);
        const float Step = PlayerRig.Step;

        // ---- The panel ----

        [Test]
        public void ThePanelSitsWhereSetLevelCompletePutsIt()
        {
            var results = new LevelResults(ContentRegistry.Default.Arena.Results);
            Assert.AreEqual(960f, results.X, "screen_width/2");
            Assert.AreEqual(270f, results.Y, "screen_height/4");
            Assert.AreEqual(604f, results.SpriteLeft, "spr_results' origin is 356,0");
            Assert.AreEqual(270f, results.SpriteTop);
            Assert.AreEqual(0.7f, results.Definition.Alpha, 1e-6f);
            Assert.AreEqual("spr_results", results.Definition.Sprite);
        }

        [Test]
        public void TheDrawGuiDrawsItsFiveLinesWhereTheOriginalDoes()
        {
            var lines = new LevelResults(ContentRegistry.Default.Arena.Results).Lines("01:05", 12);
            Assert.AreEqual(6, lines.Length);

            Assert.AreEqual("Level Complete", lines[0].Text);
            Assert.IsTrue(lines[0].Large, "fnt_score");
            Assert.AreEqual(ResultsAlign.Center, lines[0].Align);
            Assert.AreEqual(960f, lines[0].X);
            Assert.AreEqual(295f, lines[0].Y, "y + margin");

            Assert.AreEqual("Time", lines[1].Text);
            Assert.AreEqual(ResultsAlign.Left, lines[1].Align);
            Assert.AreEqual(960f - 356f + 25f, lines[1].X, "x - width/2 + margin");
            Assert.AreEqual(395f, lines[1].Y, "y + margin*5");
            Assert.AreEqual("01:05", lines[2].Text);
            Assert.AreEqual(ResultsAlign.Right, lines[2].Align);
            Assert.AreEqual(960f + 356f - 25f, lines[2].X, "x + width/2 - margin");

            Assert.AreEqual("Enemies Killed", lines[3].Text);
            Assert.AreEqual(420f, lines[3].Y, "y + margin*6");
            Assert.AreEqual("12", lines[4].Text);
            Assert.AreEqual(ResultsAlign.Right, lines[4].Align);

            Assert.AreEqual("Press [ENTER] to continue", lines[5].Text);
            Assert.AreEqual(ResultsAlign.Center, lines[5].Align);
            Assert.AreEqual(270f + 356f - 25f, lines[5].Y, "y + height/2 - margin");
            Assert.IsTrue(lines.Skip(1).All(l => !l.Large), "fnt_point");
        }

        [Test]
        public void TheHudClockCountsMinutesAndSecondsAsStrTime()
        {
            var clock = new HudClock();
            Assert.AreEqual("00:00", clock.Text, "obj_healthbar's Create");
            for (int i = 0; i < 9; i++) clock.Tick();
            Assert.AreEqual("00:09", clock.Text);
            clock.Tick();
            Assert.AreEqual("00:10", clock.Text);
            for (int i = 0; i < 50; i++) clock.Tick();
            Assert.AreEqual("01:00", clock.Text, "seconds == 60: seconds = 0; minutes += 1");
            for (int i = 0; i < 60 * 99; i++) clock.Tick();
            Assert.AreEqual("100:00", clock.Text, "minutes do not wrap");
        }

        [Test]
        public void TheResultsSettingsAreValidatedOnLoad()
        {
            Assert.Throws<ContentValidationException>(() =>
                ContentRegistry.FromJson("{\"arena\": {\"Results\": {\"Alpha\": 2}}}"));
        }

        // ---- The ship sent off ----

        [Test]
        public void EnterSendsTheShipOff_InvulnerableInTheLeapPoseAtTwiceFlightSpeed()
        {
            var rig = new PlayerRig().Landed();
            Assert.AreEqual(PlayerFlightState.Idle, rig.Flight.State);

            var result = rig.Do(new LeaveLevel(P1));
            Assert.IsTrue(result.Accepted);
            Assert.IsTrue(result.Has<ShipLeaving>());
            Assert.AreEqual(PlayerFlightState.Leaving, rig.Flight.State);
            Assert.IsTrue(rig.Health.IsInvulnerable, "player.invulnerable = true");
            Assert.AreEqual(ShipPose.Leap, rig.Ship.Pose, "sprite_index = asset_leap");
            Assert.AreEqual(rig.Flight.Tuning.FlightSpeed * 2f, rig.Flight.Speed, "speed = flight_speed * 2");
            Assert.Greater(rig.Flight.Velocity.Magnitude, 0f, "it flies");
            Assert.IsNull(rig.Flight.CurrentRock);

            Assert.IsFalse(rig.Do(new LeaveLevel(P1)).Accepted, "already leaving");
            Assert.IsFalse(rig.Do(new TakeHit(P1, ignoresSpawnProtection: true)).Accepted, "no end_flash ever clears it");
            Assert.IsFalse(rig.Do(new Leap(P1, 90f)).Accepted);
            Assert.IsFalse(rig.Do(new Fling(P1)).Accepted);
            Assert.IsFalse(rig.Do(new Land(P1)).Accepted);

            // A thousand steps on, it is still out of harm's way.
            for (int i = 0; i < 1000; i++) rig.Do(new TickHealth(P1, Step));
            Assert.IsTrue(rig.Health.IsInvulnerable);
        }

        [Test]
        public void TheLeavingShipTurnsThreeDegreesAStepTowardItsExitAngle_AndFacesItsHeading()
        {
            var rig = new PlayerRig();
            rig.Do(new LeaveLevel(P1));
            Assert.AreEqual(270f, rig.Flight.Direction, 1e-3f, "flying in, straight down");
            Assert.IsTrue(rig.Do(new AimExit(P1, 0f)).Accepted);

            // check_rotate_clockwise(0, 270): 0 is 90 counter-clockwise of 270, so direction += 3.
            rig.Do(new StepFlight(P1, Step));
            Assert.AreEqual(273f, rig.Flight.Direction, 1e-3f);
            Assert.AreEqual(rig.Flight.Direction, rig.Flight.Facing, 1e-3f, "image_angle = direction");

            rig.Do(new AimExit(P1, 180f));
            rig.Do(new StepFlight(P1, Step));
            Assert.AreEqual(270f, rig.Flight.Direction, 1e-3f, "180 is clockwise of 273: direction -= 3");

            // On its heading it still turns every step, so it wobbles 3 degrees either side.
            rig.Do(new AimExit(P1, 90f));
            for (int i = 0; i < 60; i++) rig.Do(new StepFlight(P1, Step));
            Assert.AreEqual(90f, rig.Flight.Direction, 3.01f);
            rig.Do(new StepFlight(P1, Step));
            float a = rig.Flight.Direction;
            rig.Do(new StepFlight(P1, Step));
            Assert.AreEqual(3f, System.Math.Abs(Maths.DeltaAngle(a, rig.Flight.Direction)), 1e-3f);
        }

        [Test]
        public void CheckRotateClockwiseMatchesTheOriginalsFourCases()
        {
            Assert.IsTrue(ShipFlight.RotatesClockwise(350f, 10f), "1. target > angle + 180");
            Assert.IsFalse(ShipFlight.RotatesClockwise(100f, 10f), "2.");
            Assert.IsFalse(ShipFlight.RotatesClockwise(10f, 350f), "3. angle > target + 180");
            Assert.IsTrue(ShipFlight.RotatesClockwise(10f, 100f), "4.");
            Assert.IsFalse(ShipFlight.RotatesClockwise(190f, 10f), "exactly half way, target larger: counter-clockwise");
            Assert.IsTrue(ShipFlight.RotatesClockwise(10f, 190f), "exactly half way, target smaller: clockwise");
            Assert.IsFalse(ShipFlight.RotatesClockwise(45f, 45f), "on target: turn = 0, not clockwise");
        }

        [Test]
        public void TheLeavingShipPuffsBlackAndOrangeSmokeEveryStep_AndNoSmokeTrailOrSpawnJet()
        {
            var rig = new PlayerRig();
            rig.Do(new LeaveLevel(P1));
            rig.Events.Clear();
            for (int i = 0; i < 120; i++) rig.Do(new StepFlight(P1, Step));
            Assert.AreEqual(120, rig.Seen<LeaveSmokePuffed>().Count(), "one ef_smokeup and one ef_smoke a step");
            Assert.AreEqual(0, rig.Seen<SpawnJetPuffed>().Count(), "the justspawned jet is in the other branch");
            Assert.AreEqual(0, rig.Seen<SmokeTrailPuffed>().Count(), "Alarm 2 puffs only in state_moving");
        }

        [Test]
        public void ADeadShipSentOffLeavesInsteadOfWaitingOutItsDeath_D21()
        {
            var rig = new PlayerRig();
            for (int i = 0; i < rig.Health.MaxHealth; i++)
            {
                rig.Do(new TickHealth(P1, 10f)); // past spawn protection and each hit's invulnerability
                rig.Do(new TakeHit(P1));
            }
            Assert.IsTrue(rig.Health.IsDead);

            Assert.IsTrue(rig.Do(new LeaveLevel(P1)).Accepted, "state_leave is set whatever the state");
            Assert.AreEqual(PlayerFlightState.Leaving, rig.Flight.State);
            Assert.IsFalse(rig.Health.IsDead);
            rig.Events.Clear();
            for (int i = 0; i < 1000; i++) rig.Do(new TickHealth(P1, Step));
            Assert.AreEqual(0, rig.Seen<PlayerRespawned>().Count(), "alarm_death_wait no longer puts it back to state_moving");
            Assert.AreEqual(PlayerFlightState.Leaving, rig.Flight.State);
        }

        [Test]
        public void SetupLevelsNewShipIsNoLongerLeavingOrInvulnerable()
        {
            var rig = new PlayerRig();
            rig.Do(new LeaveLevel(P1));
            rig.Do(new SetUpShip(P1));
            Assert.AreEqual(PlayerFlightState.Moving, rig.Flight.State);
            Assert.IsFalse(rig.Health.IsInvulnerable);
            Assert.AreEqual(ShipPose.Leap, rig.Ship.Pose);
        }

        // ---- The arena's leave step ----

        static ArenaWorld Arena()
        {
            var content = ContentRegistry.Default;
            var arena = new ArenaWorld(content.Arena, content.Rooms.Get(RoomId.GameScreen), content.Arena.SettingFor(1, RoomId.GameScreen), new SeededRandom(1));
            arena.Execute(new CreateArena(P1));
            return arena;
        }

        [Test]
        public void TheLeaveStepAimsAtExitXAboveTheRoom_ThenLeavesTheLevelOnce()
        {
            var arena = Arena();
            Assert.IsFalse(arena.Execute(new StepLeave(P1, 100f, 100f)).Accepted, "not leaving yet");
            Assert.IsTrue(arena.Execute(new SendPlayerOff(P1, 500f)).Accepted);
            Assert.IsFalse(arena.Execute(new SendPlayerOff(P1, 700f)).Accepted);
            Assert.AreEqual(500f, arena.ExitX);
            float top = -arena.Tuning.PlayerSize;

            var straightUp = arena.Execute(new StepLeave(P1, 500f, 1000f));
            Assert.AreEqual(90f, straightUp.Events.OfType<ExitAimed>().Single().Angle, 1e-3f, "point_direction straight up the screen");
            Assert.IsFalse(straightUp.Events.OfType<PlayerLeftLevel>().Any());

            var upLeft = arena.Execute(new StepLeave(P1, 500f + 1000f - top, 1000f));
            Assert.AreEqual(135f, upLeft.Events.OfType<ExitAimed>().Single().Angle, 1e-3f);

            Assert.IsFalse(arena.Execute(new StepLeave(P1, 500f, top)).Events.OfType<PlayerLeftLevel>().Any(), "not yet past -sprite_height");
            Assert.AreEqual(1, arena.Execute(new StepLeave(P1, 500f, top - 1f)).Events.OfType<PlayerLeftLevel>().Count());
            Assert.IsTrue(arena.PlayerExited);
            Assert.IsFalse(arena.Execute(new StepLeave(P1, 500f, top - 5f)).Events.OfType<PlayerLeftLevel>().Any(), "if(!exiting): once");
        }

        // ---- KILLS ----

        [Test]
        public void KillsCountWholeEnemiesTheShipsWeaponsDestroyed()
        {
            var at = new Vec2(0f, 0f);
            Assert.IsTrue(new EnemyKill(EnemyDeathCause.Bullet, at, 10, true, true).CountsAsKill, "the ship's own round");
            Assert.IsFalse(new EnemyKill(EnemyDeathCause.Bullet, at, 10, false, true).CountsAsKill, "a stray round scores nothing");
            foreach (var cause in new[] { EnemyDeathCause.Rocket, EnemyDeathCause.RocketExplosion, EnemyDeathCause.ShieldWall, EnemyDeathCause.Laser, EnemyDeathCause.Railgun, EnemyDeathCause.Flame })
                Assert.IsTrue(new EnemyKill(cause, at, 0, false, false).CountsAsKill, cause.ToString());
            foreach (var cause in new[] { EnemyDeathCause.Orphaned, EnemyDeathCause.Fuse, EnemyDeathCause.Proximity, EnemyDeathCause.Removed })
                Assert.IsFalse(new EnemyKill(cause, at, 0, true, false).CountsAsKill, cause.ToString());
            Assert.IsFalse(new EnemyKill(EnemyDeathCause.Rocket, at, 10, true, true, BodyPart.Segment).CountsAsKill, "a worm segment is a part");
        }
    }
}
