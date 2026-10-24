using UnityEngine;
using Unity.Hare.Content;
using Unity.Hare.Player;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// Ports shoot_laser on the perched ground primary slot (gmlHARE): the laser is a weapon of Q's cycle
    /// (<see cref="GroundWeaponLoadout"/>) fired with the left mouse while perched, not an air weapon. Pulling the
    /// trigger spawns obj_laser (<see cref="LaserBeam"/>), which stays on until the trigger is released
    /// (with(obj_laser) instance_destroy(); can_shoot = true) and only then can fire again; the rules in Core's
    /// <see cref="GunRack"/> raise <see cref="GunBeamStopped"/> for that, or when the ship dies or shields. Costs 1 of
    /// ground_ammo_primary per pull (shared with the vulcan).
    /// </summary>
    public class LaserGun : PerchedWeapon, IGroundWeapon
    {
        protected override GunType GunType => GunType.Laser;

        public LaserGun() : base(WeaponSlot.Primary) { }

        public GroundWeapon GroundKind => GroundWeapon.Laser;
        public bool IsGroundPrimary => Slot == WeaponSlot.Primary;

        /// <summary>The beam while it is on, else null.</summary>
        public LaserBeam ActiveLaser { get; private set; }

        /// <summary>Where the beam ends: 2000 px from the gun, the muzzle being 34 px out.</summary>
        public Vector2 BeamEndPosition => MuzzlePosition + Gml.Dir(Player.AimDirection) *
            Gml.Px(GameContent.Current.WeaponPhysicals.Laser.BeamLengthPx - GameContent.Current.WeaponPhysicals.PerchedMuzzle.MuzzleDistancePx);

        protected override void Shoot(Vector2 origin, float aim)
        {
            StopLaser();
            var go = new GameObject("Laser");
            ActiveLaser = go.AddComponent<LaserBeam>();
            ActiveLaser.Init(this);
        }

        protected override void OnPlayerEvent(PlayerEvent e)
        {
            base.OnPlayerEvent(e);
            if (e is GunBeamStopped stopped && stopped.Key == Key) StopLaser();
        }

        void StopLaser()
        {
            if (ActiveLaser == null) return;
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
