using System;
using System.Collections.Generic;
using Unity.Hare.Player;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// obj_player's four ammo pools. "Ground" and "air" are the ship's state, not the target:
    /// fire_primary/fire_secondary spend the ground pools while perched and the air pools in flight.
    /// </summary>
    public enum AmmoPool
    {
        /// <summary>ground_ammo_primary: the perched left-mouse weapon (vulcan, laser, railgun, shotgun, flamer).</summary>
        GroundPrimary,
        /// <summary>ground_ammo_secondary: the perched Ctrl + left-mouse weapon (default the shotgun).</summary>
        GroundSecondary,
        /// <summary>air_ammo_primary: the in-flight left-mouse weapon (rockets).</summary>
        AirPrimary,
        /// <summary>air_ammo_secondary: the in-flight Ctrl + left-mouse weapon (mines).</summary>
        AirSecondary
    }

    /// <summary>One pool's starting numbers: x_ammo_y_max, x_ammo_y_cost, x_ammo_regen_y.</summary>
    [Serializable]
    public class PoolTuning
    {
        public int Max;
        public int Cost;
        public int Regen;

        public PoolTuning() { }

        public PoolTuning(int max, int cost, int regen)
        {
            Max = max;
            Cost = cost;
            Regen = regen;
        }
    }

    /// <summary>obj_player's Create values for the ammo economy (ammo_regen_delay = room_speed).</summary>
    [Serializable]
    public class AmmoTuning
    {
        public PoolTuning GroundPrimary = new PoolTuning(150, 1, 10);
        public PoolTuning GroundSecondary = new PoolTuning(12, 4, 2);
        public PoolTuning AirPrimary = new PoolTuning(20, 1, 4);
        public PoolTuning AirSecondary = new PoolTuning(8, 1, 1);
        public float RegenDelaySteps = GameClock.StepsPerSecond;
        public float RegenDelaySecondarySteps = GameClock.StepsPerSecond;

        internal PoolTuning For(AmmoPool pool)
        {
            switch (pool)
            {
                case AmmoPool.GroundPrimary: return GroundPrimary;
                case AmmoPool.GroundSecondary: return GroundSecondary;
                case AmmoPool.AirPrimary: return AirPrimary;
                default: return AirSecondary;
            }
        }
    }

    /// <summary>
    /// gmlHARE's shared ammo economy: the ammo variables of obj_player's Create event, the set_ammo_cost and
    /// subtract_ground_ammo scripts, fire_primary/fire_secondary's "enough ammo, then subtract and clamp at 0"
    /// check, the two regen alarms (9: both primaries, with the reload sounds; 10: both secondaries) and the left
    /// mouse's press/release handling of them. Every weapon on the ship draws on these pools.
    ///
    /// Faithful quirks, kept on purpose:
    /// - set_ammo_cost runs when the ship lands or leaps, not when a weapon is switched, so a weapon equipped with Q
    ///   while perched pays the cost set at landing.
    /// - set_ammo_cost lowers the maxima (vulcan primary 120) without touching the current count, so the ground
    ///   primary keeps Create's 150 until a regen tick finds it at or over the maximum and snaps it down.
    /// - Both regen alarms are idle until the trigger is first released (Create never sets them). Pressing stops
    ///   only the primary alarm; releasing restarts both with the full delay.
    /// - Air primary regen counts as full only when it overshoots the maximum (GML compares with &gt; there and
    ///   &gt;= for the ground primary).
    /// </summary>
    public sealed class AmmoPools
    {
        /// <summary>The holder id a trigger takes when it has no weapon of its own.</summary>
        public const int DefaultHolder = 0;

        sealed class Pool
        {
            public int Current;
            public int Max;
            public int Cost;
            public int Regen;
        }

        readonly EventSink sink;
        readonly Pool[] pools = { new Pool(), new Pool(), new Pool(), new Pool() };
        readonly HashSet<int> triggerHolders = new HashSet<int>();
        AmmoTuning tuning = new AmmoTuning();

        internal AmmoPools(EventSink sink)
        {
            this.sink = sink;
            Reset(tuning);
        }

        public AmmoTuning Tuning => tuning;

        /// <summary>GML alarm[alarm_ammo_regen]: seconds left, or -1 while stopped.</summary>
        public float PrimaryRegenTimer { get; private set; } = -1f;
        /// <summary>GML alarm[alarm_ammo_regen_secondary]: seconds left, or -1 while stopped.</summary>
        public float SecondaryRegenTimer { get; private set; } = -1f;

        public bool TriggerHeld => triggerHolders.Count > 0;

        public int Current(AmmoPool pool) => pools[(int)pool].Current;
        public int Max(AmmoPool pool) => pools[(int)pool].Max;
        public int Cost(AmmoPool pool) => pools[(int)pool].Cost;
        public int Regen(AmmoPool pool) => pools[(int)pool].Regen;

        /// <summary>The pool an air weapon spends: laser and railgun are GML ground weapons, rockets spend air_ammo_primary.</summary>
        public static AmmoPool PoolFor(AirWeapon weapon) => weapon == AirWeapon.Rocket ? AmmoPool.AirPrimary : AmmoPool.GroundPrimary;

        /// <summary>obj_player Create: every pool full at its starting values, both regen alarms stopped.</summary>
        public void Reset(AmmoTuning newTuning)
        {
            tuning = newTuning ?? new AmmoTuning();
            foreach (AmmoPool pool in Enum.GetValues(typeof(AmmoPool)))
            {
                var start = tuning.For(pool);
                var p = pools[(int)pool];
                p.Max = start.Max;
                p.Cost = start.Cost;
                p.Regen = start.Regen;
                p.Current = start.Max;
            }
            triggerHolders.Clear();
            PrimaryRegenTimer = -1f;
            SecondaryRegenTimer = -1f;
        }

        /// <summary>GML fire_primary/fire_secondary: x_ammo_y &gt;= x_ammo_y_cost.</summary>
        public bool HasAmmo(AmmoPool pool) => pools[(int)pool].Current >= pools[(int)pool].Cost;

        /// <summary>GML subtract_ground_ammo (and fire_*'s inline copies for the other pools): current -= cost, clamped at 0.</summary>
        public void Subtract(AmmoPool pool)
        {
            var p = pools[(int)pool];
            p.Current = Math.Max(0, p.Current - p.Cost);
        }

        /// <summary>The check and the subtraction together. False (nothing spent) when the pool is short.</summary>
        public bool TrySpend(AmmoPool pool)
        {
            if (!HasAmmo(pool)) return false;
            Subtract(pool);
            return true;
        }

        /// <summary>Tops a pool up to its maximum (pickups, upgrades).</summary>
        public void Refill(AmmoPool pool) => pools[(int)pool].Current = pools[(int)pool].Max;

        /// <summary>Sets a pool's count directly, clamped at 0 (pickups, upgrades).</summary>
        public void SetCurrent(AmmoPool pool, int value) => pools[(int)pool].Current = Math.Max(0, value);

        /// <summary>
        /// GML set_ammo_cost, verbatim: every cost is 1 except the shotgun as ground secondary (4); the vulcan as
        /// ground primary sets its maximum to 120, the shotgun as ground secondary to 12, rockets as air primary
        /// to 20. Weapons not listed leave the maximum as it was. Its one slip is kept too: the rocket branch sets
        /// air_ammo_secondary_cost, not the primary's. Null means the slot holds nothing the script recognises.
        /// </summary>
        public void SetAmmoCost(GroundWeapon? groundWeapon, GroundWeapon? groundWeaponSecondary, AirWeapon? airWeapon, bool airSecondaryIsMine)
        {
            var groundPrimary = pools[(int)AmmoPool.GroundPrimary];
            var groundSecondary = pools[(int)AmmoPool.GroundSecondary];
            var airPrimary = pools[(int)AmmoPool.AirPrimary];
            var airSecondary = pools[(int)AmmoPool.AirSecondary];

            if (groundWeapon == GroundWeapon.Vulcan) groundPrimary.Max = 120;
            if (groundWeapon != null) groundPrimary.Cost = 1;

            if (groundWeaponSecondary == GroundWeapon.Shotgun)
            {
                groundSecondary.Max = 12;
                groundSecondary.Cost = 4;
            }
            else if (groundWeaponSecondary != null)
            {
                groundSecondary.Cost = 1;
            }

            if (airWeapon == AirWeapon.Rocket)
            {
                airPrimary.Max = 20;
                airSecondary.Cost = 1; // sic: GML writes air_ammo_secondary_cost here
            }

            if (airSecondaryIsMine) airSecondary.Cost = 1;
        }

        /// <summary>
        /// GML global left pressed (mouse1_down = true): alarm[alarm_ammo_regen] = 0 stops the primary regen. The
        /// secondary alarm keeps running. <paramref name="holder"/> is whichever weapon reads the trigger; regen
        /// restarts only once every holder has let go.
        /// </summary>
        public void PressTrigger(int holder = DefaultHolder)
        {
            triggerHolders.Add(holder);
            PrimaryRegenTimer = -1f;
        }

        /// <summary>GML global left released: both regen alarms restart with their full delay.</summary>
        public void ReleaseTrigger(int holder = DefaultHolder)
        {
            if (!triggerHolders.Remove(holder)) return;
            if (triggerHolders.Count > 0) return;
            PrimaryRegenTimer = GameClock.Seconds(tuning.RegenDelaySteps);
            SecondaryRegenTimer = GameClock.Seconds(tuning.RegenDelaySecondarySteps);
        }

        /// <summary>Runs the regen alarms.</summary>
        public void Tick(float dt)
        {
            // Each alarm re-arms itself when it fires; a long dt catches up every tick it spans.
            // The 1e-5 slack keeps whole GML steps from leaving float dust (120 steps = 1 tick, not 0).
            if (PrimaryRegenTimer >= 0f)
            {
                PrimaryRegenTimer -= dt;
                while (PrimaryRegenTimer <= 1e-5f)
                {
                    RegeneratePrimary();
                    PrimaryRegenTimer += GameClock.Seconds(tuning.RegenDelaySteps);
                }
            }

            if (SecondaryRegenTimer >= 0f)
            {
                SecondaryRegenTimer -= dt;
                while (SecondaryRegenTimer <= 1e-5f)
                {
                    RegenerateSecondary();
                    SecondaryRegenTimer += GameClock.Seconds(tuning.RegenDelaySecondarySteps);
                }
            }
        }

        /// <summary>GML alarm 9: regenerate ground primary and air primary ammunition.</summary>
        void RegeneratePrimary()
        {
            var groundPrimary = pools[(int)AmmoPool.GroundPrimary];
            if (groundPrimary.Current < groundPrimary.Max)
            {
                groundPrimary.Current += groundPrimary.Regen;
                if (groundPrimary.Current >= groundPrimary.Max)
                {
                    groundPrimary.Current = groundPrimary.Max;
                    Reloaded(AmmoPool.GroundPrimary, true);
                }
                else
                {
                    Reloaded(AmmoPool.GroundPrimary, false);
                }
            }
            else
            {
                groundPrimary.Current = groundPrimary.Max;
            }

            var airPrimary = pools[(int)AmmoPool.AirPrimary];
            if (airPrimary.Current < airPrimary.Max)
            {
                airPrimary.Current += airPrimary.Regen;
                if (airPrimary.Current > airPrimary.Max) // sic: > here, >= above
                {
                    airPrimary.Current = airPrimary.Max;
                    Reloaded(AmmoPool.AirPrimary, true);
                }
                else
                {
                    Reloaded(AmmoPool.AirPrimary, false);
                }
            }
            else
            {
                airPrimary.Current = airPrimary.Max;
            }
        }

        /// <summary>GML alarm 10: regenerate both secondaries, silently.</summary>
        void RegenerateSecondary()
        {
            foreach (var pool in new[] { pools[(int)AmmoPool.GroundSecondary], pools[(int)AmmoPool.AirSecondary] })
            {
                if (pool.Current < pool.Max) pool.Current = Math.Min(pool.Max, pool.Current + pool.Regen);
                else pool.Current = pool.Max;
            }
        }

        void Reloaded(AmmoPool pool, bool full) => sink.Emit(new AmmoReloaded { Pool = pool, Full = full });
    }
}
