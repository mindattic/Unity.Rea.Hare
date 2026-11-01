using System;
using UnityEngine;
using Unity.Rea.Hare.Audio;
using Unity.Rea.Hare.Content;
using Unity.Rea.Hare.Effects;
using Unity.Rea.Hare.Player;

namespace Unity.Rea.Hare.Weapons
{
    /// <summary>
    /// The view on the vulcan (gmlHARE: fire_primary's weapon_vulcan branch, shoot_vulcan, stop_vulcan_sfx and the
    /// left-mouse held/released events of obj_player). The rules live in Core's <see cref="GunRack"/>: while the
    /// trigger is held the gun fires at the <see cref="WeaponTable"/>'s spin-up ramp (the first shot waits
    /// base_rate, every shot after it a few steps less, down to the floor), spends one round of the ground primary
    /// pool per shot, clicks when empty, and spins down on release or running dry. This component reads the mouse,
    /// spawns the bullet when the rules fire one and plays the cues the rules raise.
    /// The vulcan is a ground weapon: like fire_primary it fires only while perched (Idle), never while dead or
    /// shielding. Steps are room_gamescreen's 120 per second.
    /// </summary>
    [RequireComponent(typeof(PlayerShipController))]
    public class VulcanWeapon : SeatView, IGroundWeapon
    {
        public const float StepsPerSecond = GameClock.StepsPerSecond;

        static VulcanPhysicalStats Stats => GameContent.Current.WeaponPhysicals.Vulcan;

        [Header("Sound (sfx_vulcan_*, sfx_empty_clip; the reloads are PlayerAmmo's)")]
        [SerializeField] AudioClip spinUpClip;
        [SerializeField] AudioClip loopClip;
        [SerializeField] AudioClip spinDownClip;
        [SerializeField] AudioClip emptyClipClip;

        PlayerShipController ship;
        PlayerAmmo ammo;
        AudioSource loopSource;
        AudioSource oneShotSource;
        float emptyClickTimer;
        int key;

        int Key => key != 0 ? key : key = Seat.AllocateKey();
        Gun Gun => Core.Guns.Find(Key);

        /// <summary>Lazy, like every other cached-component lookup here: Update could in principle run before
        /// ResetState has (Unity's Awake order across components on one GameObject is undefined).</summary>
        PlayerShipController Ship => ship != null ? ship : ship = GetComponent<PlayerShipController>();

        /// <summary>The ship's shared ammo economy; the vulcan spends ground_ammo_primary.</summary>
        public PlayerAmmo AmmoEconomy => ammo != null ? ammo : ammo = PlayerAmmo.Of(gameObject);
        public int Ammo => AmmoEconomy.Current(AmmoPool.GroundPrimary);
        public int MaxAmmo => AmmoEconomy.Max(AmmoPool.GroundPrimary);
        public bool TriggerHeld => Gun != null && Gun.TriggerHeld;

        /// <summary>GML: can_shoot. False until the fire_rate alarm (alarm_reset_fire) runs out.</summary>
        public bool CanShoot => Gun == null || Gun.CanShoot;

        /// <summary>GML: fire_rate, in steps. The wait the next shot will set.</summary>
        public int FireRateSteps => Gun != null ? Gun.FireRateSteps : Core.Table.VulcanBaseRateSteps;

        /// <summary>GML: played_spinup. True from the first shot until release or running dry.</summary>
        public bool IsSpinning => Gun != null && Gun.IsSpinning;

        /// <summary>True while the fire loop (sfx_vulcan_loop) is running.</summary>
        public bool IsLoopPlaying => Gun != null && Gun.IsLoopPlaying;

        public int BaseRateSteps => Core.Table.VulcanBaseRateSteps;
        public int MinRateSteps => Core.Table.VulcanMinRateSteps;

        public GroundWeapon GroundKind => GroundWeapon.Vulcan;
        /// <summary>The vulcan only ever answers fire_primary.</summary>
        public bool IsGroundPrimary => true;

        /// <summary>Raised for every bullet fired (after its <see cref="MuzzleFlash"/>); the cartridge hooks in here.</summary>
        public event Action<Bullet> Fired;

        /// <summary>Raised for every sound cue, whether or not a clip is assigned.</summary>
        public event Action<VulcanSound> SoundPlayed;

        void Awake() => ResetState();

        /// <summary>Awake's work, public so EditMode tests can run it (Awake never runs there).</summary>
        public void ResetState()
        {
            ship = GetComponent<PlayerShipController>();
            ammo = PlayerAmmo.Of(gameObject);
            if (loopSource == null)
            {
                loopSource = HareAudio.Route(gameObject.AddComponent<AudioSource>());
                loopSource.playOnAwake = false;
                loopSource.loop = true;
            }
            if (oneShotSource == null)
            {
                oneShotSource = HareAudio.Route(gameObject.AddComponent<AudioSource>());
                oneShotSource.playOnAwake = false;
            }
            Send(new InstallGun(Id, Key, GunType.Vulcan, WeaponSlot.Primary)); // re-installing resets the gun
            emptyClickTimer = 0f;
            if (loopSource.isPlaying) loopSource.Stop();
        }

        void Update()
        {
            var controls = Ship.Controls.Player;
            // Ctrl + left mouse is fire_secondary (the shotgun), not the primary.
            bool secondaryHeld = controls.FireSecondary.IsPressed();
            bool held = controls.FirePrimary.IsPressed() && !secondaryHeld;
            if (held && (controls.FirePrimary.WasPressedThisFrame() || !TriggerHeld)) PressTrigger();
            else if (!held && TriggerHeld) ReleaseTrigger();
            Tick(Time.deltaTime);
        }

