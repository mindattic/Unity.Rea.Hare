using Unity.Hare.Weapons;

namespace Unity.Hare.Player
{
    /// <summary>
    /// The only way a ship's state changes. A command carries the id of the player it acts for (no global current
    /// player) and runs on that player's ship in <see cref="PlayerWorld.Execute"/>, which returns whether it took
    /// effect and the events it raised. Views translate input into these and play the events.
    /// </summary>
    public abstract class PlayerCommand
    {
        protected PlayerCommand(PlayerId id) => Id = id;

        public PlayerId Id { get; }

        /// <summary>Does the work; false when the command was refused (the rules said no).</summary>
        internal abstract bool Apply(PlayerShip ship);
    }

    // ---- Flight, energy and the shield ----

    /// <summary>obj_player Create / reset_player's flight half: back in flight at full energy with no shield.</summary>
    public sealed class ResetFlight : PlayerCommand
    {
        readonly FlightTuning tuning;
        public ResetFlight(PlayerId id, FlightTuning tuning = null) : base(id) => this.tuning = tuning;
        internal override bool Apply(PlayerShip ship)
        {
            ship.Flight.Reset(tuning ?? ship.Flight.Tuning);
            ship.Look.Reset(ship.Look.Tuning); // Create's vulcan in hand; reset_player arms the smoke and spark trails
            return true;
        }
    }

    /// <summary>
    /// setup_level's ship (U.HARE-T57): the old obj_player destroyed, a new one created and reset_player run. The new
    /// instance's Create fills every ammo pool and holds the vulcan and the rocket again (its trails armed); reset_player
    /// gives it full HP, justspawned and the fly-in heading (<see cref="PlayerRespawned"/>). The energy bar is obj_energybar's,
    /// not the ship's, so it is left to refill. LIVES are the session's and stay.
    /// </summary>
    public sealed class SetUpShip : PlayerCommand
    {
        public SetUpShip(PlayerId id) : base(id) { }

        internal override bool Apply(PlayerShip ship)
        {
            ship.Ammo.Reset(ship.Ammo.Tuning);
            ship.Ground.Restart();
            ship.Air.Restart();
            ship.Look.Reset(ship.Look.Tuning);
            ship.Health.SetUp();
            return true;
        }
    }

    /// <summary>A/D: -1 clockwise .. +1 counterclockwise.</summary>
    public sealed class SetTurn : PlayerCommand
    {
        readonly float value;
        public SetTurn(PlayerId id, float value) : base(id) => this.value = value;
        internal override bool Apply(PlayerShip ship) { ship.Flight.SetTurnInput(value); return true; }
    }

    /// <summary>GML mouse_angle, degrees.</summary>
    public sealed class SetAim : PlayerCommand
    {
        readonly float degrees;
        public SetAim(PlayerId id, float degrees) : base(id) => this.degrees = degrees;
        internal override bool Apply(PlayerShip ship) { ship.Flight.SetAimDirection(degrees); return true; }
    }

    /// <summary>
    /// obj_surface_reflection's floor (U.HARE-T58): the ship reached room_sidescroll's water line and skims along it,
    /// direction and image_angle set to <c>direction</c> (0 or 180).
    /// </summary>
    public sealed class SkimSurface : PlayerCommand
    {
        readonly float direction;
        public SkimSurface(PlayerId id, float direction) : base(id) => this.direction = direction;
        internal override bool Apply(PlayerShip ship) { ship.Flight.SkimSurface(direction); return true; }
    }

    /// <summary>
    /// obj_level_results' Enter for the seat's ship (U.HARE-T22): <c>player.state = state_leave; player.invulnerable =
    /// true</c>. The ship leaves its perch (if any), flies at twice flight_speed and can no longer be hurt until setup_level
    /// replaces it. Refused while dead or already leaving.
    /// </summary>
    public sealed class LeaveLevel : PlayerCommand
    {
        public LeaveLevel(PlayerId id) : base(id) { }

        internal override bool Apply(PlayerShip ship)
        {
            if (!ship.Flight.Leave()) return false;
            ship.Health.MakeInvulnerableToLeave();
            return true;
        }
    }

