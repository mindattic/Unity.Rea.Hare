using System.Collections.Generic;
using UnityEngine;
using Unity.Hare.Content;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// Ports shoot_shotgun (gmlHARE): one blast of 16 pellets, each an obj_bullet with spr_pellet
    /// (here a <see cref="Bullet"/>, so pellets hit <see cref="IProjectileTarget"/>s and bounce off
    /// shield walls like vulcan rounds), each starting 0..12 px further along the aim than the
    /// muzzle and flying at 12 px/step along the aim plus a whole-degree spread of -8..+8. One
    /// blast per room_speed (1 s, alarm_reset_fire_secondary). In HARE the shotgun is the default
    /// ground secondary (Ctrl + left mouse): ground_ammo_secondary, 12 ammo, 4 per blast, +2 per
    /// second; as a ground primary it costs 1 of ground_ammo_primary (<see cref="PlayerAmmo"/>).
    /// </summary>
    public class Shotgun : PerchedWeapon, IGroundWeapon
    {
        [Header("Blast (GML shoot_shotgun)")]
        [SerializeField] Sprite pelletSprite;

        static ShotgunPhysicalStats Stats => GameContent.Current.WeaponPhysicals.Shotgun;

        readonly List<Bullet> lastBlast = new List<Bullet>();

        public int PelletCount => Stats.PelletCount;
        public int SpreadDegrees => Stats.SpreadDegrees;
        public float Grouping => Stats.Grouping;
        public float PelletSpeed => Stats.PelletSpeed;

        /// <summary>The pellets of the most recent blast (some may since have been destroyed).</summary>
        public IReadOnlyList<Bullet> LastBlast => lastBlast;

        protected override GunType GunType => GunType.Shotgun;

        public Shotgun() : base(WeaponSlot.Secondary) { }

        public GroundWeapon GroundKind => GroundWeapon.Shotgun;
        /// <summary>next_ground_weapon can also put the shotgun in the primary slot; set <see cref="PerchedWeapon.Slot"/> to Primary for that.</summary>
        public bool IsGroundPrimary => Slot == WeaponSlot.Primary;

        protected override void Shoot(Vector2 origin, float aim)
        {
            var stats = Stats;
            lastBlast.Clear();
            for (int i = 0; i < stats.PelletCount; i++)
            {
                Vector2 start = origin + Gml.Dir(aim) * Random.Range(0f, stats.Grouping);
                float direction = aim + Random.Range(-stats.SpreadDegrees, stats.SpreadDegrees + 1);

                var go = new GameObject("ShotgunPellet");
                go.transform.position = new Vector3(start.x, start.y, transform.position.z);
                if (pelletSprite != null) go.AddComponent<SpriteRenderer>().sprite = pelletSprite;
                var pellet = go.AddComponent<Bullet>();
                pellet.Init(gameObject, direction, stats.PelletSpeed, Core.Table.BulletDamage(GunType.Shotgun), stats.PelletLifetime, stats.PelletRadius);
                lastBlast.Add(pellet);
            }
        }
    }
}
