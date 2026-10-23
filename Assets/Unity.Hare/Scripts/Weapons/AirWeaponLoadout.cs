using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Hare.Player;

namespace Unity.Hare.Weapons
{
    /// <summary>The player's secondary air weapons, in next_air_weapon's cycling order.</summary>
    public enum AirWeapon
    {
        Laser,
        Railgun,
        Rocket
    }

    /// <summary>
    /// The player's secondary weapon slot (GML fire_secondary / next_air_weapon on obj_player):
    /// holds the unlocked air weapons, cycles through them with E (GML key-press 69), and fires the
    /// current one while the secondary trigger (right mouse) is held. The original fired the
    /// secondary with Ctrl + left mouse; left mouse is the vulcan's (UH-T8) here, so the secondary
    /// gets its own button rather than firing both at once. Firing is refused while dead or shielding, and costs ammo.
    ///
    /// Per weapon, as in the GML: the laser (shoot_laser/obj_laser) stays on until the trigger is
    /// released and only then can fire again; the railgun (shoot_railgun) re-arms after 240 steps;
    /// rockets (shoot_rocket) after 25 steps. Switching weapons re-arms immediately
    /// (next_air_weapon: can_shoot = true). Ammo numbers are obj_player's (laser/railgun draw on
    /// ground_ammo_primary 150, regen 10; rockets on air_ammo_primary 20, regen 4), regenerating
    /// every room_speed steps while the trigger is up; UH-T12 owns the shared ammo economy and may
    /// fold these pools into it. Sounds are left to the Audio epic through the events below.
    /// </summary>
    [RequireComponent(typeof(PlayerShipController))]
    public class AirWeaponLoadout : MonoBehaviour
    {
        [Serializable]
        class AmmoPool
        {
            public int max;
            public int cost = 1;
            public int regen;
            [NonSerialized] public int current;
        }

        [Header("Muzzle (GML: gunX/gunY = x + lengthdir(-13, image_angle); origin = gun + lengthdir(34, mouse_angle))")]
        [SerializeField] float gunOffsetPx = -13f;
        [SerializeField] float muzzleOffsetPx = 34f;
        [SerializeField] float beamLengthPx = 2000f;

        [Header("Re-arm (GML: alarm[alarm_reset_fire], in steps at 120/s)")]
        [SerializeField] float railgunCooldownSteps = 240f;
        [SerializeField] float rocketCooldownSteps = 25f;

        [Header("Ammo (GML: obj_player Create)")]
        [SerializeField] AmmoPool laserAmmo = new AmmoPool { max = 150, regen = 10 };
        [SerializeField] AmmoPool railgunAmmo = new AmmoPool { max = 150, regen = 10 };
        [SerializeField] AmmoPool rocketAmmo = new AmmoPool { max = 20, regen = 4 };
        [SerializeField] float ammoRegenDelaySteps = 120f;

        [Header("Unlocked air weapons")]
        [SerializeField] List<AirWeapon> unlocked = new List<AirWeapon> { AirWeapon.Laser, AirWeapon.Railgun, AirWeapon.Rocket };

        PlayerShipController ship;
        float cooldown;
        float regenTimer;
        bool triggerHeld;

        public AirWeapon Current { get; private set; }
        /// <summary>GML: can_shoot for the secondary slot.</summary>
        public bool CanShoot { get; private set; } = true;
        public bool TriggerHeld => triggerHeld;
        public LaserBeam ActiveLaser { get; private set; }
        public IReadOnlyList<AirWeapon> Unlocked => unlocked;

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
            StopLaser();
            foreach (var pool in Pools()) pool.current = pool.max;
            Current = unlocked.Count > 0 ? unlocked[0] : AirWeapon.Rocket;
            CanShoot = true;
            cooldown = 0f;
            triggerHeld = false;
            regenTimer = Gml.Steps(ammoRegenDelaySteps);
        }

        IEnumerable<AmmoPool> Pools()
        {
            yield return laserAmmo;
            yield return railgunAmmo;
            yield return rocketAmmo;
        }

        AmmoPool PoolFor(AirWeapon weapon) => weapon switch
        {
            AirWeapon.Laser => laserAmmo,
            AirWeapon.Railgun => railgunAmmo,
            _ => rocketAmmo
        };

        public int Ammo(AirWeapon weapon) => PoolFor(weapon).current;
        public int MaxAmmo(AirWeapon weapon) => PoolFor(weapon).max;

        public bool IsUnlocked(AirWeapon weapon) => unlocked.Contains(weapon);

