using UnityEngine;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// Ports shoot_railgun on the perched ground primary slot (gmlHARE): the railgun is a weapon of Q's cycle
    /// (<see cref="GroundWeaponLoadout"/>) fired with the left mouse while perched, not an air weapon. One shot draws
    /// a 2000 px <see cref="RailgunShot"/> from the muzzle along the aim and re-arms after 240 steps
    /// (alarm_reset_fire). Costs 1 of ground_ammo_primary per shot (shared with the vulcan).
    /// </summary>
    public class RailgunGun : PerchedWeapon, IGroundWeapon
    {
        [SerializeField] float beamLengthPx = 2000f;

        protected override GunType GunType => GunType.Railgun;

        public RailgunGun() : base(WeaponSlot.Primary) { }

        public GroundWeapon GroundKind => GroundWeapon.Railgun;
        public bool IsGroundPrimary => Slot == WeaponSlot.Primary;

        public float BeamLengthUnits => Gml.Px(beamLengthPx);

        protected override void Shoot(Vector2 origin, float aim)
        {
            var go = new GameObject("Railgun");
            go.AddComponent<RailgunShot>().Init(origin, aim, BeamLengthUnits);
        }
    }
}
