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
            Refresh();
        }

        /// <summary>
        /// Re-reads the ship's ground primaries (after adding or removing one, e.g. an upgrade). Keeps the equipped
        /// weapon if it is still there, else equips the first.
        /// </summary>
        internal void Refresh()
        {
            weapons.Clear();
            foreach (var gun in rack.Guns)
                if (gun.IsGroundPrimary && !weapons.Contains(gun.GroundKind.Value)) weapons.Add(gun.GroundKind.Value);
            var table = rack.Table;
            weapons.Sort((a, b) => table.GroundOrder(a).CompareTo(table.GroundOrder(b)));
            if (current == null || !weapons.Contains(current.Value)) current = weapons.Count > 0 ? weapons[0] : (GroundWeapon?)null;
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
    /// The player's secondary air weapons (GML fire_secondary / next_air_weapon on obj_player): the unlocked
    /// weapons, cycled in <see cref="WeaponTable.AirCycle"/> order, and the current one fired while the trigger is
    /// held. Firing is refused while dead or shielding, and costs ammo from the shared pools.
    ///
    /// Per weapon, as in the GML: the laser (shoot_laser/obj_laser) stays on until the trigger is released and only
    /// then can fire again; the railgun and rockets re-arm after the table's intervals. Switching weapons re-arms
    /// immediately (next_air_weapon: can_shoot = true).
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

        /// <summary>GML: can_shoot for the secondary slot.</summary>
        public bool CanShoot { get; private set; } = true;
        public bool TriggerHeld { get; private set; }

        /// <summary>True while the laser beam is on (obj_laser exists).</summary>
        public bool IsLaserOn { get; private set; }

        public IReadOnlyList<AirWeapon> Unlocked => unlocked;

        public bool IsUnlocked(AirWeapon weapon) => unlocked.Contains(weapon);

        internal void Reset()
        {
            IsActive = true;
            StopLaser();
            Current = unlocked.Count > 0 ? unlocked[0] : AirWeapon.Rocket;
            CanShoot = true;
            cooldown = 0f;
            if (TriggerHeld) ammo.ReleaseTrigger(TriggerHolder);
            TriggerHeld = false;
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

            StopLaser();
            Current = next;
            CanShoot = true;
            cooldown = 0f;
            sink.Emit(new AirWeaponSwitched { Weapon = Current });
            return true;
        }

        /// <summary>
        /// The secondary trigger. Releasing it drops the laser and re-arms it (GML global left release:
        /// with(obj_laser) instance_destroy(); can_shoot = true) and restarts ammo regen; pressing stops the primary regen.
        /// </summary>
        internal void SetTrigger(bool held)
        {
            if (held == TriggerHeld) return;
            TriggerHeld = held;
            if (held)
            {
                ammo.PressTrigger(TriggerHolder);
                return;
            }

            if (IsLaserOn || Current == AirWeapon.Laser)
            {
                StopLaser();
                CanShoot = true;
            }
            ammo.ReleaseTrigger(TriggerHolder);
        }

        /// <summary>Re-arm timer and firing while the trigger is held.</summary>
        internal void Tick(float dt)
        {
            if (IsLaserOn && (flight.State == PlayerFlightState.Dead || flight.IsShielding)) StopLaser();

            if (!CanShoot && cooldown > 0f)
            {
                cooldown -= dt;
                if (cooldown <= 0f) { cooldown = 0f; CanShoot = true; } // GML alarm 0: can_shoot = true
            }

            if (TriggerHeld) TryFire();
        }

        /// <summary>GML fire_secondary for the current air weapon. Returns true if a shot went out.</summary>
        internal bool TryFire()
        {
            if (!CanShoot || !IsUnlocked(Current) || flight.State == PlayerFlightState.Dead || flight.IsShielding) return false;

            if (!ammo.TrySpend(AmmoPools.PoolFor(Current))) // GML: x_ammo_primary >= cost, then subtract and clamp at 0
            {
                sink.Emit(new AirWeaponDryFired { Weapon = Current });
                return false;
            }
            CanShoot = false;

            if (Current == AirWeapon.Laser) IsLaserOn = true;
            cooldown = GameClock.Seconds(table.AirIntervalSteps(Current)); // the laser: re-armed only by releasing the trigger
            sink.Emit(new AirWeaponFired { Weapon = Current });
            return true;
        }

        void StopLaser()
        {
            if (!IsLaserOn) return;
            IsLaserOn = false;
            sink.Emit(new AirLaserStopped());
        }
    }
}