        /// <summary>GML: global left button held (mouse1_down = true; alarm[alarm_ammo_regen] = 0).</summary>
        public void PressTrigger() => Send(new PullTrigger(Id, Key));

        /// <summary>GML: global left released. Spin down, reset the rate, re-arm ammo regen.</summary>
        public void ReleaseTrigger() => Send(new ReleaseTrigger(Id, Key));

        /// <summary>Advances the fire cooldown and fires if the trigger is held (regen is <see cref="PlayerAmmo.Tick"/>'s).</summary>
        public void Tick(float dt)
        {
            if (emptyClickTimer > 0f) emptyClickTimer -= dt;
            Send(new TickGun(Id, Key, dt));
        }

        /// <summary>GML: fire_primary's ground branch with weapon_vulcan. Returns true when a bullet was fired.</summary>
        public bool TryFire() => Send(new FireGun(Id, Key)).Accepted;

        protected override void OnPlayerEvent(PlayerEvent e)
        {
            switch (e)
            {
                case GunFired fired when fired.Key == key:
                    Fired?.Invoke(SpawnBullet());
                    break;
                case GunDryFired dry when dry.Key == key:
                    // GML: if(!audio_is_playing(sfx_empty_clip)) -- one click per clip length while held.
                    if (emptyClickTimer <= 0f)
                    {
                        Play(VulcanSound.EmptyClip);
                        emptyClickTimer = emptyClipClip != null ? emptyClipClip.length : 0.25f;
                    }
                    break;
                case VulcanCue cue when cue.Key == key:
                    Play(cue.Sound);
                    break;
            }
        }

        /// <summary>GML: shoot_vulcan. A bullet from the muzzle along the aim, jittered vertically.</summary>
        Bullet SpawnBullet()
        {
            var stats = Stats;
            float aim = ship.AimDirection;
            Vector2 heading = new Vector2(Mathf.Cos(aim * Mathf.Deg2Rad), Mathf.Sin(aim * Mathf.Deg2Rad));
            Vector2 origin = (Vector2)transform.position + heading * stats.MuzzleDistance;
            // GML: muzzleflash = instance_create(X, Y, obj_muzzleflash), image_angle = bullet.direction (X, Y: before the jitter).
            MuzzleFlash.Spawn(origin, aim);
            origin.y +=UnityEngine.Random.Range(-stats.MuzzleJitter, stats.MuzzleJitter); // GML: Y + irandom_range(-5,5)

            var go = new GameObject("VulcanBullet");
            go.transform.position = new Vector3(origin.x, origin.y, transform.position.z);
            // shoot_vulcan: bullet.sprite_index = player.bullet_asset (the B key's choice, U.HARE-T93) at obj_bullet's depth -3.
            HareArt.Show(go, Core.Look.BulletSprite, -3);
            var bullet = go.AddComponent<Bullet>();
            bullet.Init(gameObject, aim, Gml.Px(stats.BulletSpeed) * Gml.StepsPerSecond, Core.Table.BulletDamage(GunType.Vulcan), stats.BulletLifetime, stats.BulletRadius);

            // GML: cartridge = instance_create(player.gunX, player.gunY, obj_cartridge) (U.HARE-T23).
            Pickups.PickupDirector.EjectCasing(Pickups.ScrapKind.Cartridge, GunPoint(transform));
            return bullet;
        }

        /// <summary>GML gunX/gunY: <c>x + lengthdir_x(-13, image_angle)</c>, likewise y.</summary>
        public static Vector2 GunPoint(Transform ship) =>
            (Vector2)ship.position + Gml.Dir(ship.eulerAngles.z) * Gml.Px(GameContent.Current.WeaponPhysicals.PerchedMuzzle.GunOffsetPx);

        void Play(VulcanSound sound)
        {
            switch (sound)
            {
                case VulcanSound.LoopStart:
                    if (loopClip != null && Application.isPlaying) { loopSource.clip = loopClip; loopSource.Play(); }
                    break;
                case VulcanSound.LoopStop:
                    loopSource.Stop();
                    break;
                default:
                    PlayOneShot(sound switch
                    {
                        VulcanSound.SpinUp => spinUpClip,
                        VulcanSound.SpinDown => spinDownClip,
                        VulcanSound.EmptyClip => emptyClipClip,
                        _ => null
                    });
                    break;
            }
            SoundPlayed?.Invoke(sound);
        }

        void PlayOneShot(AudioClip clip)
        {
            if (clip != null && Application.isPlaying) oneShotSource.PlayOneShot(clip);
        }

        protected override void OnDestroy()
        {
            if (key != 0 && HasSeat) Seat.Send(new RemoveGun(Id, key));
            base.OnDestroy();
        }

#if UNITY_EDITOR
        /// <summary>Fills the sound slots with the ported sfx when the component is added in the Editor.</summary>
        void Reset()
        {
            spinUpClip = HareAudio.Clip("sfx_vulcan_spinup");
            loopClip = HareAudio.Clip("sfx_vulcan_loop");
            spinDownClip = HareAudio.Clip("sfx_vulcan_spindown");
            emptyClipClip = HareAudio.Clip("sfx_empty_clip");
        }
#endif
    }
}