    /// <summary>The leaving ship's exit_angle, degrees, as the room works it out each step. Refused unless leaving.</summary>
    public sealed class AimExit : PlayerCommand
    {
        readonly float degrees;
        public AimExit(PlayerId id, float degrees) : base(id) => this.degrees = degrees;
        internal override bool Apply(PlayerShip ship) => ship.Flight.AimExit(degrees);
    }

    /// <summary>GML player_boost.</summary>
    public sealed class Boost : PlayerCommand
    {
        public Boost(PlayerId id) : base(id) { }
        internal override bool Apply(PlayerShip ship) => ship.Flight.TryBoost();
    }

    /// <summary>GML player_brake.</summary>
    public sealed class Brake : PlayerCommand
    {
        public Brake(PlayerId id) : base(id) { }
        internal override bool Apply(PlayerShip ship) => ship.Flight.TryBrake();
    }

    /// <summary>GML player_leap toward <c>aim</c> degrees; runs set_ammo_cost when it launches.</summary>
    public sealed class Leap : PlayerCommand
    {
        readonly float aim;
        public Leap(PlayerId id, float aim) : base(id) => this.aim = aim;

        internal override bool Apply(PlayerShip ship)
        {
            if (!ship.Flight.TryLeap(aim)) return false;
            ship.SetAmmoCostFromLoadout();
            return true;
        }
    }

    /// <summary>The ship perches where it is, with no surface to ride (scripted landings, tests); runs set_ammo_cost.</summary>
    public sealed class Land : PlayerCommand
    {
        public Land(PlayerId id) : base(id) { }

        internal override bool Apply(PlayerShip ship)
        {
            if (!ship.Flight.Land()) return false;
            ship.SetAmmoCostFromLoadout();
            return true;
        }
    }

    /// <summary>
    /// GML obj_player's par_rock / par_structure collision: the flying ship perches on <c>surface</c> (state_idle, facing
    /// its normal, current_rock or current_structure, land_angle, distance_to_center), is no longer spawn-protected
    /// (justspawned = false) and runs set_ammo_cost.
    /// </summary>
    public sealed class LandOn : PlayerCommand
    {
        readonly LandingSurface surface;
        public LandOn(PlayerId id, LandingSurface surface) : base(id) => this.surface = surface;

        public LandingSurface Surface => surface;

        internal override bool Apply(PlayerShip ship)
        {
            if (!ship.Flight.LandOn(surface)) return false;
            ship.Health.EndSpawnProtection();
            ship.SetAmmoCostFromLoadout();
            return true;
        }
    }

    /// <summary>
    /// obj_player's Step while it has a current_rock: the rock turned <c>spinPerStep</c> degrees a step for <c>dt</c>
    /// seconds, and land_angle and image_angle turn with it.
    /// </summary>
    public sealed class RideRock : PlayerCommand
    {
        readonly float spinPerStep, speedPxPerStep, dt;

        public RideRock(PlayerId id, float spinPerStep, float speedPxPerStep, float dt) : base(id)
        {
            this.spinPerStep = spinPerStep;
            this.speedPxPerStep = speedPxPerStep;
            this.dt = dt;
        }

        internal override bool Apply(PlayerShip ship) => ship.Flight.RideRock(spinPerStep, speedPxPerStep, dt);
    }

    /// <summary>
    /// GML fling_player then <c>current_rock = noone</c>, as rock_destroyed and par_rock's leaving the room run it for the
    /// ship's rock: thrown off along its facing, spinning until the end_flash alarm (flash_time); runs set_ammo_cost.
    /// </summary>
    public sealed class Fling : PlayerCommand
    {
        public Fling(PlayerId id) : base(id) { }

        internal override bool Apply(PlayerShip ship)
        {
            if (!ship.Flight.Fling(GameClock.Seconds(ship.Health.Tuning.InvulnerableSteps))) return false;
            ship.SetAmmoCostFromLoadout();
            return true;
        }
    }

