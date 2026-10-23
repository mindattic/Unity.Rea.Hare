using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Hare.Player;

namespace Unity.Hare.Weapons
{
    /// <summary>The vulcan's sound cues, in the order the original plays them (the reloads are <see cref="PlayerAmmo.Reloaded"/>).</summary>
    public enum VulcanSound
    {
        SpinUp,
        LoopStart,
        LoopStop,
        SpinDown,
        EmptyClip
    }

    /// <summary>
    /// Ports the vulcan (gmlHARE: fire_primary's weapon_vulcan branch, shoot_vulcan,
    /// stop_vulcan_sfx and the left-mouse held/released events of obj_player).
    ///
    /// While the trigger is held the gun fires obj_bullets at the aim. Spin-up: the first shot
    /// waits base_rate (30 steps) and every shot after it 3 steps less, down to 9 steps — the
    /// ramp the original's Rabbit build used and gmlHARE kept (commented out) beside its fixed
    /// fire_rate = 9. The spin-up sound plays on the first shot, the fire loop runs while the
    /// barrel turns, and releasing the trigger (or running dry) stops the loop, plays the
    /// spin-down and drops the rate back to base_rate (GML: stop_vulcan_sfx; fire_rate = base_rate).
    ///
    /// Ammo is <see cref="PlayerAmmo"/>'s ground primary pool (Create's 150; set_ammo_cost caps
    /// the vulcan at 120 on landing), 1 per shot (subtract_ground_ammo). An empty gun clicks
    /// (sfx_empty_clip) instead of firing. Holding the trigger stops regeneration; releasing it
    /// re-arms the regen alarm, which then adds 10 rounds every second with the reload sounds.
    ///
    /// The vulcan is a ground weapon: like fire_primary it fires only while perched (Idle), never
    /// while dead or shielding. Steps are room_gamescreen's 120 per second.
    /// </summary>
    [RequireComponent(typeof(PlayerShipController))]
    public class VulcanWeapon : MonoBehaviour, IGroundWeapon
    {
        public const float StepsPerSecond = 120f;

        [Header("Fire rate in GML steps (base_rate, spin-up floor and decrement)")]
        [SerializeField] int baseRateSteps = 30;
        [SerializeField] int minRateSteps = 9;
        [SerializeField] int spinUpDecrementSteps = 3;

        [Header("Bullet (obj_bullet; distances are placeholders until sprites set the world scale)")]
        [SerializeField] float bulletSpeed = 8f;
        [SerializeField] float bulletLifetime = 3f;
        [SerializeField] int bulletDamage = 1;
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
        float cooldown;
        float emptyClickTimer;

        /// <summary>The ship's shared ammo economy; the vulcan spends ground_ammo_primary.</summary>
        public PlayerAmmo AmmoEconomy => ammo != null ? ammo : ammo = PlayerAmmo.Of(gameObject);
        public int Ammo => AmmoEconomy.Current(AmmoPool.GroundPrimary);
        public int MaxAmmo => AmmoEconomy.Max(AmmoPool.GroundPrimary);
        public bool TriggerHeld { get; private set; }

        /// <summary>GML: can_shoot. False until the fire_rate alarm (alarm_reset_fire) runs out.</summary>
        public bool CanShoot => cooldown <= 0f;

        /// <summary>GML: fire_rate, in steps. The wait the next shot will set.</summary>
        public int FireRateSteps { get; private set; }

        /// <summary>GML: played_spinup. True from the first shot until release or running dry.</summary>
        public bool IsSpinning { get; private set; }

        /// <summary>True while the fire loop (sfx_vulcan_loop) is running.</summary>
        public bool IsLoopPlaying { get; private set; }

        public int BaseRateSteps => baseRateSteps;
        public int MinRateSteps => minRateSteps;

        public GroundWeapon GroundKind => GroundWeapon.Vulcan;
        /// <summary>The vulcan only ever answers fire_primary.</summary>
        public bool IsGroundPrimary => true;

