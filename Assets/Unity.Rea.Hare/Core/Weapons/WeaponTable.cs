using System;
using System.Collections.Generic;
using Unity.Hare.Content;

namespace Unity.Hare.Weapons
{
    /// <summary>The ground (perched) primary weapons.</summary>
    public enum GroundWeapon
    {
        Vulcan,
        Laser,
        Railgun,
        Shotgun,
        Flamer
    }

    /// <summary>
    /// The air primary: what left mouse fires in flight (GML air_weapon). next_air_weapon (E) toggles rocket and
    /// mine; the laser and railgun are perched weapons (<see cref="GroundWeapon"/>).
    /// </summary>
    public enum AirWeapon
    {
        Rocket,
        Mine
    }

    /// <summary>GML: which fire script a weapon answers to. Left mouse = fire_primary, Ctrl + left mouse = fire_secondary.</summary>
    public enum WeaponSlot
    {
        Primary,
        Secondary
    }

    /// <summary>The weapons that live on the ship as a gun of their own (a trigger, a cooldown, a shot).</summary>
    public enum GunType
    {
        Vulcan,
        Shotgun,
        Flamer,
        MineLauncher,
        Laser,
        Railgun
    }

    /// <summary>The vulcan's sound cues, in the order the original plays them (the reloads are <see cref="Player.AmmoReloaded"/>).</summary>
    public enum VulcanSound
    {
        SpinUp,
        LoopStart,
        LoopStop,
        SpinDown,
        EmptyClip
    }

    /// <summary>
    /// The weapon rules as data: the order next_ground_weapon and next_air_weapon cycle in, every fire interval and
    /// the vulcan's spin-up ramp (in the original's steps at 120 a second), and what each shot does. Changing a
    /// weapon's rhythm or the cycle is a change here and nowhere else.
    /// </summary>
    [Serializable]
    public class WeaponTable
    {
        /// <summary>next_ground_weapon's order; a weapon the ship has no gun for is skipped.</summary>
        public GroundWeapon[] GroundCycle = { GroundWeapon.Vulcan, GroundWeapon.Laser, GroundWeapon.Railgun, GroundWeapon.Shotgun, GroundWeapon.Flamer };

        /// <summary>next_air_weapon's order (E toggles rocket and mine); also the air weapons the ship starts with.</summary>
        public AirWeapon[] AirCycle = { AirWeapon.Rocket, AirWeapon.Mine };

        /// <summary>
        /// The vulcan's fire_rate ramp: the first shot waits base_rate, every shot after it
        /// <see cref="VulcanSpinUpDecrementSteps"/> less, down to <see cref="VulcanMinRateSteps"/>; releasing the
        /// trigger drops it back to base_rate. gmlHARE fires at a fixed fire_rate = 9 (the ramp is commented out
        /// there), so base and minimum are both 9 and the ramp never moves; setting base above min brings it back.
        /// </summary>
        public int VulcanBaseRateSteps = 9;
        public int VulcanMinRateSteps = 9;
        public int VulcanSpinUpDecrementSteps = 3;

        /// <summary>alarm_reset_fire_secondary = room_speed.</summary>
        public float ShotgunIntervalSteps = 120f;

        /// <summary>shoot_flamer's alarm_reset_fire = 8.</summary>
        public float FlamerIntervalSteps = 8f;

        /// <summary>The mine launcher re-arms after the mine's delay (alarm_reset_fire_secondary).</summary>
        public float MineIntervalSteps = DefaultMineIntervalSteps;

        /// <summary>The mine's own delay = 30 (shoot_mine); <c>Mine.RearmSteps</c> reads it too.</summary>
        public const float DefaultMineIntervalSteps = 30f;

        /// <summary>shoot_railgun re-arms after 240 steps.</summary>
        public float RailgunIntervalSteps = 240f;

        /// <summary>shoot_rocket re-arms after 25 steps.</summary>
        public float RocketIntervalSteps = 25f;

