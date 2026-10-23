using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Hare.Player;

namespace Unity.Hare.Weapons
{
    /// <summary>The ground (perched) primary weapons, in next_ground_weapon's cycling order.</summary>
    public enum GroundWeapon
    {
        Vulcan,
        Laser,
        Railgun,
        Shotgun,
        Flamer
    }

    /// <summary>A weapon <see cref="GroundWeaponLoadout"/> can equip in the ground primary slot.</summary>
    public interface IGroundWeapon
    {
        GroundWeapon GroundKind { get; }
        /// <summary>True when it answers fire_primary (left mouse); only those take part in the cycle.</summary>
        bool IsGroundPrimary { get; }
        /// <summary>GML can_shoot = true.</summary>
        void Rearm();
        /// <summary>Let go of the trigger as the weapon is put away.</summary>
        void Holster();
    }

    /// <summary>
    /// Ports next_ground_weapon (gmlHARE, obj_player's Q key press): the ground primary slot
    /// (player.ground_weapon, what left mouse fires while perched) steps vulcan, laser, railgun,
    /// shotgun, flamer and wraps back to the vulcan, playing sfx_switch and re-arming the gun
    /// (can_shoot = true). Here every ground primary is its own component on the ship
    /// (<see cref="VulcanWeapon"/>, <see cref="Shotgun"/>, <see cref="Flamethrower"/>, all
    /// <see cref="IGroundWeapon"/>s); the loadout enables only the equipped one, so a single left
    /// click fires one weapon, and cycles over the ones present, skipping kinds the ship has no
    /// component for (the perched laser and railgun have none yet: the air loadout fires those).
    /// The ground secondary (Ctrl + left mouse, default the shotgun) is not part of the cycle.
    /// </summary>
    [RequireComponent(typeof(PlayerShipController))]
    public class GroundWeaponLoadout : MonoBehaviour
    {
        readonly List<IGroundWeapon> weapons = new List<IGroundWeapon>();
        IGroundWeapon current;

        /// <summary>The equipped ground primary, or null when the ship has none.</summary>
        public GroundWeapon? Current => current?.GroundKind;
        public IGroundWeapon CurrentWeapon => current;
        /// <summary>The ground primaries in cycling order.</summary>
        public IReadOnlyList<IGroundWeapon> Weapons => weapons;

        /// <summary>GML: audio_play_sound(sfx_switch) in next_ground_weapon.</summary>
        public event Action<GroundWeapon> Switched;

        void Awake() => ResetState();

        /// <summary>Awake's work, callable from EditMode tests (Awake does not run there). Equips the first in order.</summary>
        public void ResetState()
        {
            current = null;
            Refresh();
        }

        /// <summary>
        /// Re-reads the ship's ground primaries (call after adding or removing one, e.g. an
        /// upgrade). Keeps the equipped weapon if it is still there, else equips the first.
        /// </summary>
        public void Refresh()
        {
            weapons.Clear();
            foreach (var weapon in GetComponents<IGroundWeapon>())
            {
                if (!weapon.IsGroundPrimary) continue;
                if (weapons.Exists(w => w.GroundKind == weapon.GroundKind)) continue; // one per kind
                weapons.Add(weapon);
            }
            weapons.Sort((a, b) => a.GroundKind.CompareTo(b.GroundKind));
            if (current == null || !weapons.Contains(current)) current = weapons.Count > 0 ? weapons[0] : null;
            Apply();
        }

        public bool Has(GroundWeapon kind) => weapons.Exists(w => w.GroundKind == kind);

        /// <summary>
        /// GML next_ground_weapon: equip the next ground primary in order, wrapping around, and
        /// re-arm it. Returns false when there is nothing else to switch to.
        /// </summary>
        public bool NextGroundWeapon()
        {
            if (weapons.Count < 2 || current == null) return false;
            var next = weapons[(weapons.IndexOf(current) + 1) % weapons.Count];
            Equip(next);
            return true;
        }

        /// <summary>Equips <paramref name="kind"/> directly (upgrade screen, tests). False if the ship lacks it.</summary>
        public bool Equip(GroundWeapon kind)
        {
            var weapon = weapons.Find(w => w.GroundKind == kind);
            if (weapon == null) return false;
            if (weapon != current) Equip(weapon);
            return true;
        }

        void Equip(IGroundWeapon weapon)
        {
            current?.Holster();
            current = weapon;
            current.Rearm();
            Apply();
            Switched?.Invoke(current.GroundKind);
        }

        /// <summary>Only the equipped weapon runs (reads the trigger, fires, regenerates).</summary>
        void Apply()
        {
            foreach (var weapon in weapons)
                if (weapon is Behaviour behaviour) behaviour.enabled = weapon == current;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.qKey.wasPressedThisFrame) NextGroundWeapon(); // GML key press 81
        }
    }
}