    /// <summary>The ship's rock was cleared away without rock_destroyed (setup_level): current_rock = noone.</summary>
    public sealed class LetGoOfRock : PlayerCommand
    {
        public LetGoOfRock(PlayerId id) : base(id) { }
        internal override bool Apply(PlayerShip ship) => ship.Flight.LetGoOfRock();
    }

    /// <summary>
    /// GML's Space and right-mouse press events (the keys that leap when they come up): perched, the ship crouches; in
    /// flight Space boosts and the right button brakes (boosts with Ctrl), as <c>inAir</c> says.
    /// </summary>
    public sealed class PressLeapKey : PlayerCommand
    {
        readonly AirPress inAir;
        public PressLeapKey(PlayerId id, AirPress inAir = AirPress.Boost) : base(id) => this.inAir = inAir;

        internal override bool Apply(PlayerShip ship)
        {
            switch (ship.Flight.State)
            {
                case PlayerFlightState.Idle: return ship.Flight.Crouch();
                case PlayerFlightState.Moving: return inAir == AirPress.Boost ? ship.Flight.TryBoost() : ship.Flight.TryBrake();
                default: return false;
            }
        }
    }

    /// <summary>GML Alt / middle mouse press: spin = 3 while flying.</summary>
    public sealed class PressSpinKey : PlayerCommand
    {
        public PressSpinKey(PlayerId id) : base(id) { }
        internal override bool Apply(PlayerShip ship) => ship.Flight.StartSpin();
    }

    /// <summary>GML Alt / middle mouse release: spin = 0 and was_spinning while flying, so the facing winds back.</summary>
    public sealed class ReleaseSpinKey : PlayerCommand
    {
        public ReleaseSpinKey(PlayerId id) : base(id) { }
        internal override bool Apply(PlayerShip ship) => ship.Flight.StopSpin();
    }

    /// <summary>GML player_shield.</summary>
    public sealed class RaiseShield : PlayerCommand
    {
        public RaiseShield(PlayerId id) : base(id) { }
        internal override bool Apply(PlayerShip ship) => ship.Flight.TryShield();
    }

    /// <summary>
    /// GML's boost-key press event (W, Space): boosts while flying; while perched (Idle) the same key raises the
    /// shield instead (player_shield). Accepted when either took effect.
    /// </summary>
    public sealed class PressBoostKey : PlayerCommand
    {
        public PressBoostKey(PlayerId id) : base(id) { }
        internal override bool Apply(PlayerShip ship) =>
            ship.Flight.State == PlayerFlightState.Moving ? ship.Flight.TryBoost() : ship.Flight.TryShield();
    }

    /// <summary>GML's brake-key press event (S, Shift): brakes while flying, raises the shield while perched.</summary>
    public sealed class PressBrakeKey : PlayerCommand
    {
        public PressBrakeKey(PlayerId id) : base(id) { }
        internal override bool Apply(PlayerShip ship) =>
            ship.Flight.State == PlayerFlightState.Moving ? ship.Flight.TryBrake() : ship.Flight.TryShield();
    }

    /// <summary>
    /// GML's boost/brake key-release events: while perched (Idle) letting go stands the ship up (crouching = false) and
    /// drops the shield so energy refills; in flight the release does nothing (boost and brake run until their energy does).
    /// </summary>
    public sealed class ReleaseShieldKey : PlayerCommand
    {
        public ReleaseShieldKey(PlayerId id) : base(id) { }

        internal override bool Apply(PlayerShip ship)
        {
            if (ship.Flight.State != PlayerFlightState.Idle) return false;
            ship.Flight.StartEnergyFill(); // KeyRelease 87/83 while state_idle: obj_energybar.mode = "fill"
            ship.Flight.StandUp();
            ship.Flight.DropShield();
            return true;
        }
    }

    /// <summary>The shield key was released: the shield drops and, while perched, the bar starts to fill.</summary>
    public sealed class DropShield : PlayerCommand
    {
        public DropShield(PlayerId id) : base(id) { }
        internal override bool Apply(PlayerShip ship)
        {
            if (ship.Flight.State == PlayerFlightState.Idle) ship.Flight.StartEnergyFill();
            ship.Flight.DropShield();
            return true;
        }
    }

