using UnityEngine;
using Unity.Hare.Player;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// Ports shoot_mine and fire_secondary's in-air branch (gmlHARE). obj_player starts with
    /// air_weapon_secondary = weapon_mine, so Ctrl + left mouse while flying (any state but
    /// state_idle, never dead or shielding) drops a <see cref="Mine"/> at the muzzle (gun + 34 px
    /// along mouse_angle) and plays sfx_fire_grenade (<see cref="PerchedWeapon.Fired"/>). The
    /// launcher re-arms after the mine's delay of 30 steps (alarm_reset_fire_secondary). Ammo is
    /// <see cref="PlayerAmmo"/>'s air_ammo_secondary: 8, 1 per mine (set_ammo_cost), +1 every
    /// room_speed once the trigger has been released (Alarm 10). Mines are the surface-clearing
    /// weapon: they drift down onto whatever comes near, whereas the perched ground weapons
    /// (<see cref="GroundWeaponLoadout"/>) fire from rocks.
    /// </summary>
    public class MineLauncher : PerchedWeapon
    {
        [Header("Mine")]
        [SerializeField] Sprite mineSprite;

        /// <summary>The most recent mine dropped (null once it is gone).</summary>
        public Mine LastMine { get; private set; }

        protected override float CooldownSeconds => Gml.Steps(Mine.RearmSteps);

        /// <summary>fire_secondary's in-air branch spends air_ammo_secondary.</summary>
        public override AmmoPool AmmoPool => AmmoPool.AirSecondary;

        public MineLauncher() : base(WeaponSlot.Secondary) { }

        /// <summary>fire_secondary's "In Air" branch: everything but perched (and dead, which TryFire already refuses).</summary>
        protected override bool InFiringState(PlayerShipController ship) => ship.State == PlayerFlightState.Moving;

        protected override void Shoot(Vector2 origin, float aim)
        {
            var go = new GameObject("Mine");
            go.transform.position = new Vector3(origin.x, origin.y, transform.position.z);
            if (mineSprite != null) go.AddComponent<SpriteRenderer>().sprite = mineSprite;
            LastMine = go.AddComponent<Mine>();
            LastMine.Init(gameObject, origin);
        }
    }
}
