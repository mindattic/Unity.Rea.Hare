using System.Collections.Generic;
using Unity.Hare.Player;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// next_ground_weapon (gmlHARE, obj_player's Q key press): the ground primary slot (what left mouse fires while
    /// perched) steps through <see cref="WeaponTable.GroundCycle"/> and wraps, playing sfx_switch and re-arming the
    /// gun (can_shoot = true). It cycles over the ground primaries the ship carries, skipping kinds it has no gun
    /// for. The ground secondary (Ctrl + left mouse, default the shotgun) is not part of the cycle.
    /// </summary>
    public sealed class GroundLoadout
    {
        readonly EventSink sink;
        readonly GunRack rack;
        readonly List<GroundWeapon> weapons = new List<GroundWeapon>();
        GroundWeapon? current;

        internal GroundLoadout(EventSink sink, GunRack rack)
        {
            this.sink = sink;
            this.rack = rack;
            rack.GunsChanged += OnGunsChanged;
        }

        /// <summary>Follows installs and removals once set up, so the cycle never depends on the order the guns appear in.</summary>
        void OnGunsChanged()
        {
            if (!IsActive) return;
            Refresh();
            sink.Emit(new GroundLoadoutChanged());
        }

        /// <summary>True once a loadout has been set up for the ship; before that set_ammo_cost reads the guns directly.</summary>
        public bool IsActive { get; private set; }

        /// <summary>The equipped ground primary, or null when the ship has none.</summary>
        public GroundWeapon? Current => current;

        /// <summary>The ground primaries in cycling order.</summary>
        public IReadOnlyList<GroundWeapon> Weapons => weapons;

        public bool Has(GroundWeapon kind) => weapons.Contains(kind);

        /// <summary>Starts with nothing equipped, then picks up the first gun in order.</summary>
        internal void Reset()
        {
            IsActive = true;
            current = null;
            chosen = false;
            Refresh();
        }

        /// <summary>
        /// A new obj_player's Create (setup_level, U.HARE-T57): ground_weapon back to the cycle's first gun (the vulcan),
        /// equipped as a switch is (the old gun holstered, the new one re-armed, <see cref="GroundWeaponSwitched"/>) when it
        /// was holding another; then as if no one had chosen.
        /// </summary>
        internal void Restart()
        {
            if (!IsActive) { Reset(); return; }
            Refresh();
            if (weapons.Count > 0 && current != weapons[0]) Equip(weapons[0]);
            chosen = false;
        }

        /// <summary>True once next_ground_weapon (or an upgrade's equip) picked the weapon rather than Create.</summary>
        bool chosen;

        /// <summary>
        /// Re-reads the ship's ground primaries (after adding or removing one, e.g. an upgrade). Keeps the equipped
        /// weapon if the player chose it and it is still there, else equips the first in cycle order: until a switch,
        /// the ship holds obj_player Create's ground_weapon (the cycle's first, the vulcan) however its guns happened
        /// to be installed (a scene wakes its components in no promised order).
        /// </summary>
        internal void Refresh()
        {
            weapons.Clear();
            foreach (var gun in rack.Guns)
                if (gun.IsGroundPrimary && !weapons.Contains(gun.GroundKind.Value)) weapons.Add(gun.GroundKind.Value);
            var table = rack.Table;
            weapons.Sort((a, b) => table.GroundOrder(a).CompareTo(table.GroundOrder(b)));
            if (!chosen || current == null || !weapons.Contains(current.Value)) current = weapons.Count > 0 ? weapons[0] : (GroundWeapon?)null;
        }

        /// <summary>GML next_ground_weapon. False when there is nothing else to switch to.</summary>
        internal bool Next()
        {
            if (weapons.Count < 2 || current == null) return false;
            Equip(weapons[(weapons.IndexOf(current.Value) + 1) % weapons.Count]);
            return true;
        }

        /// <summary>Equips <paramref name="kind"/> directly (upgrade screen, tests). False if the ship lacks it.</summary>
        internal bool EquipKind(GroundWeapon kind)
        {
            if (!weapons.Contains(kind)) return false;
            if (current != kind) Equip(kind);
            return true;
        }

        void Equip(GroundWeapon kind)
        {
            if (current != null)
            {
                var old = PrimaryGun(current.Value);
                if (old != null) rack.Holster(old);
            }
            current = kind;
            chosen = true;
            var gun = PrimaryGun(kind);
            if (gun != null) rack.Rearm(gun);
            sink.Emit(new GroundWeaponSwitched { Weapon = kind });
        }

        Gun PrimaryGun(GroundWeapon kind)
        {
            foreach (var gun in rack.Guns)
                if (gun.IsGroundPrimary && gun.GroundKind == kind) return gun;
            return null;
        }
    }

    /// <summary>
    /// The air primary (GML fire_primary's in-air branch / next_air_weapon on obj_player): the rocket or the mine,
    /// toggled with E and fired with the left mouse while flying. Firing is refused while perched, dead or
    /// shielding, and costs air_ammo_primary from the shared pools. The rocket and the mine re-arm after the
    /// table's intervals; switching weapons re-arms immediately (next_air_weapon: can_shoot = true). The laser and
    /// railgun are perched weapons on Q's cycle (<see cref="GunRack"/>), and the mine as the air secondary
    /// (Ctrl + left mouse) is the mine launcher gun.
    /// </summary>
    public sealed class AirLoadout
    {
        /// <summary>The ammo trigger holder the air trigger takes.</summary>
        public const int TriggerHolder = int.MinValue;

        readonly EventSink sink;
        readonly ShipFlight flight;
        readonly AmmoPools ammo;
        readonly List<AirWeapon> unlocked = new List<AirWeapon>();
        readonly WeaponTable table;
        float cooldown;

        internal AirLoadout(EventSink sink, ShipFlight flight, AmmoPools ammo, WeaponTable table)
        {
            this.sink = sink;
            this.flight = flight;
            this.ammo = ammo;
            this.table = table;
            unlocked.AddRange(table.AirCycle);
        }

        /// <summary>True once a loadout has been set up for the ship; before that set_ammo_cost ignores the air weapon.</summary>
        public bool IsActive { get; private set; }

        public AirWeapon Current { get; private set; }

        /// <summary>GML: can_shoot for the primary slot.</summary>
        public bool CanShoot { get; private set; } = true;
        public bool TriggerHeld { get; private set; }

        public IReadOnlyList<AirWeapon> Unlocked => unlocked;

        public bool IsUnlocked(AirWeapon weapon) => unlocked.Contains(weapon);

        internal void Reset()
        {
            IsActive = true;
            Current = unlocked.Count > 0 ? unlocked[0] : AirWeapon.Rocket;
            CanShoot = true;
            cooldown = 0f;
            if (TriggerHeld) ammo.ReleaseTrigger(TriggerHolder);
            TriggerHeld = false;
        }

        /// <summary>
        /// A new obj_player's Create (setup_level, U.HARE-T57): air_weapon back to the first unlocked (the rocket), re-armed
        /// with the trigger up; <see cref="AirWeaponSwitched"/> when it was holding another.
        /// </summary>
        internal void Restart()
        {
            var before = Current;
            Reset();
            if (Current != before) sink.Emit(new AirWeaponSwitched { Weapon = Current });
        }

        /// <summary>Adds <paramref name="weapon"/> to the cycle (upgrades/pickups call this).</summary>
        internal void Unlock(AirWeapon weapon)
        {
            if (unlocked.Contains(weapon)) return;
            unlocked.Add(weapon);
            unlocked.Sort((a, b) => table.AirOrder(a).CompareTo(table.AirOrder(b)));
            if (unlocked.Count == 1) Current = weapon;
        }

        /// <summary>Removes <paramref name="weapon"/> from the cycle; if it was equipped, moves to the next one.</summary>
        internal void Lock(AirWeapon weapon)
        {
            if (!unlocked.Contains(weapon)) return;
            bool wasCurrent = Current == weapon;
            if (wasCurrent && unlocked.Count > 1) Next();
            unlocked.Remove(weapon);
        }

        /// <summary>GML next_air_weapon: equip the next unlocked air weapon in order, wrapping around, and re-arm.</summary>
        internal bool Next()
        {
            if (unlocked.Count == 0) return false;
            int index = unlocked.IndexOf(Current);
            var next = unlocked[(index + 1) % unlocked.Count];
            if (next == Current && index >= 0) return false;

            Current = next;
            CanShoot = true;
            cooldown = 0f;
            sink.Emit(new AirWeaponSwitched { Weapon = Current });
            return true;
        }

        /// <summary>The primary trigger (left mouse): pressing stops the primary regen, releasing restarts it.</summary>
        internal void SetTrigger(bool held)
        {
            if (held == TriggerHeld) return;
            TriggerHeld = held;
            if (held) ammo.PressTrigger(TriggerHolder);
            else ammo.ReleaseTrigger(TriggerHolder);
        }

        /// <summary>Re-arm timer and firing while the trigger is held.</summary>
        internal void Tick(float dt)
        {
            if (!CanShoot && cooldown > 0f)
            {
                cooldown -= dt;
                if (cooldown <= 0f) { cooldown = 0f; CanShoot = true; } // GML alarm 0: can_shoot = true
            }

            if (TriggerHeld) TryFire();
        }

        /// <summary>GML fire_primary's in-air branch for the current air weapon. Returns true if a shot went out.</summary>
        internal bool TryFire()
        {
            if (!CanShoot || !IsUnlocked(Current)) return false;
            if (flight.State == PlayerFlightState.Idle || flight.State == PlayerFlightState.Dead || flight.IsShielding) return false;

            if (!ammo.TrySpend(AmmoPools.PoolFor(Current))) // GML: air_ammo_primary >= cost, then subtract and clamp at 0
            {
                sink.Emit(new AirWeaponDryFired { Weapon = Current });
                return false;
            }
            CanShoot = false;
            cooldown = GameClock.Seconds(table.AirIntervalSteps(Current));
            sink.Emit(new AirWeaponFired { Weapon = Current });
            return true;
        }
    }
}
