using System;
using System.Collections.Generic;

namespace Unity.Hare.Weapons
{
    /// <summary>One gun on the ship: which weapon, and which trigger it answers (left mouse or Ctrl + left mouse).</summary>
    [Serializable]
    public struct GunMount
    {
        public GunType Type;
        public WeaponSlot Slot;

        public GunMount(GunType type, WeaponSlot slot)
        {
            Type = type;
            Slot = slot;
        }

        public override string ToString() => Type + " (" + Slot + ")";
    }

    /// <summary>
    /// The guns a player ship is built with, as data. The default is obj_player's Create (gmlHARE): the perched
    /// primary cycle next_ground_weapon steps through (vulcan, laser, railgun, shotgun, flamer; ground_weapon =
    /// weapon_vulcan first), the shotgun as the perched secondary (ground_weapon_secondary = weapon_shotgun) and the
    /// mine launcher as the air secondary (air_weapon_secondary = weapon_mine). The air primary (rocket and mine,
    /// air_weapon = weapon_rocket) is <see cref="AirLoadout"/>'s, with its cycle in <see cref="WeaponTable.AirCycle"/>.
    /// </summary>
    [Serializable]
    public class ShipLoadout
    {
        public GunMount[] Guns =
        {
            new GunMount(GunType.Vulcan, WeaponSlot.Primary),
            new GunMount(GunType.Laser, WeaponSlot.Primary),
            new GunMount(GunType.Railgun, WeaponSlot.Primary),
            new GunMount(GunType.Shotgun, WeaponSlot.Primary),
            new GunMount(GunType.Flamer, WeaponSlot.Primary),
            new GunMount(GunType.Shotgun, WeaponSlot.Secondary),
            new GunMount(GunType.MineLauncher, WeaponSlot.Secondary),
        };

        /// <summary>
        /// What is wrong with this loadout, empty when it can be built: every gun at most once per slot, the vulcan
        /// only on fire_primary (its spin-up answers the left mouse alone), the mine launcher only on fire_secondary
        /// (the air primary's mine is <see cref="AirLoadout"/>'s), and at most one perched secondary, since
        /// fire_secondary fires a single ground_weapon_secondary.
        /// </summary>
        public IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            if (Guns == null) { errors.Add("The loadout has no gun list."); return errors; }

            var seen = new HashSet<GunMount>();
            int perchedSecondaries = 0;
            foreach (var mount in Guns)
            {
                if (!Enum.IsDefined(typeof(GunType), mount.Type) || !Enum.IsDefined(typeof(WeaponSlot), mount.Slot))
                {
                    errors.Add("Unknown gun mount " + (int)mount.Type + "/" + (int)mount.Slot + ".");
                    continue;
                }
                if (!seen.Add(mount)) errors.Add(mount + " is mounted twice.");
                if (mount.Type == GunType.Vulcan && mount.Slot != WeaponSlot.Primary)
                    errors.Add("The vulcan only answers fire_primary.");
                if (mount.Type == GunType.MineLauncher && mount.Slot != WeaponSlot.Secondary)
                    errors.Add("The mine launcher only answers fire_secondary.");
                if (mount.Type != GunType.MineLauncher && mount.Slot == WeaponSlot.Secondary) perchedSecondaries++;
            }
            if (perchedSecondaries > 1) errors.Add("fire_secondary fires one perched weapon; " + perchedSecondaries + " are mounted.");
            return errors;
        }

        /// <summary>Throws with every problem <see cref="Validate"/> finds; loaders call it before building a ship.</summary>
        public void EnsureValid()
        {
            var errors = Validate();
            if (errors.Count > 0) throw new InvalidOperationException("Invalid ship loadout: " + string.Join(" ", errors));
        }
    }
}