        /// <summary>Adds <paramref name="weapon"/> to the cycle (upgrades/pickups call this).</summary>
        public void Unlock(AirWeapon weapon)
        {
            if (unlocked.Contains(weapon)) return;
            unlocked.Add(weapon);
            unlocked.Sort();
            if (unlocked.Count == 1) Current = weapon;
        }

        /// <summary>Removes <paramref name="weapon"/> from the cycle; if it was equipped, moves to the next one.</summary>
        public void Lock(AirWeapon weapon)
        {
            if (!unlocked.Contains(weapon)) return;
            bool wasCurrent = Current == weapon;
            if (wasCurrent && unlocked.Count > 1) NextAirWeapon();
            unlocked.Remove(weapon);
        }

        /// <summary>
        /// GML next_air_weapon: equip the next unlocked air weapon in order, wrapping around, and
        /// re-arm. Returns false when there is nothing else to switch to.
        /// </summary>
        public bool NextAirWeapon()
        {
            if (unlocked.Count == 0) return false;
            int index = unlocked.IndexOf(Current);
            var next = unlocked[(index + 1) % unlocked.Count];
            if (next == Current && index >= 0) return false;

            StopLaser();
            Current = next;
            CanShoot = true;
            cooldown = 0f;
            Switched?.Invoke(Current);
            return true;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame) NextAirWeapon();

            SetTrigger(mouse != null && mouse.rightButton.isPressed);
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// The secondary trigger. Releasing it drops the laser and re-arms it (GML global left
        /// release: with(obj_laser) instance_destroy(); can_shoot = true) and restarts ammo regen.
        /// </summary>
        public void SetTrigger(bool held)
        {
            if (held == triggerHeld) return;
            triggerHeld = held;
            if (held) return;

            if (ActiveLaser != null || Current == AirWeapon.Laser)
            {
                StopLaser();
                CanShoot = true;
            }
            regenTimer = Gml.Steps(ammoRegenDelaySteps);
        }

        /// <summary>Re-arm timer, ammo regen, and firing while the trigger is held.</summary>
        public void Tick(float dt)
        {
            if (ActiveLaser != null && (Ship.State == PlayerFlightState.Dead || Ship.IsShielding)) StopLaser();

            if (!CanShoot && cooldown > 0f)
            {
                cooldown -= dt;
                if (cooldown <= 0f) { cooldown = 0f; CanShoot = true; } // GML alarm 0: can_shoot = true
            }

            if (triggerHeld)
            {
                TryFire();
            }
            else
            {
                regenTimer -= dt;
                if (regenTimer <= 0f)
                {
                    foreach (var pool in Pools()) pool.current = Mathf.Min(pool.max, pool.current + pool.regen);
                    regenTimer += Gml.Steps(ammoRegenDelaySteps);
                }
            }
        }

        /// <summary>GML fire_secondary for the current air weapon. Returns true if a shot went out.</summary>
        public bool TryFire()
        {
            if (!CanShoot || !IsUnlocked(Current) || Ship.State == PlayerFlightState.Dead || Ship.IsShielding) return false;

            var pool = PoolFor(Current);
            if (pool.current < pool.cost)
            {
                DryFired?.Invoke(Current);
                return false;
            }
            pool.current = Mathf.Max(0, pool.current - pool.cost);
            CanShoot = false;

            switch (Current)
            {
                case AirWeapon.Laser:
                    var beamGo = new GameObject("Laser");
                    ActiveLaser = beamGo.AddComponent<LaserBeam>();
                    ActiveLaser.Init(this);
                    cooldown = 0f; // re-armed only by releasing the trigger
                    break;
                case AirWeapon.Railgun:
                    var railGo = new GameObject("Railgun");
                    railGo.AddComponent<RailgunShot>().Init(MuzzlePosition, Ship.AimDirection, BeamLengthUnits);
                    cooldown = Gml.Steps(railgunCooldownSteps);
                    break;
                default:
                    var rocketGo = new GameObject("Rocket");
                    rocketGo.AddComponent<Rocket>().Init(MuzzlePosition, Ship.AimDirection);
                    cooldown = Gml.Steps(rocketCooldownSteps);
                    break;
            }
            Fired?.Invoke(Current);
            return true;
        }

        void StopLaser()
        {
            if (ActiveLaser == null) { ActiveLaser = null; return; }
            if (Application.isPlaying) Destroy(ActiveLaser.gameObject); else DestroyImmediate(ActiveLaser.gameObject);
            ActiveLaser = null;
        }

        void OnDestroy() => StopLaser();
    }
}
