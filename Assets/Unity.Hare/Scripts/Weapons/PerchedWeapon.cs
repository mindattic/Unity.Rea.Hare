using System;
using UnityEngine;
using Unity.Hare.Content;
using Unity.Hare.Player;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// The view on a gun of the ship (shotgun, flamer, mine launcher). The firing rules of
    /// fire_primary/fire_secondary (gmlHARE) live in Core's <see cref="GunRack"/>: only from the gun's firing state
    /// (perched, or in the air for the mines), never while shielding or dead, one shot per cooldown, and only with
    /// enough ammo (else sfx_empty_clip), spending the pool its slot selects. The cooldown comes from the
    /// <see cref="WeaponTable"/>. This component installs the gun, reads the mouse into commands and, when the
    /// rules say a shot went out, spawns its projectiles.
    /// Positions use 1 unit = 100 GML pixels; speeds keep the original's ratio to flight_speed
    /// (5 px/step there, 5 units/s in <see cref="FlightTuning"/>), so GML px/step map to units/s one to one. Both
    /// are placeholders until real sprites set the world scale.
    /// </summary>
    [RequireComponent(typeof(PlayerShipController))]
    public abstract class PerchedWeapon : SeatView
    {
        [Header("Trigger (GML: fire_primary / fire_secondary)")]
        [SerializeField] WeaponSlot slot;

        static PerchedMuzzleGeometry Muzzle => GameContent.Current.WeaponPhysicals.PerchedMuzzle;

        PlayerShipController player;
        PlayerAmmo ammo;
        int key;

        /// <summary>Fires after every successful shot (muzzle flash, shell, sound: UH-T30/UH-T31).</summary>
        public event Action Fired;

        /// <summary>Fires when the trigger is pulled without enough ammo (GML: sfx_empty_clip).</summary>
        public event Action OutOfAmmo;

        /// <summary>Which rules-side gun this is.</summary>
        protected abstract GunType GunType { get; }

        /// <summary>The key this gun is installed under on the ship.</summary>
        protected int Key => key != 0 ? key : key = Seat.AllocateKey();

        Gun Gun => Core.Guns.Find(Key);

        public WeaponSlot Slot
        {
            get => slot;
            set
            {
                slot = value;
                if (Gun != null) Install();
            }
        }

        public float CooldownRemaining => Gun != null ? Gun.Cooldown : 0f;

        /// <summary>
        /// The pool this weapon spends: perched weapons fire_primary's ground_ammo_primary or fire_secondary's
        /// ground_ammo_secondary by slot; the mine launcher spends the air secondary.
        /// </summary>
        public AmmoPool AmmoPool => GunType == GunType.MineLauncher
            ? AmmoPool.AirSecondary
            : slot == WeaponSlot.Primary ? AmmoPool.GroundPrimary : AmmoPool.GroundSecondary;

        /// <summary>The ship's shared ammo economy.</summary>
        public PlayerAmmo AmmoEconomy => ammo != null ? ammo : ammo = PlayerAmmo.Of(gameObject);

        public int Ammo => AmmoEconomy.Current(AmmoPool);
        public int MaxAmmo => AmmoEconomy.Max(AmmoPool);
        public int AmmoCost => AmmoEconomy.Cost(AmmoPool);

        /// <summary>Lazy, like every other cached-component lookup here: Update could in principle run before
        /// ResetState has (Unity's Awake order across components on one GameObject is undefined).</summary>
        protected PlayerShipController Player => player != null ? player : player = GetComponent<PlayerShipController>();

        /// <summary>Only sets the serialized default; scene/prefab values still override it.</summary>
        protected PerchedWeapon(WeaponSlot defaultSlot) => slot = defaultSlot;

        void Awake() => ResetState();

        /// <summary>Everything Awake does, callable from EditMode tests (Awake doesn't run there).</summary>
        public void ResetState()
        {
            player = GetComponent<PlayerShipController>();
            ammo = PlayerAmmo.Of(gameObject);
            Install();
        }

        void Install() => Send(new InstallGun(Id, Key, GunType, slot));

        /// <summary>GML gunX/gunY then originX/originY: where the projectiles leave the barrel.</summary>
        public Vector2 MuzzlePosition
        {
            get
            {
                var muzzle = Muzzle;
                Vector2 gun = (Vector2)transform.position + Gml.Dir(transform.eulerAngles.z) * Gml.Px(muzzle.GunOffsetPx);
                return gun + Gml.Dir(player.AimDirection) * Gml.Px(muzzle.MuzzleDistancePx);
            }
        }

        /// <summary>True when a trigger pull right now would shoot.</summary>
        public bool CanFire => Gun != null && Core.Guns.CanFire(Gun);

        /// <summary>One trigger pull. Returns true if the weapon shot.</summary>
        public bool TryFire() => Send(new FireGun(Id, Key)).Accepted;

        /// <summary>GML next_ground_weapon / next_air_weapon: can_shoot = true, the switch re-arms the gun.</summary>
        public void Rearm() => Send(new RearmGun(Id, Key));

        /// <summary>Put away by a weapon switch: let go of the trigger so ammo regen resumes.</summary>
        public void Holster() => Send(new HolsterGun(Id, Key));

        /// <summary>Spawn this weapon's projectiles from <paramref name="origin"/> toward <paramref name="aim"/> (degrees).</summary>
        protected abstract void Shoot(Vector2 origin, float aim);

        /// <summary>
        /// GML mouse1_down: pressing stops the primary regen alarm and releasing restarts both
        /// (<see cref="AmmoPools.PressTrigger"/>, <see cref="AmmoPools.ReleaseTrigger"/>).
        /// </summary>
        public bool TriggerHeld
        {
            get => Gun != null && Gun.TriggerHeld;
            set
            {
                if (value == TriggerHeld) return;
                if (value) Send(new PullTrigger(Id, Key)); else Send(new ReleaseTrigger(Id, Key));
            }
        }

        /// <summary>The re-arm cooldown; public so EditMode tests can drive time. Regen is <see cref="PlayerAmmo.Tick"/>'s.</summary>
        public void Tick(float dt) => Send(new TickGun(Id, Key, dt));

        void Update()
        {
            TriggerHeld = ReadTrigger();
            if (TriggerHeld) TryFire();
            Tick(Time.deltaTime);
        }

        bool ReadTrigger()
        {
            var controls = Player.Controls.Player;
            bool secondaryRequested = controls.FireSecondary.IsPressed(); // Ctrl + left mouse is fire_secondary
            bool primaryRequested = controls.FirePrimary.IsPressed() && !secondaryRequested;
            return slot == WeaponSlot.Secondary ? secondaryRequested : primaryRequested;
        }

        protected override void OnPlayerEvent(PlayerEvent e)
        {
            switch (e)
            {
                case GunFired fired when fired.Key == key:
                    Shoot(MuzzlePosition, player.AimDirection);
                    Fired?.Invoke();
                    break;
                case GunDryFired dry when dry.Key == key:
                    OutOfAmmo?.Invoke();
                    break;
            }
        }

        protected override void OnDestroy()
        {
            if (key != 0 && HasSeat) Seat.Send(new RemoveGun(Id, key));
            base.OnDestroy();
        }
    }
}
