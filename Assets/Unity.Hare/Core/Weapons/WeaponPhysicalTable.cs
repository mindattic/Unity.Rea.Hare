using System;
using System.Collections.Generic;
using Unity.Hare.Content;

namespace Unity.Hare.Weapons
{
    /// <summary>The perched weapons' shared muzzle (GML gunX/gunY then originX/originY), in GML pixels.</summary>
    [Serializable]
    public class PerchedMuzzleGeometry
    {
        /// <summary>GML lengthdir(-13, image_angle): the gun mount, behind the ship's facing.</summary>
        public float GunOffsetPx = -13f;
        /// <summary>GML lengthdir(34, mouse_angle): the muzzle, out along the aim from the gun mount.</summary>
        public float MuzzleDistancePx = 34f;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Positive(errors, path + ".MuzzleDistancePx", MuzzleDistancePx);
        }
    }

    /// <summary>obj_bullet's vulcan-specific numbers (shoot_vulcan).</summary>
    [Serializable]
    public class VulcanPhysicalStats
    {
        public float BulletSpeed = 8f;
        public float BulletLifetime = 3f;
        public float BulletRadius = 0.05f;
        /// <summary>GML originX/originY = x/y + lengthdir(34, mouse_angle); 1 unit = 100 GML px (<see cref="GameClock"/>).</summary>
        public float MuzzleDistance = 0.34f;
        /// <summary>GML Y + irandom_range(-5, 5): 5 px of jitter, in units.</summary>
        public float MuzzleJitter = 0.05f;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Positive(errors, path + ".BulletSpeed", BulletSpeed);
            ValidationHelpers.Positive(errors, path + ".BulletLifetime", BulletLifetime);
            ValidationHelpers.Positive(errors, path + ".BulletRadius", BulletRadius);
            ValidationHelpers.Positive(errors, path + ".MuzzleDistance", MuzzleDistance);
            ValidationHelpers.NonNegative(errors, path + ".MuzzleJitter", MuzzleJitter);
        }
    }

    /// <summary>shoot_shotgun's blast numbers.</summary>
    [Serializable]
    public class ShotgunPhysicalStats
    {
        public int PelletCount = 16;
        public int SpreadDegrees = 8;
        /// <summary>GML 12 px of stagger along the aim, in units.</summary>
        public float Grouping = 0.12f;
        public float PelletSpeed = 12f;
        /// <summary>
        /// Used only with no play room running: in one, a pellet (an obj_bullet) goes once past the room's buffer_zone
        /// (ArenaWorld.OutOfPlay, U.HARE-T21), as the vulcan's bullets do.
        /// </summary>
        public float PelletLifetime = 2f;
        /// <summary>spr_pellet is 6 px, in units.</summary>
        public float PelletRadius = 0.03f;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Positive(errors, path + ".PelletCount", PelletCount);
            ValidationHelpers.NonNegative(errors, path + ".SpreadDegrees", SpreadDegrees);
            ValidationHelpers.NonNegative(errors, path + ".Grouping", Grouping);
            ValidationHelpers.Positive(errors, path + ".PelletSpeed", PelletSpeed);
            ValidationHelpers.Positive(errors, path + ".PelletLifetime", PelletLifetime);
            ValidationHelpers.Positive(errors, path + ".PelletRadius", PelletRadius);
        }
    }

    /// <summary>shoot_flamer / obj_flame's stream numbers.</summary>
    [Serializable]
    public class FlamerPhysicalStats
    {
        public float FlameSpeed = 4f;
        /// <summary>42 frames at image_speed 0.3, in seconds.</summary>
        public float FlameLifetime = 42f / 0.3f / GameClock.StepsPerSecond;
        /// <summary>spr_powder_fire's 100 px bbox, in units; scales with the flame.</summary>
        public float FlameRadius = 0.5f;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Positive(errors, path + ".FlameSpeed", FlameSpeed);
            ValidationHelpers.Positive(errors, path + ".FlameLifetime", FlameLifetime);
            ValidationHelpers.Positive(errors, path + ".FlameRadius", FlameRadius);
        }
    }

    /// <summary>shoot_laser / shoot_railgun's beam length, in GML pixels.</summary>
    [Serializable]
    public class BeamPhysicalStats
    {
        public float BeamLengthPx = 2000f;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Positive(errors, path + ".BeamLengthPx", BeamLengthPx);
        }
    }

    /// <summary>
    /// The weapons' physical/visual tuning that used to live as [SerializeField] defaults on each weapon's
    /// MonoBehaviour (VulcanWeapon, Shotgun, Flamethrower, LaserGun, RailgunGun, PerchedWeapon, AirWeaponLoadout):
    /// bullet and pellet speeds, lifetimes, radii, and the ship's muzzle geometry. Sprites and audio clips stay as
    /// Inspector-wired asset references on those components; only the tuning numbers move here (AGENTS.md rule 4).
    /// </summary>
    [Serializable]
    public class WeaponPhysicalTable
    {
        /// <summary>The shared muzzle every perched weapon (shotgun, flamer, laser, railgun, mine launcher) fires from.</summary>
        public PerchedMuzzleGeometry PerchedMuzzle = new PerchedMuzzleGeometry();
        /// <summary>The air primary's own copy of the same geometry (AirWeaponLoadout), in GML pixels.</summary>
        public PerchedMuzzleGeometry AirMuzzle = new PerchedMuzzleGeometry { GunOffsetPx = -13f, MuzzleDistancePx = 34f };
        public VulcanPhysicalStats Vulcan = new VulcanPhysicalStats();
        public ShotgunPhysicalStats Shotgun = new ShotgunPhysicalStats();
        public FlamerPhysicalStats Flamer = new FlamerPhysicalStats();
        public BeamPhysicalStats Laser = new BeamPhysicalStats();
        public BeamPhysicalStats Railgun = new BeamPhysicalStats();

        internal void Validate(List<string> errors, string path)
        {
            PerchedMuzzle.Validate(errors, path + ".PerchedMuzzle");
            AirMuzzle.Validate(errors, path + ".AirMuzzle");
            Vulcan.Validate(errors, path + ".Vulcan");
            Shotgun.Validate(errors, path + ".Shotgun");
            Flamer.Validate(errors, path + ".Flamer");
            Laser.Validate(errors, path + ".Laser");
            Railgun.Validate(errors, path + ".Railgun");
        }
    }
}
