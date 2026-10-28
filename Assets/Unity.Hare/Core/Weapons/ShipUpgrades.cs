namespace Unity.Hare.Weapons
{
    /// <summary>
    /// What the garage's bought pips do to one ship's weapons (U.HARE-T24, docs/BIBLE.md V49): scales on the vulcan's
    /// wait between rounds, the shotgun's re-arm wait and spread, and the rocket's explosion. Built from the save by
    /// <c>UpgradeCatalog.EffectsOf</c> and fitted to a ship with <c>FitUpgrades</c>; <see cref="None"/> is the
    /// original's ship with nothing bought.
    /// </summary>
    public sealed class ShipUpgrades
    {
        public static readonly ShipUpgrades None = new ShipUpgrades(1f, 1f, 1f, 1f);

        public ShipUpgrades(float vulcanIntervalScale, float shotgunIntervalScale, float shotgunSpreadScale, float missileRadiusScale)
        {
            VulcanIntervalScale = vulcanIntervalScale;
            ShotgunIntervalScale = shotgunIntervalScale;
            ShotgunSpreadScale = shotgunSpreadScale;
            MissileRadiusScale = missileRadiusScale;
        }

        /// <summary>Times the vulcan's fire_rate wait (the "Rate of Fire" row: below 1 fires faster).</summary>
        public float VulcanIntervalScale { get; }

        /// <summary>Times the shotgun's alarm_reset_fire_secondary (the "Reload" row).</summary>
        public float ShotgunIntervalScale { get; }

        /// <summary>Times shoot_shotgun's spread of -8..+8 degrees (the "Spread" row).</summary>
        public float ShotgunSpreadScale { get; }

        /// <summary>Times obj_rocket_explosion's size and reach for the ship's own rockets (the "Power" row).</summary>
        public float MissileRadiusScale { get; }

        /// <summary>The scale on the re-arm wait of a gun of <paramref name="type"/>.</summary>
        public float IntervalScale(GunType type)
        {
            switch (type)
            {
                case GunType.Vulcan: return VulcanIntervalScale;
                case GunType.Shotgun: return ShotgunIntervalScale;
                default: return 1f;
            }
        }
    }
}
