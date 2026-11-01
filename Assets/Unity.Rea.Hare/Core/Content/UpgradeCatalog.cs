using System;
using System.Collections.Generic;
using Unity.Rea.Hare.Weapons;

namespace Unity.Rea.Hare.Content
{
    /// <summary>What a garage purchase improves: one row of room_upgrade's pips (U.HARE-T24).</summary>
    public enum UpgradeTrack
    {
        /// <summary>The "Rate of Fire" row: obj_upgrade_vulcan1, "Increase Vulcan Cannon's fire rate by 5%".</summary>
        VulcanRateOfFire,
        /// <summary>The "Reload" row: the shotgun's re-arm wait (alarm_reset_fire_secondary).</summary>
        ShotgunReload,
        /// <summary>The "Spread" row: obj_upgrade_shotgun1, "Decrease Shotgun's spread by 5%".</summary>
        ShotgunSpread,
        /// <summary>The "Power" row: obj_upgrade_missle1, "Increase missles explosive radius".</summary>
        MissilePower
    }

    /// <summary>
    /// One garage purchase: one of room_upgrade's obj_upgrade_vulcan1 / obj_upgrade_shotgun1 / obj_upgrade_missle1
    /// pips (spr_upgrade_circle_empty, filled once bought). Its left press: <c>if(SKILLPOINTS &gt; 0) { SKILLPOINTS -= 1;
    /// sprite_index = spr_upgrade_circle_filled; //Write to Ini here }</c>.
    /// </summary>
    [Serializable]
    public class UpgradeCost
    {
        /// <summary>A short, unique key the save keeps (e.g. "vulcan.rate.1"), not the GML object name.</summary>
        public string Id = "";
        /// <summary>SKILLPOINTS spent: 1 for every pip in the original.</summary>
        public int Cost = 1;
        /// <summary>The GML object the pip is.</summary>
        public string Object = "obj_upgrade_vulcan1";
        /// <summary>The row it belongs to, and so what it improves.</summary>
        public UpgradeTrack Track;
        /// <summary>Its place in room_upgrade (the sprite's origin is its top-left corner).</summary>
        public int X;
        public int Y;
        /// <summary>Create's <c>message</c>, drawn at <c>(room_width / 2, room_height - 30)</c> while the mouse is over it.</summary>
        public string Message = "";

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Id", Id);
            ValidationHelpers.Positive(errors, path + ".Cost", Cost);
            ValidationHelpers.Required(errors, path + ".Object", Object);
            ValidationHelpers.Required(errors, path + ".Message", Message);
            if (!Enum.IsDefined(typeof(UpgradeTrack), Track)) errors.Add($"{path}.Track: not a track (was {(int)Track}).");
        }
    }

    /// <summary>How much one bought pip of a row improves it, in percent.</summary>
    [Serializable]
    public class UpgradeTrackEffect
    {
        public UpgradeTrack Track;
        /// <summary>
        /// VulcanRateOfFire: the fire rate rises by this much a pip (the wait between rounds is divided by 1 + n x p).
        /// ShotgunReload and ShotgunSpread: the wait or the spread falls by this much a pip (times 1 - n x p).
        /// MissilePower: the rocket's explosion grows by this much a pip (times 1 + n x p).
        /// </summary>
        public float PercentPerUpgrade = 5f;

        internal void Validate(List<string> errors, string path)
        {
            if (!Enum.IsDefined(typeof(UpgradeTrack), Track)) errors.Add($"{path}.Track: not a track (was {(int)Track}).");
            ValidationHelpers.NonNegative(errors, path + ".PercentPerUpgrade", PercentPerUpgrade);
        }
    }

    /// <summary>
    /// What the garage sells and what it does (U.HARE-T24): room_upgrade's pips (<see cref="Upgrades"/>), each a
    /// SKILLPOINTS purchase, what each row's pips do to the ship's weapons (<see cref="Effects"/>), and how points are
    /// earned beyond globals.gml's <c>SKILLPOINTS = 5</c> (<see cref="SkillPointsPerMissionWon"/>). The room's layout
    /// is <see cref="Screen"/>. The defaults are empty; the data file holds the original's pips (docs/BIBLE.md V49).
    /// </summary>
    [Serializable]
    public class UpgradeCatalog
    {
        public UpgradeCost[] Upgrades = Array.Empty<UpgradeCost>();

        public UpgradeTrackEffect[] Effects = Array.Empty<UpgradeTrackEffect>();

        /// <summary>
        /// Skill points a map mission gives the first time it is won. The original starts with 5 and never gives
        /// another (intended, completed: docs/BIBLE.md D27).
        /// </summary>
        public int SkillPointsPerMissionWon = 1;

        /// <summary>room_upgrade's background, obj_btn_map, the weapon icons, the labels and the "Skill Points x" line.</summary>
        public Upgrade.UpgradeScreenTuning Screen = new Upgrade.UpgradeScreenTuning();

        public UpgradeCost Find(string id)
        {
            foreach (var upgrade in Upgrades)
                if (upgrade != null && upgrade.Id == id) return upgrade;
            return null;
        }

        /// <summary>The percent one pip of <paramref name="track"/> is worth (0 when the data gives the row none).</summary>
        public float PercentPerUpgrade(UpgradeTrack track)
        {
            foreach (var effect in Effects)
                if (effect != null && effect.Track == track) return effect.PercentPerUpgrade;
            return 0f;
        }

        /// <summary>The skill points <paramref name="owned"/> cost; ids the catalog does not know cost nothing.</summary>
        public int SpentOn(IEnumerable<string> owned)
        {
            int spent = 0;
            if (owned == null) return 0;
            foreach (var id in owned)
            {
                var upgrade = Find(id);
                if (upgrade != null) spent += upgrade.Cost;
            }
            return spent;
        }

        /// <summary>What the bought pips <paramref name="owned"/> do to a ship's weapons.</summary>
        public ShipUpgrades EffectsOf(IEnumerable<string> owned)
        {
            var counts = new Dictionary<UpgradeTrack, int>();
            if (owned != null)
                foreach (var id in owned)
                {
                    var upgrade = Find(id);
                    if (upgrade == null) continue;
                    counts.TryGetValue(upgrade.Track, out int n);
                    counts[upgrade.Track] = n + 1;
                }

            float Fraction(UpgradeTrack track)
            {
                counts.TryGetValue(track, out int n);
                return n * PercentPerUpgrade(track) / 100f;
            }

            return new ShipUpgrades(
                vulcanIntervalScale: 1f / (1f + Fraction(UpgradeTrack.VulcanRateOfFire)),
                shotgunIntervalScale: Math.Max(0.05f, 1f - Fraction(UpgradeTrack.ShotgunReload)),
                shotgunSpreadScale: Math.Max(0f, 1f - Fraction(UpgradeTrack.ShotgunSpread)),
                missileRadiusScale: 1f + Fraction(UpgradeTrack.MissilePower));
        }

        internal void Validate(List<string> errors, string path)
        {
            if (Screen == null) errors.Add(path + ".Screen: must not be null.");
            else Screen.Validate(errors, path + ".Screen");
            ValidationHelpers.NonNegative(errors, path + ".SkillPointsPerMissionWon", SkillPointsPerMissionWon);
            if (Upgrades == null) { errors.Add(path + ".Upgrades: must not be null."); Upgrades = Array.Empty<UpgradeCost>(); }
            if (Effects == null) { errors.Add(path + ".Effects: must not be null."); Effects = Array.Empty<UpgradeTrackEffect>(); }
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Upgrades.Length; i++)
            {
                var upgrade = Upgrades[i];
                string entryPath = $"{path}.Upgrades[{i}]";
                if (upgrade == null) { errors.Add($"{entryPath}: must not be null."); continue; }
                upgrade.Validate(errors, entryPath);
                if (!string.IsNullOrWhiteSpace(upgrade.Id) && !seen.Add(upgrade.Id))
                    errors.Add($"{entryPath}.Id: '{upgrade.Id}' is used by more than one upgrade.");
            }
            var tracks = new HashSet<UpgradeTrack>();
            for (int i = 0; i < Effects.Length; i++)
            {
                var effect = Effects[i];
                string entryPath = $"{path}.Effects[{i}]";
                if (effect == null) { errors.Add($"{entryPath}: must not be null."); continue; }
                effect.Validate(errors, entryPath);
                if (!tracks.Add(effect.Track)) errors.Add($"{entryPath}.Track: {effect.Track} has more than one effect.");
            }
        }
    }
}