        /// <summary>GML next_ground_weapon: can_shoot = true.</summary>
        public void Rearm() => cooldown = 0f;

        /// <summary>Switched away mid-burst: release the trigger (spin down, re-arm regen).</summary>
        public void Holster()
        {
            if (TriggerHeld) ReleaseTrigger();
        }

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
            if (TriggerHeld) ammo.ReleaseTrigger(this);
            FireRateSteps = baseRateSteps;
            cooldown = 0f;
            TriggerHeld = false;
            IsSpinning = false;
            IsLoopPlaying = false;
            emptyClickTimer = 0f;
            if (loopSource.isPlaying) loopSource.Stop();
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) PressTrigger();
                if (mouse.leftButton.wasReleasedThisFrame) ReleaseTrigger();
            }
            Tick(Time.deltaTime);
        }

        /// <summary>GML: global left button held (mouse1_down = true; alarm[alarm_ammo_regen] = 0).</summary>
        public void PressTrigger()
        {
            TriggerHeld = true;
            AmmoEconomy.PressTrigger(this);
        }

        /// <summary>GML: global left released. Spin down, reset the rate, re-arm ammo regen.</summary>
        public void ReleaseTrigger()
        {
            TriggerHeld = false;
            if (IsSpinning) SpinDown();
            FireRateSteps = baseRateSteps;
            AmmoEconomy.ReleaseTrigger(this);
        }

        /// <summary>Advances the fire cooldown and fires if the trigger is held (regen is <see cref="PlayerAmmo.Tick"/>'s).</summary>
        public void Tick(float dt)
        {
            // Snap float residue to zero so a 30-step wait is 30 steps, not 31.
            if (cooldown > 0f) cooldown = cooldown - dt < 1e-5f ? 0f : cooldown - dt;
            if (emptyClickTimer > 0f) emptyClickTimer -= dt;

            // A ship that dies mid-burst can't keep the barrel spinning.
            if (IsSpinning && ship.State == PlayerFlightState.Dead) SpinDown();

            if (TriggerHeld) TryFire();
        }

        /// <summary>
        /// GML: fire_primary's ground branch with weapon_vulcan. Returns true when a bullet was
        /// fired; an empty gun clicks and spins down instead.
        /// </summary>
        public bool TryFire()
        {
            if (!CanShoot || ship.State != PlayerFlightState.Idle || ship.IsShielding) return false;

            if (!AmmoEconomy.HasAmmo(AmmoPool.GroundPrimary))
            {
                // GML: if(!audio_is_playing(sfx_empty_clip)) -- one click per clip length while held.
                if (emptyClickTimer <= 0f)
                {
                    Play(VulcanSound.EmptyClip);
                    emptyClickTimer = emptyClipClip != null ? emptyClipClip.length : 0.25f;
                }
                if (IsSpinning) SpinDown();
                return false;
            }

            if (!IsSpinning)
            {
                IsSpinning = true;
                Play(VulcanSound.SpinUp);
            }

            var bullet = SpawnBullet();
            AmmoEconomy.Subtract(AmmoPool.GroundPrimary); // subtract_ground_ammo

            if (!IsLoopPlaying)
            {
                IsLoopPlaying = true;
                Play(VulcanSound.LoopStart);
            }

            cooldown = FireRateSteps / StepsPerSecond;
            FireRateSteps = Mathf.Max(minRateSteps, FireRateSteps - spinUpDecrementSteps);

            Fired?.Invoke(bullet);
            return true;
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
            bullet.Init(gameObject, aim, bulletSpeed, bulletDamage, bulletLifetime, bulletRadius);
            return bullet;
        }

        /// <summary>GML: stop_vulcan_sfx, plus played_spinup = false.</summary>
        void SpinDown()
        {
            IsSpinning = false;
            if (!IsLoopPlaying) return;
            IsLoopPlaying = false;
            Play(VulcanSound.LoopStop);
            Play(VulcanSound.SpinDown);
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
