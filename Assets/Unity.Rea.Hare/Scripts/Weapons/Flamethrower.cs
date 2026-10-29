using UnityEngine;
using Unity.Hare.Content;
using Unity.Hare.Effects;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// Ports shoot_flamer (gmlHARE, the version fire_primary/fire_secondary call; the older
    /// shoot_flamethrower differs only in a 10-step reload): while the trigger is held, one
    /// <see cref="Flame"/> every 8 steps (alarm_reset_fire = 8), straight along the aim at
    /// 4 px/step. The cone comes from each flame growing as it travels. Costs 1 ammo per flame
    /// (set_ammo_cost); as a ground primary it draws on ground_ammo_primary, which starts at 150
    /// and refills 10 per second (<see cref="PlayerAmmo"/>). Each flame puffs a small black
    /// ef_smokeup under everything at the muzzle.
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
            Effects.HareArt.Show(go, Flame.SpriteName, Flame.Depth); // obj_flame's spr_powder_fire (U.HARE-T95)
            LastFlame = go.AddComponent<Flame>();
            LastFlame.Init(Player, aim, Gml.Px(stats.FlameSpeed) * Gml.StepsPerSecond, stats.FlameLifetime, stats.FlameRadius);
            // GML: effect_create_below(ef_smokeup, X, Y, 0, c_black) at the muzzle with every flame.
            GmlEffects.CreateBelow(GmlEffectKind.SmokeUp, origin, 0, Color.black);
        }
    }
}
