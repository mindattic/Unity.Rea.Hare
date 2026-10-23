using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Hare.Player;

namespace Unity.Hare.Weapons
{
    /// <summary>GML: which fire script a weapon answers to. Left mouse = fire_primary, Ctrl + left mouse = fire_secondary.</summary>
    public enum WeaponSlot
    {
        Primary,
        Secondary
    }

    /// <summary>
    /// The shared firing rules of fire_primary/fire_secondary (gmlHARE) for the weapons the ship
    /// uses while perched: only from state_idle, never while shielding or dead, one shot per
    /// cooldown (GML can_shoot + alarm_reset_fire / alarm_reset_fire_secondary), and only with
    /// enough ammo for the shot (GML ground_ammo_* &gt;= ground_ammo_*_cost, else sfx_empty_clip).
    /// Ammo is the ship's shared <see cref="PlayerAmmo"/> economy: each weapon spends the pool its
    /// slot and firing state select (<see cref="AmmoPool"/>), and the regen alarms live there too.
    /// Positions use 1 unit = 100 GML pixels; speeds keep the original's ratio to flight_speed
    /// (5 px/step there, 5 units/s in <see cref="PlayerShipController"/>), so GML px/step map to
    /// units/s one to one. Both are placeholders until real sprites set the world scale.
    /// </summary>
    [RequireComponent(typeof(PlayerShipController))]
    public abstract class PerchedWeapon : MonoBehaviour
    {
        [Header("Trigger (GML: fire_primary / fire_secondary)")]
        [SerializeField] WeaponSlot slot;

        [Header("Muzzle (GML: gunX/gunY = 13 px behind the ship, origin 34 px along mouse_angle)")]
        [SerializeField] float gunOffset = -0.13f;
        [SerializeField] float muzzleDistance = 0.34f;

        PlayerShipController player;
        PlayerAmmo ammo;
        bool triggerHeld;

        /// <summary>Fires after every successful shot (muzzle flash, shell, sound: UH-T30/UH-T31).</summary>
        public event Action Fired;

        /// <summary>Fires when the trigger is pulled without enough ammo (GML: sfx_empty_clip).</summary>
        public event Action OutOfAmmo;

        public WeaponSlot Slot { get => slot; set => slot = value; }
        public float CooldownRemaining { get; private set; }

        protected abstract float CooldownSeconds { get; }

        /// <summary>
        /// The pool this weapon spends: perched weapons fire_primary's ground_ammo_primary or
        /// fire_secondary's ground_ammo_secondary by slot; <see cref="MineLauncher"/> overrides.
        /// </summary>
        public virtual AmmoPool AmmoPool => slot == WeaponSlot.Primary ? AmmoPool.GroundPrimary : AmmoPool.GroundSecondary;

        /// <summary>The ship's shared ammo economy.</summary>
        public PlayerAmmo AmmoEconomy => ammo != null ? ammo : ammo = PlayerAmmo.Of(gameObject);

        public int Ammo => AmmoEconomy.Current(AmmoPool);
        public int MaxAmmo => AmmoEconomy.Max(AmmoPool);
        public int AmmoCost => AmmoEconomy.Cost(AmmoPool);

        protected PlayerShipController Player => player;

        /// <summary>Only sets the serialized default; scene/prefab values still override it.</summary>
        protected PerchedWeapon(WeaponSlot defaultSlot) => slot = defaultSlot;

        void Awake() => ResetState();

        /// <summary>Everything Awake does, callable from EditMode tests (Awake doesn't run there).</summary>
        public void ResetState()
        {
            player = GetComponent<PlayerShipController>();
            ammo = PlayerAmmo.Of(gameObject);
            CooldownRemaining = 0f;
            TriggerHeld = false;
        }

        /// <summary>GML gunX/gunY then originX/originY: where the projectiles leave the barrel.</summary>
        public Vector2 MuzzlePosition
        {
            get
            {
                Vector2 gun = (Vector2)transform.position + Gml.Dir(transform.eulerAngles.z) * gunOffset;
                return gun + Gml.Dir(player.AimDirection) * muzzleDistance;
            }
        }

        /// <summary>
        /// GML fire_primary/fire_secondary's state branch this weapon fires from: perched
        /// (state_idle) by default; <see cref="MineLauncher"/> fires from the in-air branch.
        /// </summary>
        protected virtual bool InFiringState(PlayerShipController ship) => ship.State == PlayerFlightState.Idle;

        /// <summary>True when a trigger pull right now would shoot.</summary>
        public bool CanFire =>
            player != null
            && InFiringState(player)
            && !player.IsShielding
            && CooldownRemaining <= 0f
            && AmmoEconomy.HasAmmo(AmmoPool);

        /// <summary>One trigger pull. Returns true if the weapon shot.</summary>
        public bool TryFire()
        {
            if (player == null || !InFiringState(player) || player.IsShielding) return false;
            if (CooldownRemaining > 0f) return false;
            if (!AmmoEconomy.TrySpend(AmmoPool)) // GML: x_ammo_y >= cost, then subtract and clamp at 0
            {
                OutOfAmmo?.Invoke();
                return false;
            }

            CooldownRemaining = CooldownSeconds;
            Shoot(MuzzlePosition, player.AimDirection);
            Fired?.Invoke();
            return true;
        }

        /// <summary>GML next_ground_weapon / next_air_weapon: can_shoot = true, the switch re-arms the gun.</summary>
        public void Rearm() => CooldownRemaining = 0f;

        /// <summary>Put away by a weapon switch: let go of the trigger so ammo regen resumes.</summary>
        public void Holster() => TriggerHeld = false;

        /// <summary>Spawn this weapon's projectiles from <paramref name="origin"/> toward <paramref name="aim"/> (degrees).</summary>
        protected abstract void Shoot(Vector2 origin, float aim);

        /// <summary>
        /// GML mouse1_down: pressing stops the primary regen alarm and releasing restarts both
        /// (<see cref="PlayerAmmo.PressTrigger"/>, <see cref="PlayerAmmo.ReleaseTrigger"/>).
        /// </summary>
        public bool TriggerHeld
        {
            get => triggerHeld;
            set
            {
                if (value == triggerHeld) return;
                triggerHeld = value;
                if (value) AmmoEconomy.PressTrigger(this); else AmmoEconomy.ReleaseTrigger(this);
            }
        }

        /// <summary>The re-arm cooldown; public so EditMode tests can drive time. Regen is <see cref="PlayerAmmo.Tick"/>'s.</summary>
        public void Tick(float dt)
        {
            CooldownRemaining -= dt;
            if (CooldownRemaining <= 1e-5f) CooldownRemaining = 0f; // whole GML steps must not leave float dust
        }

        void Update()
        {
            TriggerHeld = ReadTrigger();
            if (TriggerHeld) TryFire();
            Tick(Time.deltaTime);
        }

        bool ReadTrigger()
        {
            var mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed) return false;
            var keyboard = Keyboard.current;
            bool ctrl = keyboard != null && keyboard.ctrlKey.isPressed;
            return slot == WeaponSlot.Secondary ? ctrl : !ctrl;
        }
    }
}
