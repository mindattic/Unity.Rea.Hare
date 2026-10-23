using System;
using System.Collections.Generic;
using UnityEngine;
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

    /// <summary>
    /// Ports gmlHARE's shared ammo economy: the ammo variables of obj_player's Create event, the
    /// set_ammo_cost and subtract_ground_ammo scripts, fire_primary/fire_secondary's "enough ammo,
    /// then subtract and clamp at 0" check, the two regen alarms (9: both primaries, with the
    /// reload sounds; 10: both secondaries) and the left mouse's press/release handling of them.
    /// Every weapon on the ship draws on these pools instead of keeping its own clip.
    ///
    /// Faithful quirks, kept on purpose:
    /// - set_ammo_cost runs when the ship lands or leaps, not when a weapon is switched, so a
    ///   weapon equipped with Q while perched pays the cost set at landing.
    /// - set_ammo_cost lowers the maxima (vulcan primary 120) without touching the current count,
    ///   so the ground primary keeps Create's 150 until a regen tick finds it at or over the
    ///   maximum and snaps it down.
    /// - Both regen alarms are idle until the trigger is first released (Create never sets them).
    ///   Pressing stops only the primary alarm; releasing restarts both with the full delay.
    /// - Air primary regen plays sfx_reload_full only when it overshoots the maximum (GML compares
    ///   with &gt; there and &gt;= for the ground primary).
    /// </summary>
    public class PlayerAmmo : MonoBehaviour
    {
        /// <summary>One obj_player pool: x_ammo_y, x_ammo_y_max, x_ammo_y_cost, x_ammo_regen_y.</summary>
        [Serializable]
        public class Pool
        {
            [NonSerialized] public int current;
            public int max;
            public int cost;
            public int regen;

            public Pool(int max, int cost, int regen)
            {
                current = max;
                this.max = max;
                this.cost = cost;
                this.regen = regen;
            }

            public Pool Clone() => new Pool(max, cost, regen) { current = current };
        }

        [Header("GML obj_player Create")]
        [SerializeField] Pool groundPrimary = new Pool(150, 1, 10);
        [SerializeField] Pool groundSecondary = new Pool(12, 4, 2);
        [SerializeField] Pool airPrimary = new Pool(20, 1, 4);
        [SerializeField] Pool airSecondary = new Pool(8, 1, 1);
        [SerializeField] float regenDelaySteps = Gml.StepsPerSecond; // ammo_regen_delay = room_speed
        [SerializeField] float regenDelaySecondarySteps = Gml.StepsPerSecond;

        [Header("Sound (sfx_reload_half / sfx_reload_full)")]
        [SerializeField] AudioClip reloadHalfClip;
        [SerializeField] AudioClip reloadFullClip;

        Pool[] pools;
        Pool[] defaults;
        readonly HashSet<object> triggerHolders = new HashSet<object>();
        PlayerShipController boundShip;
        AudioSource audioSource;

        /// <summary>GML alarm[alarm_ammo_regen]: seconds left, or -1 while stopped.</summary>
        public float PrimaryRegenTimer { get; private set; } = -1f;
        /// <summary>GML alarm[alarm_ammo_regen_secondary]: seconds left, or -1 while stopped.</summary>
        public float SecondaryRegenTimer { get; private set; } = -1f;

        /// <summary>GML alarm 9's reload sounds: the pool that refilled and whether it reached full.</summary>
        public event Action<AmmoPool, bool> Reloaded;

        /// <summary>The ship's ammo, adding and binding the component on first use (scenes, tests).</summary>
        public static PlayerAmmo Of(GameObject ship)
        {
            var ammo = ship.GetComponent<PlayerAmmo>();
            if (ammo == null) ammo = ship.AddComponent<PlayerAmmo>(); // Awake runs here in Play Mode
            ammo.Bind();
            return ammo;
        }

        void Awake() => ResetState();

        void OnDestroy() => Unbind();

        /// <summary>GML obj_player Create: every pool full at its starting values, both regen alarms stopped.</summary>
        public void ResetState()
        {
            if (defaults == null)
                defaults = new[] { groundPrimary.Clone(), groundSecondary.Clone(), airPrimary.Clone(), airSecondary.Clone() };
            groundPrimary = defaults[0].Clone();
            groundSecondary = defaults[1].Clone();
            airPrimary = defaults[2].Clone();
            airSecondary = defaults[3].Clone();
            foreach (var pool in new[] { groundPrimary, groundSecondary, airPrimary, airSecondary }) pool.current = pool.max;
            pools = null;
            triggerHolders.Clear();
            PrimaryRegenTimer = -1f;
            SecondaryRegenTimer = -1f;
            Bind();
        }

        /// <summary>Hooks set_ammo_cost to the ship's landings and leaps (idempotent).</summary>
        public void Bind()
        {
            var ship = GetComponent<PlayerShipController>();
            if (ship == boundShip) return;
            Unbind();
            boundShip = ship;
            if (boundShip == null) return;
            boundShip.Landed += SetAmmoCost;
            boundShip.Leapt += SetAmmoCost;
        }

        void Unbind()
        {
            if (boundShip == null) return;
            boundShip.Landed -= SetAmmoCost;
            boundShip.Leapt -= SetAmmoCost;
            boundShip = null;
        }

        Pool Get(AmmoPool pool)
        {
            pools ??= new[] { groundPrimary, groundSecondary, airPrimary, airSecondary };
            return pools[(int)pool];
        }

        public int Current(AmmoPool pool) => Get(pool).current;
        public int Max(AmmoPool pool) => Get(pool).max;
        public int Cost(AmmoPool pool) => Get(pool).cost;
        public int Regen(AmmoPool pool) => Get(pool).regen;

        /// <summary>GML fire_primary/fire_secondary: x_ammo_y &gt;= x_ammo_y_cost.</summary>
        public bool HasAmmo(AmmoPool pool) => Get(pool).current >= Get(pool).cost;

        /// <summary>
        /// GML subtract_ground_ammo (and fire_*'s inline copies for the other pools):
        /// current -= cost, clamped at 0.
        /// </summary>
        public void Subtract(AmmoPool pool)
        {
            var p = Get(pool);
            p.current = Mathf.Max(0, p.current - p.cost);
        }

        /// <summary>The check and the subtraction together. False (nothing spent) when the pool is short.</summary>
        public bool TrySpend(AmmoPool pool)
        {
            if (!HasAmmo(pool)) return false;
            Subtract(pool);
            return true;
        }

        /// <summary>Tops a pool up to its maximum (pickups, upgrades, tests).</summary>
        public void Refill(AmmoPool pool) => Get(pool).current = Get(pool).max;

        /// <summary>Sets a pool's count directly, clamped at 0 (pickups, upgrades, tests).</summary>
        public void SetCurrent(AmmoPool pool, int value) => Get(pool).current = Mathf.Max(0, value);

        /// <summary>
        /// GML set_ammo_cost, verbatim: every cost is 1 except the shotgun as ground secondary (4);
        /// the vulcan as ground primary sets its maximum to 120, the shotgun as ground secondary to
        /// 12, rockets as air primary to 20. Weapons not listed leave the maximum as it was. Its one
        /// slip is kept too: the rocket branch sets air_ammo_secondary_cost, not the primary's.
        /// Null means the slot holds nothing the script recognises (its branches all miss).
        /// </summary>
        public void SetAmmoCost(GroundWeapon? groundWeapon, GroundWeapon? groundWeaponSecondary, AirWeapon? airWeapon, bool airSecondaryIsMine)
        {
            if (groundWeapon == GroundWeapon.Vulcan) groundPrimary.max = 120;
            if (groundWeapon != null) groundPrimary.cost = 1;

            if (groundWeaponSecondary == GroundWeapon.Shotgun)
            {
                groundSecondary.max = 12;
                groundSecondary.cost = 4;
            }
            else if (groundWeaponSecondary != null)
            {
                groundSecondary.cost = 1;
            }

            if (airWeapon == AirWeapon.Rocket)
            {
                airPrimary.max = 20;
                airSecondary.cost = 1; // sic: GML writes air_ammo_secondary_cost here
            }

            if (airSecondaryIsMine) airSecondary.cost = 1;
        }

        /// <summary>
        /// GML set_ammo_cost as player_leap, fling_player and the landing collisions call it: reads
        /// the ship's equipped weapons (player.ground_weapon, ground_weapon_secondary, air_weapon,
        /// air_weapon_secondary) and applies <see cref="SetAmmoCost(GroundWeapon?, GroundWeapon?, AirWeapon?, bool)"/>.
        /// </summary>
        public void SetAmmoCost()
        {
            var loadout = GetComponent<GroundWeaponLoadout>();
            GroundWeapon? primary = loadout != null ? loadout.Current : null;
            GroundWeapon? secondary = null;

            foreach (var weapon in GetComponents<IGroundWeapon>())
            {
                if (weapon.IsGroundPrimary) { if (loadout == null) primary ??= weapon.GroundKind; }
                else secondary ??= weapon.GroundKind;
            }

            var air = GetComponent<AirWeaponLoadout>();
            AirWeapon? airWeapon = air != null && air.IsUnlocked(air.Current) ? air.Current : (AirWeapon?)null;

            SetAmmoCost(primary, secondary, airWeapon, GetComponent<MineLauncher>() != null);
        }

        /// <summary>
        /// GML global left pressed (mouse1_down = true): alarm[alarm_ammo_regen] = 0 stops the
        /// primary regen. The secondary alarm keeps running. <paramref name="holder"/> is whichever
        /// weapon reads the trigger; regen restarts only once every holder has let go.
        /// </summary>
        public void PressTrigger(object holder = null)
        {
            triggerHolders.Add(holder ?? this);
            PrimaryRegenTimer = -1f;
        }

        /// <summary>GML global left released: both regen alarms restart with their full delay.</summary>
        public void ReleaseTrigger(object holder = null)
        {
            if (!triggerHolders.Remove(holder ?? this)) return;
            if (triggerHolders.Count > 0) return;
            PrimaryRegenTimer = Gml.Steps(regenDelaySteps);
            SecondaryRegenTimer = Gml.Steps(regenDelaySecondarySteps);
        }

        public bool TriggerHeld => triggerHolders.Count > 0;

        void Update() => Tick(Time.deltaTime);

        /// <summary>Runs the regen alarms; public so EditMode tests can drive time.</summary>
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
                    PrimaryRegenTimer += Gml.Steps(regenDelaySteps);
                }
            }

            if (SecondaryRegenTimer >= 0f)
            {
                SecondaryRegenTimer -= dt;
                while (SecondaryRegenTimer <= 1e-5f)
                {
                    RegenerateSecondary();
                    SecondaryRegenTimer += Gml.Steps(regenDelaySecondarySteps);
                }
            }
        }

        /// <summary>GML alarm 9: regenerate ground primary and air primary ammunition.</summary>
        void RegeneratePrimary()
        {
            if (groundPrimary.current < groundPrimary.max)
            {
                groundPrimary.current += groundPrimary.regen;
                if (groundPrimary.current >= groundPrimary.max)
                {
                    groundPrimary.current = groundPrimary.max;
                    PlayReload(AmmoPool.GroundPrimary, true);
                }
                else
                {
                    PlayReload(AmmoPool.GroundPrimary, false);
                }
            }
            else
            {
                groundPrimary.current = groundPrimary.max;
            }

            if (airPrimary.current < airPrimary.max)
            {
                airPrimary.current += airPrimary.regen;
                if (airPrimary.current > airPrimary.max) // sic: > here, >= above
                {
                    airPrimary.current = airPrimary.max;
                    PlayReload(AmmoPool.AirPrimary, true);
                }
                else
                {
                    PlayReload(AmmoPool.AirPrimary, false);
                }
            }
            else
            {
                airPrimary.current = airPrimary.max;
            }
        }

        /// <summary>GML alarm 10: regenerate both secondaries, silently.</summary>
        void RegenerateSecondary()
        {
            foreach (var pool in new[] { groundSecondary, airSecondary })
            {
                if (pool.current < pool.max) pool.current = Mathf.Min(pool.max, pool.current + pool.regen);
                else pool.current = pool.max;
            }
        }

        void PlayReload(AmmoPool pool, bool full)
        {
            if (Application.isPlaying)
            {
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                }
                // GML: audio_stop_sound(sfx_reload_half) before sfx_reload_full; neither restarts while playing.
                if (full) audioSource.Stop();
                var clip = full ? reloadFullClip : reloadHalfClip;
                if (clip != null && !(audioSource.isPlaying && audioSource.clip == clip))
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                }
            }
            Reloaded?.Invoke(pool, full);
        }

#if UNITY_EDITOR
        /// <summary>Fills the reload sounds with the ported sfx when the component is added in the Editor.</summary>
        void Reset()
        {
            const string folder = "Assets/Unity.Hare/Audio/Weapons/";
            reloadHalfClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(folder + "sfx_reload_half.wav");
            reloadFullClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(folder + "sfx_reload_full.wav");
        }
#endif
    }
}
