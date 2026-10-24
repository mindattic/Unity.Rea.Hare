using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Hare.Player;

namespace Unity.Hare.Weapons
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

        [Header("Bullet (obj_bullet; distances are placeholders until sprites set the world scale)")]
        [SerializeField] float bulletSpeed = 8f;
        [SerializeField] float bulletLifetime = 3f;
        [SerializeField] float bulletRadius = 0.05f;
        [SerializeField] float muzzleDistance = 0.34f;
        [SerializeField] float muzzleJitter = 0.05f;
        [SerializeField] Sprite bulletSprite;

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

        /// <summary>Raised for every bullet fired; VFX (muzzle flash, cartridge, UH-T31) hooks in here.</summary>
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
                loopSource = gameObject.AddComponent<AudioSource>();
                loopSource.playOnAwake = false;
                loopSource.loop = true;
            }
            if (oneShotSource == null)
            {
                oneShotSource = gameObject.AddComponent<AudioSource>();
                oneShotSource.playOnAwake = false;
            }
            Send(new InstallGun(Id, Key, GunType.Vulcan, WeaponSlot.Primary)); // re-installing resets the gun
            emptyClickTimer = 0f;
            if (loopSource.isPlaying) loopSource.Stop();
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                // Ctrl + left mouse is fire_secondary (the shotgun), not the primary.
                var keyboard = Keyboard.current;
                bool ctrl = keyboard != null && keyboard.ctrlKey.isPressed;
                bool held = mouse.leftButton.isPressed && !ctrl;
                if (held && (mouse.leftButton.wasPressedThisFrame || !TriggerHeld)) PressTrigger();
                else if (!held && TriggerHeld) ReleaseTrigger();
            }
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
            float aim = ship.AimDirection;
            Vector2 heading = new Vector2(Mathf.Cos(aim * Mathf.Deg2Rad), Mathf.Sin(aim * Mathf.Deg2Rad));
            Vector2 origin = (Vector2)transform.position + heading * muzzleDistance;
            origin.y += UnityEngine.Random.Range(-muzzleJitter, muzzleJitter); // GML: Y + irandom_range(-5,5)

            var go = new GameObject("VulcanBullet");
            go.transform.position = new Vector3(origin.x, origin.y, transform.position.z);
            if (bulletSprite != null) go.AddComponent<SpriteRenderer>().sprite = bulletSprite;
            var bullet = go.AddComponent<Bullet>();
            bullet.Init(gameObject, aim, bulletSpeed, Core.Table.BulletDamage(GunType.Vulcan), bulletLifetime, bulletRadius);
            return bullet;
        }

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
            const string folder = "Assets/Unity.Hare/Audio/Weapons/";
            AudioClip Load(string name) => UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(folder + name + ".wav");
            spinUpClip = Load("sfx_vulcan_spinup");
            loopClip = Load("sfx_vulcan_loop");
            spinDownClip = Load("sfx_vulcan_spindown");
            emptyClipClip = Load("sfx_empty_clip");
        }
#endif
    }
}
