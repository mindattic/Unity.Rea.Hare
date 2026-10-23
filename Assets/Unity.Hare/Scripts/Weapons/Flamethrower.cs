using UnityEngine;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// Ports shoot_flamer (gmlHARE, the version fire_primary/fire_secondary call; the older
    /// shoot_flamethrower differs only in a 10-step reload): while the trigger is held, one
    /// <see cref="Flame"/> every 8 steps (alarm_reset_fire = 8), straight along the aim at
    /// 4 px/step. The cone comes from each flame growing as it travels. Costs 1 ammo per flame
    /// (set_ammo_cost); as a ground primary it draws on the 150-round pool that refills 10 per
    /// second (obj_player Create).
    /// </summary>
    public class Flamethrower : PerchedWeapon
    {
        [Header("Stream (GML shoot_flamer / obj_flame)")]
        [SerializeField] float flameSpeed = 4f;                                         // speed = 4
        [SerializeField] float flameLifetime = 42f / 0.3f / Gml.StepsPerSecond;  // 42 frames at image_speed 0.3
        [SerializeField] float flameRadius = 0.5f;                                      // spr_powder_fire's 100 px bbox; scales with the flame
        [SerializeField] float cooldownSeconds = 8f / Gml.StepsPerSecond;         // alarm_reset_fire = 8

        [Header("Ammo (GML ground_ammo_primary)")]
        [SerializeField] int maxAmmo = 150;
        [SerializeField] int ammoCost = 1;
        [SerializeField] int ammoRegen = 10;

        public float FlameSpeed => flameSpeed;
        public float FlameLifetime => flameLifetime;

        /// <summary>The most recent flame fired (null once it burns out).</summary>
        public Flame LastFlame { get; private set; }

        protected override float CooldownSeconds => cooldownSeconds;
        public override int MaxAmmo => maxAmmo;
        public override int AmmoCost => ammoCost;
        protected override int AmmoRegenPerInterval => ammoRegen;

        public Flamethrower() : base(WeaponSlot.Primary) { }

        protected override void Shoot(Vector2 origin, float aim)
        {
            var go = new GameObject("Flame");
            go.transform.position = origin;
            LastFlame = go.AddComponent<Flame>();
            LastFlame.Init(Player, aim, flameSpeed, flameLifetime, flameRadius);
        }
    }
}