    /// <summary>Stops the ship dead and drops every flight mode (player_hurt's HP-reached-0 flight half).</summary>
    public sealed class HaltShip : PlayerCommand
    {
        public HaltShip(PlayerId id) : base(id) { }
        internal override bool Apply(PlayerShip ship) { ship.Flight.Die(); return true; }
    }

    /// <summary>
    /// One physics step of the ship (energy, turning, speed changes), then its look's alarms (the smoke and spark trails
    /// and the justspawned jet, <see cref="ShipLook"/>).
    /// </summary>
    public sealed class StepFlight : PlayerCommand
    {
        readonly float dt;
        public StepFlight(PlayerId id, float dt) : base(id) => this.dt = dt;

        internal override bool Apply(PlayerShip ship)
        {
            ship.Flight.Step(dt);
            ship.Look.Step(dt);
            return true;
        }
    }

    // ---- Health and lives ----

    /// <summary>A fresh game: full HP and lives, spawn-protected.</summary>
    public sealed class ResetHealth : PlayerCommand
    {
        readonly HealthTuning tuning;
        public ResetHealth(PlayerId id, HealthTuning tuning = null) : base(id) => this.tuning = tuning;
        internal override bool Apply(PlayerShip ship) { ship.Health.Reset(tuning ?? ship.Health.Tuning); return true; }
    }

    /// <summary>
    /// Counts the ship's LIVES with <c>ledger</c> (the session's, so a death and the out-of-bounds rule spend the same
    /// global LIVES the HUD and game_restart read); null goes back to the ship's own count.
    /// </summary>
    public sealed class UseLives : PlayerCommand
    {
        readonly ILivesLedger ledger;
        public UseLives(PlayerId id, ILivesLedger ledger) : base(id) => this.ledger = ledger;
        internal override bool Apply(PlayerShip ship) { ship.Health.UseLives(ledger); return true; }
    }

    /// <summary>
    /// obj_player's Step past a non-looping edge's buffer zone (the arena's <c>PlayerOutOfBounds</c>): a life goes and
    /// reset_player runs, or the game restarts with none left.
    /// </summary>
    public sealed class LeaveArena : PlayerCommand
    {
        public LeaveArena(PlayerId id) : base(id) { }
        internal override bool Apply(PlayerShip ship) => ship.Health.LeaveArena();
    }

    /// <summary>
    /// A damage source reached the ship. Accepted when the hit landed. Before player_hurt, the hit collisions arm the
    /// end_flash alarm and react as <c>reaction</c> says (by default obj_rocket_explosion's none for a source that ignores
    /// spawn protection, else obj_bullet's and par_enemy's spin): a flying ship spins (spin = 3), and obj_rocket flings a
    /// ship perched on a rock.
    /// </summary>
    public sealed class TakeHit : PlayerCommand
    {
        readonly bool ignoresSpawnProtection;
        readonly HitReaction reaction;

        public TakeHit(PlayerId id, bool ignoresSpawnProtection = false, HitReaction? reaction = null) : base(id)
        {
            this.ignoresSpawnProtection = ignoresSpawnProtection;
            this.reaction = reaction ?? (ignoresSpawnProtection ? HitReaction.None : HitReaction.Spin);
        }

        internal override bool Apply(PlayerShip ship)
        {
            var flight = ship.Flight;
            bool flying = flight.State == PlayerFlightState.Moving;
            bool onRock = flight.State == PlayerFlightState.Idle && flight.CurrentRock.HasValue;
            if (!ship.Health.TryTakeHit(ignoresSpawnProtection)) return false;
            if (ship.Health.IsDead) return true; // player_hurt halted the ship; its spin no longer matters

            float endFlash = GameClock.Seconds(ship.Health.Tuning.InvulnerableSteps);
            flight.TakeHit(flying && reaction != HitReaction.None, endFlash);
            if (reaction == HitReaction.SpinOrFling && onRock && flight.Fling(endFlash)) ship.SetAmmoCostFromLoadout();
            return true;
        }
    }

