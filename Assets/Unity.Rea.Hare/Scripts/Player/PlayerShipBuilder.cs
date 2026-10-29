using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Hare.Weapons;

namespace Unity.Hare.Player
{
    /// <summary>
    /// Composes obj_player in code: the flight controller, health and the shared ammo, one gun component per
    /// <see cref="GunMount"/> of a <see cref="ShipLoadout"/> (obj_player Create's guns by default: vulcan, laser,
    /// railgun, shotgun and flamer on the perched primary, the shotgun on the perched secondary, the mine launcher on
    /// the air secondary), then the ground loadout (Q's next_ground_weapon) and the air loadout (the rocket and mine
    /// air primary, E's next_air_weapon). The editor's scene setup builds the main scene's ship with it, and an
    /// authority that holds a shared <see cref="PlayerWorld"/> seats each ship it builds through the same path.
    /// </summary>
    public static class PlayerShipBuilder
    {
        /// <summary>
        /// Builds a ship at <paramref name="position"/>. The loadout is validated first and a bad one throws.
        /// With <paramref name="world"/> the ship is seated as <paramref name="id"/> in that world before any view
        /// touches it; without, it gets a world of its own (local play). The components wake together once every
        /// one is on the ship. In Edit Mode, where Awake never runs, <paramref name="resetInEditMode"/> runs each
        /// component's reset in the same order (tests); the editor leaves it off when it saves the ship into a scene,
        /// so Awake sets it up in play.
        /// </summary>
        public static GameObject Build(Vector3 position, ShipLoadout loadout = null, PlayerWorld world = null,
            PlayerId? id = null, string name = "Player", bool resetInEditMode = true)
        {
            loadout = loadout ?? new ShipLoadout();
            loadout.EnsureValid();

            var go = new GameObject(name);
            go.SetActive(false); // every component is on before any Awake runs
            go.transform.position = position;

            if (world != null) go.AddComponent<PlayerSeat>().Bind(world, id ?? new PlayerId(1));

            var resets = new List<Action>();
            var ship = go.AddComponent<PlayerShipController>();
            resets.Add(ship.ResetState);
            var health = go.AddComponent<PlayerHealth>();
            resets.Add(health.ResetState);
            var ammo = go.AddComponent<PlayerAmmo>();
            resets.Add(ammo.ResetState);
            var look = go.AddComponent<PlayerLook>();
            resets.Add(look.ResetState);

            foreach (var mount in loadout.Guns) resets.Add(Mount(go, mount));

            var ground = go.AddComponent<GroundWeaponLoadout>();
            resets.Add(ground.ResetState);
            var air = go.AddComponent<AirWeaponLoadout>();
            resets.Add(air.ResetState);

            go.SetActive(true);
            if (!Application.isPlaying && resetInEditMode)
                foreach (var reset in resets) reset();
            return go;
        }

        /// <summary>Adds the component that carries <paramref name="mount"/> and returns its reset.</summary>
        static Action Mount(GameObject go, GunMount mount)
        {
            if (mount.Type == GunType.Vulcan) return go.AddComponent<VulcanWeapon>().ResetState; // fire_primary only

            PerchedWeapon gun;
            switch (mount.Type)
            {
                case GunType.Laser: gun = go.AddComponent<LaserGun>(); break;
                case GunType.Railgun: gun = go.AddComponent<RailgunGun>(); break;
                case GunType.Shotgun: gun = go.AddComponent<Shotgun>(); break;
                case GunType.Flamer: gun = go.AddComponent<Flamethrower>(); break;
                case GunType.MineLauncher: gun = go.AddComponent<MineLauncher>(); break;
                default: throw new ArgumentOutOfRangeException(nameof(mount), mount.Type, "No component carries this gun.");
            }
            gun.Slot = mount.Slot; // not installed yet, so this only sets the slot Awake installs it under
            return gun.ResetState;
        }
    }
}
