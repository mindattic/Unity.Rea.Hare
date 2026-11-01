using System.Collections.Generic;
using UnityEngine;
using Unity.Rea.Hare.Content;
using Unity.Rea.Hare.Effects;

namespace Unity.Rea.Hare.Weapons
{
    /// <summary>
    /// Ports shoot_shotgun (gmlHARE): one blast of 16 pellets, each an obj_bullet with spr_pellet
    /// (here a <see cref="Bullet"/>, so pellets hit <see cref="IProjectileTarget"/>s and bounce off
    /// shield walls like vulcan rounds), each starting 0..12 px further along the aim than the
    /// muzzle and flying at 12 px/step along the aim plus a whole-degree spread of -8..+8. One
    /// blast per room_speed (1 s, alarm_reset_fire_secondary). In HARE the shotgun is the default
    /// ground secondary (Ctrl + left mouse): ground_ammo_secondary, 12 ammo, 4 per blast, +2 per
    /// second; as a ground primary it costs 1 of ground_ammo_primary (<see cref="PlayerAmmo"/>).
    /// Each blast leaves a <see cref="MuzzleFlash"/> turned to the last pellet's direction and a
    /// medium black ef_smoke under everything at the muzzle.
    /// </summary>
    public class Shotgun : PerchedWeapon, IGroundWeapon
    {
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
            float direction = aim;
            // The garage's "Spread" pips narrow the whole-degree pick (U.HARE-T24, BIBLE V49); 1 with none bought.
            float spreadScale = Core.Guns.Upgrades.ShotgunSpreadScale;
            for (int i = 0; i < stats.PelletCount; i++)
            {
                Vector2 start = origin + Gml.Dir(aim) * Random.Range(0f, stats.Grouping);
                direction = aim + Random.Range(-stats.SpreadDegrees, stats.SpreadDegrees + 1) * spreadScale;

                var go = new GameObject("ShotgunPellet");
                go.transform.position = new Vector3(start.x, start.y, transform.position.z);
                // pellet.sprite_index = spr_pellet at obj_bullet's depth -3 (U.HARE-T95); Bullet.Init turns it to the direction.
                HareArt.Show(go, "spr_pellet", -3);
                var pellet = go.AddComponent<Bullet>();
                pellet.Init(gameObject, direction, Gml.Px(stats.PelletSpeed) * Gml.StepsPerSecond, Core.Table.BulletDamage(GunType.Shotgun), stats.PelletLifetime, stats.PelletRadius);
                lastBlast.Add(pellet);
            }

            // GML: obj_muzzleflash at X, Y turned to pellet.direction (the last pellet's), then effect_create_below(ef_smoke, X, Y, 1, c_black).
            MuzzleFlash.Spawn(origin, direction);
            // GML: shell = instance_create(player.gunX, player.gunY, obj_cartridge); shell.sprite_index = spr_ammo_shell (U.HARE-T23).
            Pickups.PickupDirector.EjectCasing(Pickups.ScrapKind.Shell, VulcanWeapon.GunPoint(transform));
            // GML: audio_play_sound(sfx_fire_shotgun, 2, 0), just before the smoke (U.HARE-T30).
            Audio.HareAudio.Play(FireSound);
            GmlEffects.CreateBelow(GmlEffectKind.Smoke, origin, 1, Color.black);
        }

        /// <summary>shoot_shotgun's blast sound.</summary>
        public const string FireSound = "sfx_fire_shotgun";
    }
}
