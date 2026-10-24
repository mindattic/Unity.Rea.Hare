using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Hare.Player
{
    /// <summary>
    /// The view on obj_player's health and lives (gmlHARE: player_hurt, reset_player, the hit collisions and
    /// Alarms 1/4/5/6/7). The rules live in Core's <see cref="ShipHealth"/> (numbers in <see cref="HealthTuning"/>,
    /// converted from steps at room_gamescreen's 120 steps/sec); this component forwards hits and time as commands,
    /// flashes the ship while it is invulnerable, flies it back in on a respawn and restarts the scene on game over.
    /// Hurt sounds, the obj_losthealth popup and the HUD hang off <see cref="Damaged"/>/<see cref="Died"/>
    /// (UH-T25, UH-T27, UH-T30). The original only counted a hit while the source was on screen (in_view); damage
    /// sources that need that check apply it before calling <see cref="TryTakeHit"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerShipController))]
    public class PlayerHealth : SeatView
    {
        [SerializeField] HealthTuning tuning = new HealthTuning();

        [Header("Respawn (GML: reset_player)")]
        [Tooltip("Where the ship re-enters. Empty = the position it started the scene at.")]
        [SerializeField] Transform respawnPoint;

        [Header("Flash (GML Alarm 5: alarm_flashing re-arms every 3 steps)")]
        [SerializeField] float flashCycleSeconds = 3f / GameClock.StepsPerSecond;

        [Header("Game over (GML: game_restart)")]
        [SerializeField] bool reloadSceneOnGameOver = true;

        // GML colour constants from globals.gml, cycled while invulnerable (Alarm 5).
        static readonly Color[] FlashColors =
        {
            Color.white,
            new Color32(255, 192, 203, 255), // c_pink
            new Color32(255, 191, 0, 255),   // c_amber
            new Color32(154, 206, 235, 255), // c_cornflowerblue
        };

        SpriteRenderer[] renderers = Array.Empty<SpriteRenderer>();
        Vector2 spawnPosition;
        float flashClock;

        public int MaxHealth => Core.Health.MaxHealth;
        public int Health => Core.Health.Health;
        public int Lives => Core.Health.Lives;
        public bool IsDead => Core.Health.IsDead;
        public bool IsGameOver => Core.Health.IsGameOver;
        /// <summary>GML: invulnerable. Set by a hit, cleared when flash_time runs out.</summary>
        public bool IsInvulnerable => Core.Health.IsInvulnerable;
        /// <summary>GML: justspawned. Set by a (re)spawn, cleared when flash_time runs out.</summary>
        public bool IsSpawnProtected => Core.Health.IsSpawnProtected;
        /// <summary>GML: hurting. Drives the *_hurt sprite pose.</summary>
        public bool IsHurting => Core.Health.IsHurting;

        /// <summary>Raised after a hit that left the ship alive; the argument is the HP remaining.</summary>
        public event Action<int> Damaged;
        /// <summary>Raised when HP reaches 0; the argument is the lives remaining.</summary>
        public event Action<int> Died;
        public event Action Respawned;
        /// <summary>Raised when the last life is gone and the death wait has run out.</summary>
        public event Action GameOver;

        void Awake() => ResetState();

        /// <summary>
        /// Everything Awake does, callable from EditMode tests. Starts a fresh game: full HP, full lives,
        /// spawn-protected like the original's Create event.
        /// </summary>
        public void ResetState()
        {
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            spawnPosition = transform.position;
            Send(new ResetHealth(Id, tuning));
            ApplyVisuals();
        }

        void Update() => Tick(Time.deltaTime);

        /// <summary>
        /// GML: the player's hit collisions (obj_bullet, obj_rocket, par_enemy, obj_rocket_explosion) followed by
        /// player_hurt. Returns false when the hit didn't land: dead, still invulnerable from the last hit, or
        /// spawn-protected (unless <paramref name="ignoresSpawnProtection"/>, which only obj_rocket_explosion does
        /// in the original).
        /// </summary>
        public bool TryTakeHit(bool ignoresSpawnProtection = false) => Send(new TakeHit(Id, ignoresSpawnProtection)).Accepted;

        /// <summary>Advances the original's alarms by <paramref name="deltaTime"/> seconds.</summary>
        public void Tick(float deltaTime)
        {
            if (IsGameOver) return;
            flashClock += deltaTime;
            Send(new TickHealth(Id, deltaTime));
            ApplyVisuals();
        }

        protected override void OnPlayerEvent(PlayerEvent e)
        {
            switch (e)
            {
                case PlayerDamaged damaged:
                    Damaged?.Invoke(damaged.Health);
                    break;
                case PlayerDied died:
                    Died?.Invoke(died.Lives);
                    break;
                case PlayerRespawned respawned:
                    // GML reset_player: fly back in from the respawn point (the rules chose the heading).
                    Vector2 position = respawnPoint != null ? (Vector2)respawnPoint.position : spawnPosition;
                    transform.position = new Vector3(position.x, position.y, transform.position.z);
                    Respawned?.Invoke();
                    break;
                case GameEnded _:
                    GameOver?.Invoke();
                    if (reloadSceneOnGameOver && Application.isPlaying)
                        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                    break;
            }
        }

        /// <summary>
        /// The Draw event skips the ship while dead; while invulnerable Alarm 5 cycles its blend
        /// through white/pink/amber/cornflowerblue every 3 steps.
        /// </summary>
        void ApplyVisuals()
        {
            Color color = Color.white;
            if (IsInvulnerable && flashCycleSeconds > 0f)
                color = FlashColors[(int)(flashClock / flashCycleSeconds) % FlashColors.Length];

            foreach (var r in renderers)
            {
                if (r == null) continue;
                r.enabled = !IsDead;
                r.color = color;
            }
        }
    }
}
