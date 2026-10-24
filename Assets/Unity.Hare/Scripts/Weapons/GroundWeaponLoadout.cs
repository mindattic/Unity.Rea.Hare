using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Hare.Player;

namespace Unity.Hare.Weapons
{
    /// <summary>A weapon <see cref="GroundWeaponLoadout"/> can equip in the ground primary slot.</summary>
    public interface IGroundWeapon
    {
        GroundWeapon GroundKind { get; }
        /// <summary>True when it answers fire_primary (left mouse); only those take part in the cycle.</summary>
        bool IsGroundPrimary { get; }
    }

    /// <summary>
    /// The view on next_ground_weapon (gmlHARE, obj_player's Q key press): the ground primary slot (what left mouse
    /// fires while perched) steps through the weapon table's ground cycle (vulcan, laser, railgun, shotgun, flamer)
    /// and wraps, playing sfx_switch and re-arming the gun. The cycle and the equip rules live in Core's
    /// <see cref="GroundLoadout"/>. Here every ground primary is its own component on the ship
    /// (<see cref="VulcanWeapon"/>, <see cref="Shotgun"/>, <see cref="Flamethrower"/>, all
    /// <see cref="IGroundWeapon"/>s); this component turns Q into the command and enables only the equipped one, so
    /// a single left click fires one weapon. The ground secondary (Ctrl + left mouse) is not part of the cycle.
    /// </summary>
    [RequireComponent(typeof(PlayerShipController))]
    public class GroundWeaponLoadout : SeatView
    {
        readonly List<IGroundWeapon> weapons = new List<IGroundWeapon>();

        /// <summary>The equipped ground primary, or null when the ship has none.</summary>
        public GroundWeapon? Current => Core.Ground.Current;
        public IGroundWeapon CurrentWeapon => Find(Current);
        /// <summary>The ground primaries in cycling order.</summary>
        public IReadOnlyList<IGroundWeapon> Weapons => weapons;

        /// <summary>GML: audio_play_sound(sfx_switch) in next_ground_weapon.</summary>
        public event Action<GroundWeapon> Switched;

        void Awake() => ResetState();

        /// <summary>Awake's work, callable from EditMode tests (Awake does not run there). Equips the first in order.</summary>
        public void ResetState()
        {
            Send(new ResetGroundLoadout(Id));
            Rebuild();
        }

        /// <summary>
        /// Re-reads the ship's ground primaries (call after adding or removing one, e.g. an upgrade). Keeps the
        /// equipped weapon if it is still there, else equips the first.
        /// </summary>
        public void Refresh()
        {
            Send(new RefreshGroundLoadout(Id));
            Rebuild();
        }

        public bool Has(GroundWeapon kind) => Core.Ground.Has(kind);

        /// <summary>GML next_ground_weapon. Returns false when there is nothing else to switch to.</summary>
        public bool NextGroundWeapon() => Send(new NextGroundWeapon(Id)).Accepted;

        /// <summary>Equips <paramref name="kind"/> directly (upgrade screen, tests). False if the ship lacks it.</summary>
        public bool Equip(GroundWeapon kind) => Send(new EquipGroundWeapon(Id, kind)).Accepted;

        IGroundWeapon Find(GroundWeapon? kind) => kind == null ? null : weapons.Find(w => w.GroundKind == kind.Value);

        /// <summary>The weapon components for the kinds the rules cycle through, in cycle order.</summary>
        void Rebuild()
        {
            weapons.Clear();
            var components = GetComponents<IGroundWeapon>();
            foreach (var kind in Core.Ground.Weapons)
            {
                foreach (var weapon in components)
                {
                    if (!weapon.IsGroundPrimary || weapon.GroundKind != kind) continue;
                    weapons.Add(weapon);
                    break;
                }
            }
            Apply();
        }

        /// <summary>Only the equipped weapon runs (reads the trigger, fires, regenerates).</summary>
        void Apply()
        {
            var current = Current;
            foreach (var weapon in weapons)
                if (weapon is Behaviour behaviour) behaviour.enabled = weapon.GroundKind == current;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.qKey.wasPressedThisFrame) NextGroundWeapon(); // GML key press 81
        }

        protected override void OnPlayerEvent(PlayerEvent e)
        {
            if (e is GroundWeaponSwitched switched)
            {
                Apply();
                Switched?.Invoke(switched.Weapon);
            }
        }
    }
}
