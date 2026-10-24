using UnityEngine;
using Unity.Hare.Content;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// Ports shoot_flamer (gmlHARE, the version fire_primary/fire_secondary call; the older
    /// shoot_flamethrower differs only in a 10-step reload): while the trigger is held, one
    /// <see cref="Flame"/> every 8 steps (alarm_reset_fire = 8), straight along the aim at
    /// 4 px/step. The cone comes from each flame growing as it travels. Costs 1 ammo per flame
    /// (set_ammo_cost); as a ground primary it draws on ground_ammo_primary, which starts at 150
    /// and refills 10 per second (<see cref="PlayerAmmo"/>).
    /// </summary>
    public class Flamethrower : PerchedWeapon, IGroundWeapon
    {
        static FlamerPhysicalStats Stats => GameContent.Current.WeaponPhysicals.Flamer;

        public float FlameSpeed => Stats.FlameSpeed;
        public float FlameLifetime => Stats.FlameLifetime;

        /// <summary>The most recent flame fired (null once it burns out).</summary>
        public Flame LastFlame { get; private set; }

        protected override GunType GunType => GunType.Flamer;

        public Flamethrower() : base(WeaponSlot.Primary) { }

        public GroundWeapon GroundKind => GroundWeapon.Flamer;
        public bool IsGroundPrimary => Slot == WeaponSlot.Primary;

        protected override void Shoot(Vector2 origin, float aim)
        {
            var stats = Stats;
            var go = new GameObject("Flame");
            go.transform.position = origin;
            LastFlame = go.AddComponent<Flame>();
            LastFlame.Init(Player, aim, stats.FlameSpeed, stats.FlameLifetime, stats.FlameRadius);
        }
    }
}