    /// <summary>obj_carrot's collision: <c>HP += amount</c> while below MAXHP. Refused when the ship is already full.</summary>
    public sealed class RestoreHealth : PlayerCommand
    {
        readonly int amount;
        public RestoreHealth(PlayerId id, int amount = 1) : base(id) => this.amount = amount;
        internal override bool Apply(PlayerShip ship) => ship.Health.Heal(amount);
    }

    /// <summary>Advances the health alarms (invulnerability, spawn protection, hurt pose, death wait).</summary>
    public sealed class TickHealth : PlayerCommand
    {
        readonly float dt;
        public TickHealth(PlayerId id, float dt) : base(id) => this.dt = dt;
        internal override bool Apply(PlayerShip ship) { ship.Health.Tick(dt); return true; }
    }

    // ---- Ammo ----

    /// <summary>obj_player Create: every pool full, both regen alarms stopped.</summary>
    public sealed class ResetAmmo : PlayerCommand
    {
        readonly AmmoTuning tuning;
        public ResetAmmo(PlayerId id, AmmoTuning tuning = null) : base(id) => this.tuning = tuning;

        internal override bool Apply(PlayerShip ship)
        {
            ship.Ammo.Reset(tuning ?? ship.Ammo.Tuning);
            return true;
        }
    }

    /// <summary>Runs the two ammo regen alarms.</summary>
    public sealed class TickAmmo : PlayerCommand
    {
        readonly float dt;
        public TickAmmo(PlayerId id, float dt) : base(id) => this.dt = dt;
        internal override bool Apply(PlayerShip ship) { ship.Ammo.Tick(dt); return true; }
    }

    /// <summary>A trigger went down (stops the primary regen).</summary>
    public sealed class PressAmmoTrigger : PlayerCommand
    {
        readonly int holder;
        public PressAmmoTrigger(PlayerId id, int holder = AmmoPools.DefaultHolder) : base(id) => this.holder = holder;
        internal override bool Apply(PlayerShip ship) { ship.Ammo.PressTrigger(holder); return true; }
    }

    /// <summary>A trigger came up (restarts both regen alarms once every holder has let go).</summary>
    public sealed class ReleaseAmmoTrigger : PlayerCommand
    {
        readonly int holder;
        public ReleaseAmmoTrigger(PlayerId id, int holder = AmmoPools.DefaultHolder) : base(id) => this.holder = holder;
        internal override bool Apply(PlayerShip ship) { ship.Ammo.ReleaseTrigger(holder); return true; }
    }

    /// <summary>Tops a pool up to its maximum (pickups, upgrades).</summary>
    public sealed class RefillAmmo : PlayerCommand
    {
        readonly AmmoPool pool;
        public RefillAmmo(PlayerId id, AmmoPool pool) : base(id) => this.pool = pool;
        internal override bool Apply(PlayerShip ship) { ship.Ammo.Refill(pool); return true; }
    }

    /// <summary>Sets a pool's count directly (pickups, upgrades).</summary>
    public sealed class SetAmmo : PlayerCommand
    {
        readonly AmmoPool pool;
        readonly int value;
        public SetAmmo(PlayerId id, AmmoPool pool, int value) : base(id) { this.pool = pool; this.value = value; }
        internal override bool Apply(PlayerShip ship) { ship.Ammo.SetCurrent(pool, value); return true; }
    }

    /// <summary>GML set_ammo_cost with the weapons it is told about (see <see cref="AmmoPools.SetAmmoCost"/>).</summary>
    public sealed class SetAmmoCost : PlayerCommand
    {
        readonly GroundWeapon? groundWeapon;
        readonly GroundWeapon? groundWeaponSecondary;
        readonly AirWeapon? airWeapon;
        readonly bool airSecondaryIsMine;

        public SetAmmoCost(PlayerId id, GroundWeapon? groundWeapon, GroundWeapon? groundWeaponSecondary, AirWeapon? airWeapon, bool airSecondaryIsMine) : base(id)
        {
            this.groundWeapon = groundWeapon;
            this.groundWeaponSecondary = groundWeaponSecondary;
            this.airWeapon = airWeapon;
            this.airSecondaryIsMine = airSecondaryIsMine;
        }

