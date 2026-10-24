using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Hare.Player;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// The view on the player's secondary weapon slot (GML fire_secondary / next_air_weapon on obj_player). The
    /// rules live in Core's <see cref="AirLoadout"/>: the unlocked air weapons cycle with E (GML key-press 69) in
    /// the weapon table's order, and the current one fires while the secondary trigger is held. The original fired
    /// the secondary with Ctrl + left mouse; left mouse is the vulcan's (UH-T8) here, so the secondary gets its own
    /// button (right mouse) rather than firing both at once. Firing is refused while dead or shielding, and costs
    /// ammo.
    ///
    /// Per weapon, as in the GML: the laser (shoot_laser/obj_laser) stays on until the trigger is released and only
    /// then can fire again; the railgun (shoot_railgun) and rockets (shoot_rocket) re-arm after their table
    /// intervals (240 and 25 steps). Switching weapons re-arms immediately (next_air_weapon: can_shoot = true). Ammo
    /// is the ship's shared <see cref="PlayerAmmo"/>: the laser and railgun are GML ground weapons and spend
    /// ground_ammo_primary (shared with the vulcan), rockets spend air_ammo_primary. This component turns input into
    /// commands and, when the rules fire, spawns the laser, rail or rocket. Sounds are left to the Audio epic
    /// through the events below.
    /// </summary>
    [RequireComponent(typeof(PlayerShipController))]
    public class AirWeaponLoadout : SeatView
    {
        [Header("Muzzle (GML: gunX/gunY = x + lengthdir(-13, image_angle); origin = gun + lengthdir(34, mouse_angle))")]
        [SerializeField] float gunOffsetPx = -13f;
        [SerializeField] float muzzleOffsetPx = 34f;
        [SerializeField] float beamLengthPx = 2000f;

        PlayerShipController ship;
        PlayerAmmo ammo;

        public AirWeapon Current => Core.Air.Current;
        /// <summary>GML: can_shoot for the secondary slot.</summary>
        public bool CanShoot => Core.Air.CanShoot;
        public bool TriggerHeld => Core.Air.TriggerHeld;
        public LaserBeam ActiveLaser { get; private set; }
        public IReadOnlyList<AirWeapon> Unlocked => Core.Air.Unlocked;

        /// <summary>GML: audio_play_sound(sfx_switch) in next_air_weapon.</summary>
        public event Action<AirWeapon> Switched;
        /// <summary>GML: sfx_fire_laser / sfx_fire_railgun / sfx_set_rocket.</summary>
        public event Action<AirWeapon> Fired;
        /// <summary>GML: sfx_empty_clip, out of ammo.</summary>
        public event Action<AirWeapon> DryFired;

        public Vector2 GunPosition => (Vector2)transform.position + Gml.Dir(Ship.Direction) * Gml.Px(gunOffsetPx);
        public Vector2 MuzzlePosition => GunPosition + Gml.Dir(Ship.AimDirection) * Gml.Px(muzzleOffsetPx);
        public Vector2 BeamEndPosition => GunPosition + Gml.Dir(Ship.AimDirection) * Gml.Px(beamLengthPx);
        public float BeamLengthUnits => Gml.Px(beamLengthPx);

        PlayerShipController Ship => ship != null ? ship : ship = GetComponent<PlayerShipController>();

        void Awake() => ResetState();

        /// <summary>Awake's work, callable from EditMode tests (Awake does not run there).</summary>
        public void ResetState()
        {
            ship = GetComponent<PlayerShipController>();
            ammo = PlayerAmmo.Of(gameObject);
            Send(new ResetAirLoadout(Id));
            StopLaser();
        }

        /// <summary>The ship's shared ammo economy.</summary>
        public PlayerAmmo AmmoEconomy => ammo != null ? ammo : ammo = PlayerAmmo.Of(gameObject);

        /// <summary>The pool <paramref name="weapon"/> spends: laser/railgun ground_ammo_primary, rockets air_ammo_primary.</summary>
        public static AmmoPool PoolFor(AirWeapon weapon) => AmmoPools.PoolFor(weapon);

        public int Ammo(AirWeapon weapon) => AmmoEconomy.Current(PoolFor(weapon));
        public int MaxAmmo(AirWeapon weapon) => AmmoEconomy.Max(PoolFor(weapon));

        public bool IsUnlocked(AirWeapon weapon) => Core.Air.IsUnlocked(weapon);

        /// <summary>Adds <paramref name="weapon"/> to the cycle (upgrades/pickups call this).</summary>
        public void Unlock(AirWeapon weapon) => Send(new UnlockAirWeapon(Id, weapon));

        /// <summary>Removes <paramref name="weapon"/> from the cycle; if it was equipped, moves to the next one.</summary>
        public void Lock(AirWeapon weapon) => Send(new LockAirWeapon(Id, weapon));

        /// <summary>
        /// GML next_air_weapon: equip the next unlocked air weapon in order, wrapping around, and re-arm.
        /// Returns false when there is nothing else to switch to.
        /// </summary>
        public bool NextAirWeapon() => Send(new NextAirWeapon(Id)).Accepted;

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame) NextAirWeapon();

            SetTrigger(mouse != null && mouse.rightButton.isPressed);
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// The secondary trigger. Releasing it drops the laser and re-arms it (GML global left release:
        /// with(obj_laser) instance_destroy(); can_shoot = true) and restarts ammo regen; pressing stops the
        /// primary regen.
        /// </summary>
        public void SetTrigger(bool held) => Send(new SetAirTrigger(Id, held));

        /// <summary>Re-arm timer and firing while the trigger is held (regen is <see cref="PlayerAmmo.Tick"/>'s).</summary>
        public void Tick(float dt) => Send(new TickAir(Id, dt));

        /// <summary>GML fire_secondary for the current air weapon. Returns true if a shot went out.</summary>
        public bool TryFire() => Send(new FireAir(Id)).Accepted;

        protected override void OnPlayerEvent(PlayerEvent e)
        {
            switch (e)
            {
                case AirWeaponFired fired:
                    Spawn(fired.Weapon);
                    Fired?.Invoke(fired.Weapon);
                    break;
                case AirWeaponDryFired dry:
                    DryFired?.Invoke(dry.Weapon);
                    break;
                case AirWeaponSwitched switched:
                    Switched?.Invoke(switched.Weapon);
                    break;
                case AirLaserStopped _:
                    StopLaser();
                    break;
            }
        }

        void Spawn(AirWeapon weapon)
        {
            switch (weapon)
            {
                case AirWeapon.Laser:
                    var beamGo = new GameObject("Laser");
                    ActiveLaser = beamGo.AddComponent<LaserBeam>();
                    ActiveLaser.Init(this);
                    break;
                case AirWeapon.Railgun:
                    var railGo = new GameObject("Railgun");
                    railGo.AddComponent<RailgunShot>().Init(MuzzlePosition, Ship.AimDirection, BeamLengthUnits);
                    break;
                default:
                    var rocketGo = new GameObject("Rocket");
                    rocketGo.AddComponent<Rocket>().Init(MuzzlePosition, Ship.AimDirection);
                    break;
            }
        }

        void StopLaser()
        {
            if (ActiveLaser == null) { ActiveLaser = null; return; }
            if (Application.isPlaying) Destroy(ActiveLaser.gameObject); else DestroyImmediate(ActiveLaser.gameObject);
            ActiveLaser = null;
        }

        protected override void OnDestroy()
        {
            StopLaser();
            base.OnDestroy();
        }
    }
}
