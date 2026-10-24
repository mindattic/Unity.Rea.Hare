using System;
using UnityEngine;
using Unity.Hare.Content;
using Unity.Hare.Player;

namespace Unity.Hare.Weapons
{
    /// <summary>
    /// The view on obj_player's four ammo pools. The economy (Create's pools, set_ammo_cost, subtract_ground_ammo,
    /// the two regen alarms and the trigger's press/release handling of them) lives in Core's
    /// <see cref="AmmoPools"/>, with its numbers in <see cref="AmmoTuning"/>; every weapon spends from it through
    /// its own commands. This component runs the regen clock, plays the reload sounds and answers reads for pickups
    /// and the HUD.
    /// </summary>
    public class PlayerAmmo : SeatView
    {
        /// <summary>
        /// The loaded content's ammo economy (AGENTS.md rule 4); no longer an Inspector field. Read live, not
        /// cached at construction, so a component built before <see cref="ContentBootstrap"/> runs still picks up
        /// the loaded file once it does.
        /// </summary>
        static AmmoTuning Tuning => GameContent.Current.Ammo;

        [Header("Sound (sfx_reload_half / sfx_reload_full)")]
        [SerializeField] AudioClip reloadHalfClip;
        [SerializeField] AudioClip reloadFullClip;

        AudioSource audioSource;

        /// <summary>GML alarm[alarm_ammo_regen]: seconds left, or -1 while stopped.</summary>
        public float PrimaryRegenTimer => Core.Ammo.PrimaryRegenTimer;
        /// <summary>GML alarm[alarm_ammo_regen_secondary]: seconds left, or -1 while stopped.</summary>
        public float SecondaryRegenTimer => Core.Ammo.SecondaryRegenTimer;

        /// <summary>GML alarm 9's reload sounds: the pool that refilled and whether it reached full.</summary>
        public event Action<AmmoPool, bool> Reloaded;

        /// <summary>The ship's ammo, adding and resetting the component on first use (scenes, tests).</summary>
        public static PlayerAmmo Of(GameObject ship)
        {
            var ammo = ship.GetComponent<PlayerAmmo>();
            if (ammo == null) ammo = ship.AddComponent<PlayerAmmo>(); // Awake runs here in Play Mode
            ammo.EnsureSeated();
            return ammo;
        }

        void Awake() => ResetState();

        /// <summary>GML obj_player Create: every pool full at its starting values, both regen alarms stopped.</summary>
        public void ResetState() => Send(new ResetAmmo(Id, Tuning));

        /// <summary>Subscribes to the ship's events (the reload sounds) without resetting anything.</summary>
        void EnsureSeated() => _ = Seat;

        public int Current(AmmoPool pool) => Core.Ammo.Current(pool);
        public int Max(AmmoPool pool) => Core.Ammo.Max(pool);
        public int Cost(AmmoPool pool) => Core.Ammo.Cost(pool);
        public int Regen(AmmoPool pool) => Core.Ammo.Regen(pool);

        /// <summary>GML fire_primary/fire_secondary: x_ammo_y &gt;= x_ammo_y_cost.</summary>
        public bool HasAmmo(AmmoPool pool) => Core.Ammo.HasAmmo(pool);

        /// <summary>Tops a pool up to its maximum (pickups, upgrades).</summary>
        public void Refill(AmmoPool pool) => Send(new RefillAmmo(Id, pool));

        /// <summary>Sets a pool's count directly, clamped at 0 (pickups, upgrades).</summary>
        public void SetCurrent(AmmoPool pool, int value) => Send(new SetAmmo(Id, pool, value));

        /// <summary>GML global left pressed: stops the primary regen. Regen restarts once every holder has let go.</summary>
        public void PressTrigger(int holder = AmmoPools.DefaultHolder) => Send(new PressAmmoTrigger(Id, holder));

        /// <summary>GML global left released: both regen alarms restart with their full delay.</summary>
        public void ReleaseTrigger(int holder = AmmoPools.DefaultHolder) => Send(new ReleaseAmmoTrigger(Id, holder));

        public bool TriggerHeld => Core.Ammo.TriggerHeld;

        void Update() => Tick(Time.deltaTime);

        /// <summary>Runs the regen alarms; public so EditMode tests can drive time.</summary>
        public void Tick(float dt) => Send(new TickAmmo(Id, dt));

        protected override void OnPlayerEvent(PlayerEvent e)
        {
            if (e is AmmoReloaded reload) PlayReload(reload.Pool, reload.Full);
        }

        void PlayReload(AmmoPool pool, bool full)
        {
            if (Application.isPlaying)
            {
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                    audioSource.playOnAwake = false;
                }
                // GML: audio_stop_sound(sfx_reload_half) before sfx_reload_full; neither restarts while playing.
                if (full) audioSource.Stop();
                var clip = full ? reloadFullClip : reloadHalfClip;
                if (clip != null && !(audioSource.isPlaying && audioSource.clip == clip))
                {
                    audioSource.clip = clip;
                    audioSource.Play();
                }
            }
            Reloaded?.Invoke(pool, full);
        }

#if UNITY_EDITOR
        /// <summary>Fills the reload sounds with the ported sfx when the component is added in the Editor.</summary>
        void Reset()
        {
            const string folder = "Assets/Unity.Hare/Audio/Weapons/";
            reloadHalfClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(folder + "sfx_reload_half.wav");
            reloadFullClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(folder + "sfx_reload_full.wav");
        }
#endif
    }
}
