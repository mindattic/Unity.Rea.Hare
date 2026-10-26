using System;
using System.Collections.Generic;
using Unity.Hare.Content;

namespace Unity.Hare.Player
{
    /// <summary>Mirrors obj_player's top-level states (GML: state_idle / state_moving / state_dead).</summary>
    public enum PlayerFlightState
    {
        Idle,
        Moving,
        Dead
    }

    /// <summary>
    /// obj_player's flight numbers. Brake is half of flight speed and boost double, as in the original; absolute
    /// values are placeholders to retune once real sprites set the world scale (UH-T2 note), and the energy rates
    /// are this rebuild's own until obj_energybar is ported (UH-T15). Speeds are per second (GML px per step x 120).
    /// </summary>
    [Serializable]
    public class FlightTuning
    {
        public float FlightSpeed = 5f;
        public float BrakeSpeed = 2.5f;
        public float TurnSpeedDegreesPerSecond = 120f;
        public float BrakeTurnSpeedDegreesPerSecond = 240f;
        public float SpeedChangeRatePerSecond = 6f;

        /// <summary>obj_energybar: boost, brake and shield need this fraction of the bar to start.</summary>
        public float MaxEnergy = 100f;
        public float MinEnergyFractionToStart = 0.33f;
        public float EnergyDrainPerSecond = 40f;
        public float EnergyFillPerSecond = 25f;

        /// <summary>jump_arc: how far from the ship's facing a leap may aim.</summary>
        public float JumpArcDegrees = 100f;

        /// <summary>spin = 3: degrees a step (x 120) the ship's facing turns while it flies spinning (Alt, a hit, fling_player).</summary>
        public float SpinDegreesPerSecond = 360f;

        /// <summary>was_spinning: image_angle += 3 a step (x 120) until it is back on the heading.</summary>
        public float SpinRecoverDegreesPerSecond = 360f;

        /// <summary>was_spinning's a_near_b(image_angle, direction, 3): this close, the facing snaps onto the heading.</summary>
        public float SpinSnapDegrees = 3f;

        /// <summary>obj_shieldwall's hitbox radius, world units.</summary>
        public float ShieldRadius = 0.75f;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Positive(errors, path + ".FlightSpeed", FlightSpeed);
            ValidationHelpers.Positive(errors, path + ".BrakeSpeed", BrakeSpeed);
            ValidationHelpers.Positive(errors, path + ".TurnSpeedDegreesPerSecond", TurnSpeedDegreesPerSecond);
            ValidationHelpers.Positive(errors, path + ".BrakeTurnSpeedDegreesPerSecond", BrakeTurnSpeedDegreesPerSecond);
            ValidationHelpers.Positive(errors, path + ".SpeedChangeRatePerSecond", SpeedChangeRatePerSecond);
            ValidationHelpers.Positive(errors, path + ".MaxEnergy", MaxEnergy);
            if (MinEnergyFractionToStart < 0f || MinEnergyFractionToStart > 1f)
                errors.Add($"{path}.MinEnergyFractionToStart: must be between 0 and 1 (was {MinEnergyFractionToStart}).");
            ValidationHelpers.Positive(errors, path + ".EnergyDrainPerSecond", EnergyDrainPerSecond);
            ValidationHelpers.Positive(errors, path + ".EnergyFillPerSecond", EnergyFillPerSecond);
            ValidationHelpers.NonNegative(errors, path + ".JumpArcDegrees", JumpArcDegrees);
            ValidationHelpers.Positive(errors, path + ".SpinDegreesPerSecond", SpinDegreesPerSecond);
            ValidationHelpers.Positive(errors, path + ".SpinRecoverDegreesPerSecond", SpinRecoverDegreesPerSecond);
            ValidationHelpers.NonNegative(errors, path + ".SpinSnapDegrees", SpinSnapDegrees);
            ValidationHelpers.Positive(errors, path + ".ShieldRadius", ShieldRadius);
        }
    }

    /// <summary>
    /// obj_player's health and lives numbers (globals.gml, Create and the alarms), in the original's steps at
    /// room_gamescreen's 120 steps a second.
    /// </summary>
    [Serializable]
    public class HealthTuning
    {
        public int MaxHealth = 6;
        public int StartingLives = 3;

        /// <summary>flash_time, alarm_end_flash.</summary>
        public float InvulnerableSteps = 180f;
        /// <summary>alarm_justspawned = flash_time.</summary>
        public float SpawnProtectionSteps = 180f;
        /// <summary>alarm_hurting.</summary>
        public float HurtPoseSteps = 60f;
        /// <summary>alarm_death_wait.</summary>
        public float DeathWaitSteps = 320f;

        /// <summary>reset_player flies the ship back in heading down, give or take the jitter.</summary>
        public float RespawnDirection = 270f;
        public float RespawnDirectionJitter = 30f;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Positive(errors, path + ".MaxHealth", MaxHealth);
            ValidationHelpers.NonNegative(errors, path + ".StartingLives", StartingLives);
            ValidationHelpers.Positive(errors, path + ".InvulnerableSteps", InvulnerableSteps);
            ValidationHelpers.Positive(errors, path + ".SpawnProtectionSteps", SpawnProtectionSteps);
            ValidationHelpers.Positive(errors, path + ".HurtPoseSteps", HurtPoseSteps);
            ValidationHelpers.Positive(errors, path + ".DeathWaitSteps", DeathWaitSteps);
            ValidationHelpers.NonNegative(errors, path + ".RespawnDirectionJitter", RespawnDirectionJitter);
        }
    }
}