        internal override bool Apply(PlayerShip ship)
        {
            ship.Ammo.SetAmmoCost(groundWeapon, groundWeaponSecondary, airWeapon, airSecondaryIsMine);
            return true;
        }
    }

    // ---- Guns (vulcan, shotgun, flamer, mine launcher) ----

    /// <summary>Puts a gun on the ship under <c>key</c>, or moves the one under that key to another slot.</summary>
    public sealed class InstallGun : PlayerCommand
    {
        readonly int key;
        readonly GunType type;
        readonly WeaponSlot slot;

        public InstallGun(PlayerId id, int key, GunType type, WeaponSlot slot) : base(id)
        {
            this.key = key;
            this.type = type;
            this.slot = slot;
        }

        internal override bool Apply(PlayerShip ship) { ship.Guns.Install(key, type, slot); return true; }
    }

    /// <summary>Takes a gun off the ship.</summary>
    public sealed class RemoveGun : PlayerCommand
    {
        readonly int key;
        public RemoveGun(PlayerId id, int key) : base(id) => this.key = key;
        internal override bool Apply(PlayerShip ship) { ship.Guns.Remove(key); return true; }
    }

    /// <summary>The gun's trigger went down.</summary>
    public sealed class PullTrigger : PlayerCommand
    {
        readonly int key;
        public PullTrigger(PlayerId id, int key) : base(id) => this.key = key;

        internal override bool Apply(PlayerShip ship)
        {
            var gun = ship.Guns.Find(key);
            if (gun == null) return false;
            ship.Guns.PullTrigger(gun);
            return true;
        }
    }

    /// <summary>The gun's trigger came up (the vulcan spins down).</summary>
    public sealed class ReleaseTrigger : PlayerCommand
    {
        readonly int key;
        public ReleaseTrigger(PlayerId id, int key) : base(id) => this.key = key;

        internal override bool Apply(PlayerShip ship)
        {
            var gun = ship.Guns.Find(key);
            if (gun == null) return false;
            ship.Guns.ReleaseTrigger(gun);
            return true;
        }
    }

    /// <summary>One trigger pull on a gun. Accepted when a shot went out (a <see cref="GunFired"/> event).</summary>
    public sealed class FireGun : PlayerCommand
    {
        readonly int key;
        public FireGun(PlayerId id, int key) : base(id) => this.key = key;

        internal override bool Apply(PlayerShip ship)
        {
            var gun = ship.Guns.Find(key);
            return gun != null && ship.Guns.TryFire(gun);
        }
    }

    /// <summary>Advances a gun's re-arm cooldown (the vulcan also fires while its trigger is held).</summary>
    public sealed class TickGun : PlayerCommand
    {
        readonly int key;
        readonly float dt;
        public TickGun(PlayerId id, int key, float dt) : base(id) { this.key = key; this.dt = dt; }

        internal override bool Apply(PlayerShip ship)
        {
            var gun = ship.Guns.Find(key);
            if (gun == null) return false;
            ship.Guns.Tick(gun, dt);
            return true;
        }
    }

    /// <summary>GML can_shoot = true for a gun.</summary>
    public sealed class RearmGun : PlayerCommand
    {
        readonly int key;
        public RearmGun(PlayerId id, int key) : base(id) => this.key = key;

        internal override bool Apply(PlayerShip ship)
        {
            var gun = ship.Guns.Find(key);
            if (gun == null) return false;
            ship.Guns.Rearm(gun);
            return true;
        }
    }

    /// <summary>A gun is put away: its trigger lets go so ammo regen resumes.</summary>
    public sealed class HolsterGun : PlayerCommand
    {
        readonly int key;
        public HolsterGun(PlayerId id, int key) : base(id) => this.key = key;

        internal override bool Apply(PlayerShip ship)
        {
            var gun = ship.Guns.Find(key);
            if (gun == null) return false;
            ship.Guns.Holster(gun);
            return true;
        }
    }

    // ---- Ground loadout (next_ground_weapon) ----

