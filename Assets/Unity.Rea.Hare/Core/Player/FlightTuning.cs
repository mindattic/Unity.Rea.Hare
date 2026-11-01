using System;
using System.Collections.Generic;
using Unity.Rea.Hare.Content;

namespace Unity.Rea.Hare.Player
{
    /// <summary>Mirrors obj_player's top-level states (GML: state_idle / state_moving / state_dead / state_leave).</summary>
    public enum PlayerFlightState
    {
        Idle,
        Moving,
        Dead,
        /// <summary>GML state_leave (U.HARE-T22): obj_level_results' Enter sent the ship off the top of the room.</summary>
        Leaving
    }

    /// <summary>
    /// obj_player's flight numbers and obj_energybar's (U.HARE-T89).
    /// <see cref="FlightSpeed"/> and <see cref="BrakeSpeed"/> are the original's px a step, as <see cref="ShipFlight.Speed"/>
    /// holds them; <see cref="ShipFlight.WorldSpeed"/> turns them into world units a second (U.HARE-T88).
    /// </summary>
    [Serializable]
    public class FlightTuning
    {
        /// <summary>obj_player Create's flight_speed (and reset_player's speed = 5), px a step.</summary>
        public float FlightSpeed = 5f;
        /// <summary>obj_player Create's brake_speed, px a step.</summary>
        public float BrakeSpeed = 2.5f;
        public float TurnSpeedDegreesPerSecond = 120f;
        public float BrakeTurnSpeedDegreesPerSecond = 240f;
        /// <summary>
        /// The boost and brake ease, px a step gained or lost a second: obj_player Step's <c>speed -= 0.05</c> each step
        /// (x 120).
        /// </summary>
        public float SpeedChangeRatePerSecond = 6f;

        /// <summary>obj_energybar: boost, brake and shield need this fraction of the bar to start.</summary>
        public float MaxEnergy = 100f;
        public float MinEnergyFractionToStart = 0.33f;
        /// <summary>obj_energybar Create's <c>rate = 4</c>: Alarm 0 fires every this many steps and fills or drains.</summary>
        public int EnergyTickSteps = 4;
        /// <summary>obj_energybar Alarm 0, mode "fill": <c>currentValue += 1</c> until maxValue, then mode "idle".</summary>
        public float EnergyFillPerTick = 1f;
        /// <summary>
        /// obj_energybar Alarm 0, mode "drain": <c>if(currentValue &gt; 0) currentValue -= 1;</c> then
        /// <c>currentValue -= 1</c> again, held at 0 by its Step's range check: 2 a tick.
        /// </summary>
        public float EnergyDrainPerTick = 2f;

        /// <summary>jump_arc: how far from the ship's facing a leap may aim.</summary>
        public float JumpArcDegrees = 100f;

        /// <summary>spin = 3: degrees a step (x 120) the ship's facing turns while it flies spinning (Alt, a hit, fling_player).</summary>
        public float SpinDegreesPerSecond = 360f;

        /// <summary>was_spinning: image_angle += 3 a step (x 120) until it is back on the heading.</summary>
        public float SpinRecoverDegreesPerSecond = 360f;

        /// <summary>was_spinning's a_near_b(image_angle, direction, 3): this close, the facing snaps onto the heading.</summary>
        public float SpinSnapDegrees = 3f;

        /// <summary>state_leave's turn toward exit_angle: <c>direction -= 3</c> or <c>+= 3</c> a step (x 120).</summary>
        public float LeaveTurnDegreesPerSecond = 360f;

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
            ValidationHelpers.Positive(errors, path + ".EnergyTickSteps", EnergyTickSteps);
            ValidationHelpers.Positive(errors, path + ".EnergyFillPerTick", EnergyFillPerTick);
            ValidationHelpers.Positive(errors, path + ".EnergyDrainPerTick", EnergyDrainPerTick);
            ValidationHelpers.NonNegative(errors, path + ".JumpArcDegrees", JumpArcDegrees);
            ValidationHelpers.Positive(errors, path + ".SpinDegreesPerSecond", SpinDegreesPerSecond);
            ValidationHelpers.Positive(errors, path + ".SpinRecoverDegreesPerSecond", SpinRecoverDegreesPerSecond);
            ValidationHelpers.NonNegative(errors, path + ".SpinSnapDegrees", SpinSnapDegrees);
            ValidationHelpers.Positive(errors, path + ".LeaveTurnDegreesPerSecond", LeaveTurnDegreesPerSecond);
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
        /// <summary>reset_player: <c>direction = 270 + irandom_range(-30, 30)</c>, a whole number of degrees.</summary>
        public int RespawnDirectionJitter = 30;

        /// <summary>A hit's <c>alarm[alarm_flashing] = 1</c>: the first colour change comes a step after the hit.</summary>
        public float FlashFirstSteps = 1f;
        /// <summary>Alarm 5 re-arms itself every 3 steps.</summary>
        public float FlashSteps = 3f;

        /// <summary>player_hurt picks its sound with irandom_range(1, 8): sfx_hurt1..sfx_hurt8.</summary>
        public int HurtSounds = 8;

        /// <summary>obj_healthbar's column2: the first health pip's left edge on the 1920x1080 GUI layer.</summary>
        public int LostHealthColumnX = 140;
        /// <summary>spr_health_pip's width and height: the pips' spacing, and how far above the 36 px margin they sit.</summary>
        public int LostHealthPipWidth = 44;
        public int LostHealthPipHeight = 64;
        /// <summary>obj_healthbar draws the pips at <c>view_hview - height - 36</c>.</summary>
        public int LostHealthBottomMargin = 36;
        /// <summary>obj_losthealth's Step: <c>y -= 1; image_alpha -= 0.01</c> (unless paused), gone at alpha 0.</summary>
        public float LostHealthRisePerStep = 1f;
        public float LostHealthFadePerStep = 0.01f;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Positive(errors, path + ".FlashFirstSteps", FlashFirstSteps);
            ValidationHelpers.Positive(errors, path + ".FlashSteps", FlashSteps);
            ValidationHelpers.Positive(errors, path + ".HurtSounds", HurtSounds);
            ValidationHelpers.NonNegative(errors, path + ".LostHealthColumnX", LostHealthColumnX);
            ValidationHelpers.Positive(errors, path + ".LostHealthPipWidth", LostHealthPipWidth);
            ValidationHelpers.Positive(errors, path + ".LostHealthPipHeight", LostHealthPipHeight);
            ValidationHelpers.NonNegative(errors, path + ".LostHealthBottomMargin", LostHealthBottomMargin);
            ValidationHelpers.NonNegative(errors, path + ".LostHealthRisePerStep", LostHealthRisePerStep);
            ValidationHelpers.Positive(errors, path + ".LostHealthFadePerStep", LostHealthFadePerStep);
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
