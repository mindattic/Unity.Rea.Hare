using UnityEngine;
using Unity.Rea.Hare.Content;
using Unity.Rea.Hare.Player;

namespace Unity.Rea.Hare.Weapons
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

        /// <summary>shoot_laser's <c>audio_play_sound(sfx_fire_laser, 2, 0)</c>.</summary>
        public const string FireSound = "sfx_fire_laser";
        /// <summary>obj_laser's hum: <c>audio_play_sound(sfx_loop_laser, 1, 0)</c> whenever neither laser sound is playing.</summary>
        public const string LoopSound = "sfx_loop_laser";

        protected override void Shoot(Vector2 origin, float aim)
        {
            StopLaser();
            var go = new GameObject("Laser");
            ActiveLaser = go.AddComponent<LaserBeam>();
            ActiveLaser.Init(this);
            Audio.HareAudio.PlayVoice(FireSound); // shoot_laser (U.HARE-T30)
        }

        protected override void OnPlayerEvent(PlayerEvent e)
        {
            base.OnPlayerEvent(e);
            if (e is GunBeamStopped stopped && stopped.Key == Key)
            {
                StopLaser();
                // obj_player's left-button release with the laser up: audio_stop_sound(sfx_fire_laser);
                // audio_stop_sound(sfx_loop_laser) (U.HARE-T30). The port also puts the beam out on a switch, death or
                // shield, and its sound goes with it.
                Audio.HareAudio.Stop(FireSound);
                Audio.HareAudio.Stop(LoopSound);
            }
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
