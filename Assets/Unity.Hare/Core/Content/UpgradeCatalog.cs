using System;
using System.Collections.Generic;

namespace Unity.Hare.Content
{
    /// <summary>One garage purchase (obj_upgrade_vulcan1, obj_upgrade_shotgun1, obj_upgrade_missle1, the schematics).</summary>
    [Serializable]
    public class UpgradeCost
    {
        /// <summary>A short, unique key (e.g. "vulcan1", "rabbit_schematic"), not the GML object name.</summary>
        public string Id = "";
        /// <summary>SKILLPOINTS spent (room_upgrade spends SKILLPOINTS; docs/RECONSTRUCTION.md).</summary>
        public int Cost;

        internal void Validate(List<string> errors, string path)
        {
            ValidationHelpers.Required(errors, path + ".Id", Id);
            ValidationHelpers.NonNegative(errors, path + ".Cost", Cost);
        }
    }

    /// <summary>
    /// The garage/upgrade screen's prices (docs/RECONSTRUCTION.md's "Garage and upgrades" row, U.HARE-T24). Empty
    /// until that ticket reconstructs the originals' SKILLPOINTS costs; the registry validates whatever is here
    /// today so that ticket adds prices to a data file instead of hardcoding them on the upgrade screen.
    /// </summary>
    [Serializable]
    public class UpgradeCatalog
    {
        public UpgradeCost[] Upgrades = Array.Empty<UpgradeCost>();

        /// <summary>room_upgrade's background and obj_btn_map, its way back to room_map (U.HARE-T64).</summary>
        public Upgrade.UpgradeScreenTuning Screen = new Upgrade.UpgradeScreenTuning();

        public UpgradeCost Find(string id)
        {
            foreach (var upgrade in Upgrades)
                if (upgrade != null && upgrade.Id == id) return upgrade;
            return null;
        }

        internal void Validate(List<string> errors, string path)
        {
            if (Screen == null) errors.Add(path + ".Screen: must not be null.");
            else Screen.Validate(errors, path + ".Screen");
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
        }
    }
}