        /// <summary>obj_bullet damage per round: the vulcan's bullet and each of the shotgun's pellets.</summary>
        public int VulcanBulletDamage = 1;
        public int ShotgunPelletDamage = 1;

        /// <summary>Steps between shots of a gun that is not the vulcan (the vulcan's wait is its ramp).</summary>
        public float GunIntervalSteps(GunType type)
        {
            switch (type)
            {
                case GunType.Vulcan: return VulcanBaseRateSteps;
                case GunType.Shotgun: return ShotgunIntervalSteps;
                case GunType.Flamer: return FlamerIntervalSteps;
                case GunType.Railgun: return RailgunIntervalSteps;
                case GunType.Laser: return 0f; // stays on; re-armed only by releasing the trigger
                default: return MineIntervalSteps;
            }
        }

        /// <summary>Steps before an air primary re-arms after a shot (shoot_rocket 25, shoot_mine the mine's delay).</summary>
        public float AirIntervalSteps(AirWeapon weapon)
        {
            switch (weapon)
            {
                case AirWeapon.Rocket: return RocketIntervalSteps;
                default: return MineIntervalSteps;
            }
        }

        /// <summary>The damage one round of this gun does (0 for guns that fire no bullets).</summary>
        public int BulletDamage(GunType type)
        {
            switch (type)
            {
                case GunType.Vulcan: return VulcanBulletDamage;
                case GunType.Shotgun: return ShotgunPelletDamage;
                default: return 0;
            }
        }

        internal int GroundOrder(GroundWeapon weapon)
        {
            int index = Array.IndexOf(GroundCycle, weapon);
            return index < 0 ? int.MaxValue : index;
        }

        internal int AirOrder(AirWeapon weapon)
        {
            int index = Array.IndexOf(AirCycle, weapon);
            return index < 0 ? int.MaxValue : index;
        }

        internal static GroundWeapon? GroundKindOf(GunType type)
        {
            switch (type)
            {
                case GunType.Vulcan: return GroundWeapon.Vulcan;
                case GunType.Shotgun: return GroundWeapon.Shotgun;
                case GunType.Flamer: return GroundWeapon.Flamer;
                case GunType.Laser: return GroundWeapon.Laser;
                case GunType.Railgun: return GroundWeapon.Railgun;
                default: return null;
            }
        }

        internal void Validate(List<string> errors, string path)
        {
            if (GroundCycle == null || GroundCycle.Length == 0) errors.Add($"{path}.GroundCycle: must list at least one weapon.");
            if (AirCycle == null || AirCycle.Length == 0) errors.Add($"{path}.AirCycle: must list at least one weapon.");
            ValidationHelpers.Positive(errors, path + ".VulcanBaseRateSteps", VulcanBaseRateSteps);
            ValidationHelpers.Positive(errors, path + ".VulcanMinRateSteps", VulcanMinRateSteps);
            if (VulcanMinRateSteps > VulcanBaseRateSteps)
                errors.Add($"{path}: VulcanMinRateSteps ({VulcanMinRateSteps}) must not be greater than VulcanBaseRateSteps ({VulcanBaseRateSteps}).");
            ValidationHelpers.NonNegative(errors, path + ".VulcanSpinUpDecrementSteps", VulcanSpinUpDecrementSteps);
            ValidationHelpers.Positive(errors, path + ".ShotgunIntervalSteps", ShotgunIntervalSteps);
            ValidationHelpers.Positive(errors, path + ".FlamerIntervalSteps", FlamerIntervalSteps);
            ValidationHelpers.Positive(errors, path + ".MineIntervalSteps", MineIntervalSteps);
            ValidationHelpers.Positive(errors, path + ".RailgunIntervalSteps", RailgunIntervalSteps);
            ValidationHelpers.Positive(errors, path + ".RocketIntervalSteps", RocketIntervalSteps);
            ValidationHelpers.NonNegative(errors, path + ".VulcanBulletDamage", VulcanBulletDamage);
            ValidationHelpers.NonNegative(errors, path + ".ShotgunPelletDamage", ShotgunPelletDamage);
        }
    }
}