    /// <summary>Sets the ground loadout up: nothing equipped, then the first gun in cycle order.</summary>
    public sealed class ResetGroundLoadout : PlayerCommand
    {
        public ResetGroundLoadout(PlayerId id) : base(id) { }
        internal override bool Apply(PlayerShip ship) { ship.Ground.Reset(); return true; }
    }

    /// <summary>Re-reads the ground primaries after one was added or removed.</summary>
    public sealed class RefreshGroundLoadout : PlayerCommand
    {
        public RefreshGroundLoadout(PlayerId id) : base(id) { }
        internal override bool Apply(PlayerShip ship) { ship.Ground.Refresh(); return true; }
    }

    /// <summary>GML next_ground_weapon (Q).</summary>
    public sealed class NextGroundWeapon : PlayerCommand
    {
        public NextGroundWeapon(PlayerId id) : base(id) { }
        internal override bool Apply(PlayerShip ship) => ship.Ground.Next();
    }

    /// <summary>Equips a ground primary directly (upgrade screen).</summary>
    public sealed class EquipGroundWeapon : PlayerCommand
    {
        readonly GroundWeapon kind;
        public EquipGroundWeapon(PlayerId id, GroundWeapon kind) : base(id) => this.kind = kind;
        internal override bool Apply(PlayerShip ship) => ship.Ground.EquipKind(kind);
    }

    // ---- Air loadout (next_air_weapon, fire_secondary in the air) ----

    /// <summary>Sets the air loadout up: first unlocked weapon equipped, re-armed, trigger up.</summary>
    public sealed class ResetAirLoadout : PlayerCommand
    {
        public ResetAirLoadout(PlayerId id) : base(id) { }
        internal override bool Apply(PlayerShip ship) { ship.Air.Reset(); return true; }
    }

    /// <summary>GML next_air_weapon (E).</summary>
    public sealed class NextAirWeapon : PlayerCommand
    {
        public NextAirWeapon(PlayerId id) : base(id) { }
        internal override bool Apply(PlayerShip ship) => ship.Air.Next();
    }

    /// <summary>Adds an air weapon to the cycle.</summary>
    public sealed class UnlockAirWeapon : PlayerCommand
    {
        readonly AirWeapon weapon;
        public UnlockAirWeapon(PlayerId id, AirWeapon weapon) : base(id) => this.weapon = weapon;
        internal override bool Apply(PlayerShip ship) { ship.Air.Unlock(weapon); return true; }
    }

    /// <summary>Removes an air weapon from the cycle.</summary>
    public sealed class LockAirWeapon : PlayerCommand
    {
        readonly AirWeapon weapon;
        public LockAirWeapon(PlayerId id, AirWeapon weapon) : base(id) => this.weapon = weapon;
        internal override bool Apply(PlayerShip ship) { ship.Air.Lock(weapon); return true; }
    }

    /// <summary>The secondary (right mouse) trigger went down or up.</summary>
    public sealed class SetAirTrigger : PlayerCommand
    {
        readonly bool held;
        public SetAirTrigger(PlayerId id, bool held) : base(id) => this.held = held;
        internal override bool Apply(PlayerShip ship) { ship.Air.SetTrigger(held); return true; }
    }

    /// <summary>Advances the air loadout's re-arm timer and fires while the trigger is held.</summary>
    public sealed class TickAir : PlayerCommand
    {
        readonly float dt;
        public TickAir(PlayerId id, float dt) : base(id) => this.dt = dt;
        internal override bool Apply(PlayerShip ship) { ship.Air.Tick(dt); return true; }
    }

    /// <summary>obj_player KeyPress 66 (B): the vulcan's rounds take the next look (<see cref="ShipLook.BulletSprite"/>).</summary>
    public sealed class CycleBulletSprite : PlayerCommand
    {
        public CycleBulletSprite(PlayerId id) : base(id) { }
        internal override bool Apply(PlayerShip ship) => ship.Look.CycleBulletSprite();
    }

    /// <summary>GML fire_secondary for the current air weapon. Accepted when a shot went out.</summary>
    public sealed class FireAir : PlayerCommand
    {
        public FireAir(PlayerId id) : base(id) { }
        internal override bool Apply(PlayerShip ship) => ship.Air.TryFire();
    }
}
