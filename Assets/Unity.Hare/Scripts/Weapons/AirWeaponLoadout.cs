using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Hare.Content;
using Unity.Hare.Player;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// The view on the air primary slot (GML fire_primary's in-air branch / next_air_weapon on obj_player), as the
    /// original has it: while flying the left mouse fires the air weapon, and E (GML key-press 69) toggles it
    /// between the rocket and the mine. The rules live in Core's <see cref="AirLoadout"/>; firing is refused while
    /// perched, dead or shielding, and costs air_ammo_primary from the ship's shared <see cref="PlayerAmmo"/>. The
    /// laser and railgun are perched weapons on Q's cycle (<see cref="LaserGun"/>, <see cref="RailgunGun"/>), and
    /// Ctrl + left mouse is the secondary (<see cref="MineLauncher"/> in flight). The right mouse is flight's
    /// (brake, boost), not a weapon's.
    ///
    /// Per weapon, as in the GML: the rocket (shoot_rocket) re-arms after 25 steps and the mine (shoot_mine) after
    /// the mine's delay of 30; switching re-arms immediately (next_air_weapon: can_shoot = true). This component
    /// turns input into commands and, when the rules fire, spawns the rocket or mine. Sounds are left to the Audio
    /// epic through the events below.
    /// </summary>
    [RequireComponent(typeof(PlayerShipController))]
    public class AirWeaponLoadout : SeatView
    {
        [Header("Muzzle (GML: gunX/gunY = x + lengthdir(-13, image_angle); origin = gun + lengthdir(34, mouse_angle))")]
        [SerializeField] Sprite mineSprite;

        static PerchedMuzzleGeometry Muzzle => GameContent.Current.WeaponPhysicals.AirMuzzle;

        PlayerShipController ship;
        PlayerAmmo ammo;

        public AirWeapon Current => Core.Air.Current;
        /// <summary>GML: can_shoot for the secondary slot.</summary>
        public bool CanShoot => Core.Air.CanShoot;
        public bool TriggerHeld => Core.Air.TriggerHeld;
        /// <summary>The most recent mine dropped from the air primary (null once it is gone).</summary>
        public Mine LastMine { get; private set; }
        public IReadOnlyList<AirWeapon> Unlocked => Core.Air.Unlocked;

        /// <summary>GML: audio_play_sound(sfx_switch) in next_air_weapon.</summary>
        public event Action<AirWeapon> Switched;
        /// <summary>GML: sfx_set_rocket / sfx_fire_grenade.</summary>
        public event Action<AirWeapon> Fired;
        /// <summary>GML: sfx_empty_clip, out of ammo.</summary>
        public event Action<AirWeapon> DryFired;

        public Vector2 GunPosition => (Vector2)transform.position + Gml.Dir(Ship.Direction) * Gml.Px(Muzzle.GunOffsetPx);
        public Vector2 MuzzlePosition => GunPosition + Gml.Dir(Ship.AimDirection) * Gml.Px(Muzzle.MuzzleDistancePx);

        PlayerShipController Ship => ship != null ? ship : ship = GetComponent<PlayerShipController>();

        void Awake() => ResetState();

        /// <summary>Awake's work, callable from EditMode tests (Awake does not run there).</summary>
        public void ResetState()
        {
            ship = GetComponent<PlayerShipController>();
            ammo = PlayerAmmo.Of(gameObject);
            Send(new ResetAirLoadout(Id));
        }

        /// <summary>The ship's shared ammo economy.</summary>
        public PlayerAmmo AmmoEconomy => ammo != null ? ammo : ammo = PlayerAmmo.Of(gameObject);

        /// <summary>The pool <paramref name="weapon"/> spends: rockets and mines fired from fire_primary spend air_ammo_primary.</summary>
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
            var player = Ship.Controls.Player;
            if (player.NextAirWeapon.WasPressedThisFrame()) NextAirWeapon(); // GML key press 69 (E)

            bool secondaryHeld = player.FireSecondary.IsPressed(); // Ctrl + left mouse is fire_secondary
            SetTrigger(player.FirePrimary.IsPressed() && !secondaryHeld);
            Tick(Time.deltaTime);
        }

        /// <summary>The primary trigger (left mouse held in flight): pressing stops the primary regen, releasing restarts it.</summary>
        public void SetTrigger(bool held) => Send(new SetAirTrigger(Id, held));

        /// <summary>Re-arm timer and firing while the trigger is held (regen is <see cref="PlayerAmmo.Tick"/>'s).</summary>
        public void Tick(float dt) => Send(new TickAir(Id, dt));

        /// <summary>GML fire_primary for the current air weapon. Returns true if a shot went out.</summary>
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
            }
        }

        void Spawn(AirWeapon weapon)
        {
            if (weapon == AirWeapon.Mine)
            {
                var mineGo = new GameObject("Mine");
                mineGo.transform.position = new Vector3(MuzzlePosition.x, MuzzlePosition.y, transform.position.z);
                if (mineSprite != null) mineGo.AddComponent<SpriteRenderer>().sprite = mineSprite;
                LastMine = mineGo.AddComponent<Mine>();
                LastMine.Init(gameObject, MuzzlePosition);
                return;
            }

            var rocketGo = new GameObject("Rocket");
            rocketGo.AddComponent<Rocket>().Init(MuzzlePosition, Ship.AimDirection);
        }
    }
}
