using System;
using System.Collections.Generic;
using Unity.Hare.Player;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// One weapon the ship carries as a gun of its own: its slot (fire_primary or fire_secondary), its re-arm
    /// cooldown (GML can_shoot + alarm_reset_fire) and trigger, plus the vulcan's spin-up. Views install a gun under
    /// a key of their own and address it by that key afterwards.
    /// </summary>
    public sealed class Gun
    {
        internal Gun(int key, GunType type, WeaponSlot slot)
        {
            Key = key;
            Type = type;
            Slot = slot;
        }

        public int Key { get; }
        public GunType Type { get; }
        public WeaponSlot Slot { get; internal set; }

        /// <summary>Seconds until the gun can shoot again; 0 when it can.</summary>
        public float Cooldown { get; internal set; }
        public bool TriggerHeld { get; internal set; }

        /// <summary>GML: fire_rate, in steps. The wait the vulcan's next shot will set.</summary>
        public int FireRateSteps { get; internal set; }

        /// <summary>GML: played_spinup. True from the first shot until release or running dry.</summary>
        public bool IsSpinning { get; internal set; }

        /// <summary>True while the vulcan's fire loop (sfx_vulcan_loop) is running.</summary>
        public bool IsLoopPlaying { get; internal set; }

        /// <summary>True while the laser's beam is on (obj_laser exists).</summary>
        public bool IsBeamOn { get; internal set; }

        /// <summary>The laser went out or fired and stays disarmed until the trigger is released (GML can_shoot = false).</summary>
        public bool AwaitingRelease { get; internal set; }

        public bool CanShoot => Cooldown <= 0f && !AwaitingRelease;

        /// <summary>The ground weapon this gun is, or null for the mine launcher.</summary>
        public GroundWeapon? GroundKind => WeaponTable.GroundKindOf(Type);

        /// <summary>True when it answers fire_primary and so takes part in the ground cycle.</summary>
        public bool IsGroundPrimary => GroundKind != null && Slot == WeaponSlot.Primary;
    }

    /// <summary>
    /// The shared firing rules of fire_primary/fire_secondary (gmlHARE) for the weapons that sit on the ship as
    /// guns: the perched ones fire only from state_idle and the mine launcher in the air, never while shielding or
    /// dead, one shot per cooldown, and only with enough ammo for the shot (else sfx_empty_clip). Each gun spends
    /// the pool its slot and firing state select; the vulcan keeps its spin-up ramp too (fire_primary's weapon_vulcan
    /// branch, shoot_vulcan and stop_vulcan_sfx): the first shot waits base_rate and every shot after it a few steps
    /// less, down to the floor, and releasing the trigger or running dry spins the barrel down.
    /// </summary>
    public sealed class GunRack
    {
        readonly EventSink sink;
        readonly ShipFlight flight;
        readonly AmmoPools ammo;
        readonly List<Gun> guns = new List<Gun>();
        readonly WeaponTable table;

        internal GunRack(EventSink sink, ShipFlight flight, AmmoPools ammo, WeaponTable table)
        {
            this.sink = sink;
            this.flight = flight;
            this.ammo = ammo;
            this.table = table;
        }

        public WeaponTable Table => table;

        /// <summary>The garage's bought pips on this ship's guns (U.HARE-T24); <see cref="ShipUpgrades.None"/> by default.</summary>
        public ShipUpgrades Upgrades { get; internal set; } = ShipUpgrades.None;

        /// <summary>Raised after a gun is installed, re-seated or removed (the ground loadout follows it).</summary>
        internal Action GunsChanged;

        /// <summary>The guns in the order they were installed.</summary>
        public IReadOnlyList<Gun> Guns => guns;

        public Gun Find(int key) => guns.Find(g => g.Key == key);

        /// <summary>Installs a gun, or re-seats one already installed under this key in another slot.</summary>
        internal Gun Install(int key, GunType type, WeaponSlot slot)
        {
            var gun = Find(key);
            if (gun == null)
            {
                gun = new Gun(key, type, slot) { FireRateSteps = table.VulcanBaseRateSteps };
                guns.Add(gun);
                GunsChanged?.Invoke();
                return gun;
            }

            gun.Slot = slot;
            gun.Cooldown = 0f;
            gun.AwaitingRelease = false;
            gun.IsBeamOn = false;
            gun.TriggerHeld = false;
            ammo.ReleaseTrigger(key);
            gun.FireRateSteps = table.VulcanBaseRateSteps;
            gun.IsSpinning = false;
            gun.IsLoopPlaying = false;
            GunsChanged?.Invoke();
            return gun;
        }

        internal void Remove(int key)
        {
            var gun = Find(key);
            if (gun == null) return;
            StopBeam(gun);
            ammo.ReleaseTrigger(key);
            guns.Remove(gun);
            GunsChanged?.Invoke();
        }

        /// <summary>The pool this gun spends: by slot while perched, the mine launcher's air secondary.</summary>
        public static AmmoPool PoolFor(Gun gun)
        {
            if (gun.Type == GunType.MineLauncher) return AmmoPool.AirSecondary;
            return gun.Slot == WeaponSlot.Primary ? AmmoPool.GroundPrimary : AmmoPool.GroundSecondary;
        }

        bool InFiringState(Gun gun) =>
            gun.Type == GunType.MineLauncher ? flight.State == PlayerFlightState.Moving : flight.State == PlayerFlightState.Idle;

        /// <summary>True when a trigger pull right now would shoot.</summary>
        public bool CanFire(Gun gun) => InFiringState(gun) && !flight.IsShielding && gun.CanShoot && ammo.HasAmmo(PoolFor(gun));

        /// <summary>
        /// GML mouse1_down: pressing stops the primary regen alarm. The vulcan answers every press; other guns only
        /// a change.
        /// </summary>
        internal void PullTrigger(Gun gun)
        {
            if (gun.Type != GunType.Vulcan && gun.TriggerHeld) return;
            gun.TriggerHeld = true;
            ammo.PressTrigger(gun.Key);
        }

        /// <summary>
        /// Releasing restarts both regen alarms. The vulcan also spins down and drops back to base_rate
        /// (stop_vulcan_sfx; fire_rate = base_rate); the laser's beam goes out and it can shoot again
        /// (with(obj_laser) instance_destroy(); can_shoot = true).
        /// </summary>
        internal void ReleaseTrigger(Gun gun)
        {
            if (gun.Type == GunType.Laser)
            {
                StopBeam(gun);
                gun.AwaitingRelease = false;
            }
            if (gun.Type != GunType.Vulcan && !gun.TriggerHeld) return;
            gun.TriggerHeld = false;
            if (gun.Type == GunType.Vulcan)
            {
                if (gun.IsSpinning) SpinDown(gun);
                gun.FireRateSteps = table.VulcanBaseRateSteps;
            }
            ammo.ReleaseTrigger(gun.Key);
        }

        /// <summary>Put away by a weapon switch: let go of the trigger so ammo regen resumes.</summary>
        internal void Holster(Gun gun)
        {
            if (gun.TriggerHeld) ReleaseTrigger(gun);
        }

        /// <summary>GML next_ground_weapon / next_air_weapon: can_shoot = true.</summary>
        internal void Rearm(Gun gun)
        {
            gun.Cooldown = 0f;
            gun.AwaitingRelease = false;
        }

        /// <summary>One trigger pull. Returns true if the gun shot.</summary>
        internal bool TryFire(Gun gun)
        {
            if (gun.Type == GunType.Vulcan) return TryFireVulcan(gun);

            if (!InFiringState(gun) || flight.IsShielding) return false;
            if (!gun.CanShoot) return false;
            if (!ammo.TrySpend(PoolFor(gun))) // GML: x_ammo_y >= cost, then subtract and clamp at 0
            {
                sink.Emit(new GunDryFired { Key = gun.Key, Type = gun.Type });
                return false;
            }

            if (gun.Type == GunType.Laser)
            {
                // The laser stays on and is re-armed only by releasing the trigger.
                gun.IsBeamOn = true;
                gun.AwaitingRelease = true;
            }
            else
            {
                gun.Cooldown = GameClock.Seconds(table.GunIntervalSteps(gun.Type)) * Upgrades.IntervalScale(gun.Type);
            }
            sink.Emit(new GunFired { Key = gun.Key, Type = gun.Type });
            return true;
        }

        /// <summary>The re-arm cooldown, and for the vulcan the held-trigger fire too (regen is the ammo's own tick).</summary>
        internal void Tick(Gun gun, float dt)
        {
            if (gun.Type != GunType.Vulcan)
            {
                if (gun.IsBeamOn && (flight.State == PlayerFlightState.Dead || flight.IsShielding)) StopBeam(gun);
                gun.Cooldown -= dt;
                if (gun.Cooldown <= 1e-5f) gun.Cooldown = 0f; // whole GML steps must not leave float dust
                return;
            }

            // Snap float residue to zero so a 30-step wait is 30 steps, not 31.
            if (gun.Cooldown > 0f) gun.Cooldown = gun.Cooldown - dt < 1e-5f ? 0f : gun.Cooldown - dt;

            // A ship that dies mid-burst can't keep the barrel spinning.
            if (gun.IsSpinning && flight.State == PlayerFlightState.Dead) SpinDown(gun);

            if (gun.TriggerHeld) TryFireVulcan(gun);
        }

        /// <summary>GML: fire_primary's ground branch with weapon_vulcan. An empty gun clicks and spins down instead.</summary>
        bool TryFireVulcan(Gun gun)
        {
            if (!gun.CanShoot || flight.State != PlayerFlightState.Idle || flight.IsShielding) return false;

            var pool = PoolFor(gun);
            if (!ammo.HasAmmo(pool))
            {
                sink.Emit(new GunDryFired { Key = gun.Key, Type = gun.Type });
                if (gun.IsSpinning) SpinDown(gun);
                return false;
            }

            if (!gun.IsSpinning)
            {
                gun.IsSpinning = true;
                Cue(gun, VulcanSound.SpinUp);
            }

            ammo.Subtract(pool); // subtract_ground_ammo

            if (!gun.IsLoopPlaying)
            {
                gun.IsLoopPlaying = true;
                Cue(gun, VulcanSound.LoopStart);
            }

            gun.Cooldown = GameClock.Seconds(gun.FireRateSteps) * Upgrades.VulcanIntervalScale;
            gun.FireRateSteps = Math.Max(table.VulcanMinRateSteps, gun.FireRateSteps - table.VulcanSpinUpDecrementSteps);

            sink.Emit(new GunFired { Key = gun.Key, Type = gun.Type });
            return true;
        }

        void StopBeam(Gun gun)
        {
            if (!gun.IsBeamOn) return;
            gun.IsBeamOn = false;
            sink.Emit(new GunBeamStopped { Key = gun.Key });
        }

        /// <summary>GML: stop_vulcan_sfx, plus played_spinup = false.</summary>
        void SpinDown(Gun gun)
        {
            gun.IsSpinning = false;
            if (!gun.IsLoopPlaying) return;
            gun.IsLoopPlaying = false;
            Cue(gun, VulcanSound.LoopStop);
            Cue(gun, VulcanSound.SpinDown);
        }

        void Cue(Gun gun, VulcanSound sound) => sink.Emit(new VulcanCue { Key = gun.Key, Sound = sound });
    }
}
