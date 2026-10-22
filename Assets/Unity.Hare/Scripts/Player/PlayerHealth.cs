using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Hare.Player
{
    /// <summary>
    /// Ports obj_player's health/lives rules (gmlHARE: player_hurt, reset_player, the hit
    /// collisions and Alarms 1/4/5/6/7). Numbers are the original's, converted from steps at
    /// room_gamescreen's 120 steps/sec:
    /// <list type="bullet">
    /// <item>HP = MAXHP = 6; LIVES = 3 (globals.gml). Every hit costs exactly 1 HP.</item>
    /// <item>A hit makes the ship invulnerable for flash_time (180 steps = 1.5 s) and plays the
    /// hurt pose for 60 steps (0.5 s).</item>
    /// <item>A respawn ("justspawned") is protected for flash_time too; only rocket explosions
    /// ignore that protection, every other source respects it.</item>
    /// <item>At 0 HP: LIVES -= 1, the ship stops (state_dead) and after alarm_death_wait (320 steps)
    /// either reset_player runs (LIVES &gt; 0) or the game restarts.</item>
    /// <item>reset_player refills HP to MAXHP and flies the ship back in at flight_speed heading
    /// down (270 +/- 30 degrees) from above the playfield.</item>
    /// </list>
    /// The original only counted a hit while the source was on screen (in_view); damage sources
    /// that need that check should apply it before calling <see cref="TryTakeHit"/>. Hurt sounds,
    /// the obj_losthealth popup and the HUD hang off <see cref="Damaged"/>/<see cref="Died"/>
    /// (UH-T25, UH-T27, UH-T30).
    /// </summary>
    [RequireComponent(typeof(PlayerShipController))]
    public class PlayerHealth : MonoBehaviour
    {
        const float StepsPerSecond = 120f;

        [Header("Health (GML: HP, MAXHP, LIVES)")]
        [SerializeField] int maxHealth = 6;
        [SerializeField] int startingLives = 3;

        [Header("Timers (GML steps / 120)")]
        [SerializeField] float invulnerableSeconds = 180f / StepsPerSecond;  // flash_time, alarm_end_flash
        [SerializeField] float spawnProtectionSeconds = 180f / StepsPerSecond; // alarm_justspawned = flash_time
        [SerializeField] float hurtPoseSeconds = 60f / StepsPerSecond;       // alarm_hurting
        [SerializeField] float deathWaitSeconds = 320f / StepsPerSecond;     // alarm_death_wait
        [SerializeField] float flashCycleSeconds = 3f / StepsPerSecond;      // alarm_flashing re-arms every 3 steps

        [Header("Respawn (GML: reset_player)")]
        [Tooltip("Where the ship re-enters. Empty = the position it started the scene at.")]
        [SerializeField] Transform respawnPoint;
        [SerializeField] float respawnDirection = 270f;
        [SerializeField] float respawnDirectionJitter = 30f;

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

        PlayerShipController ship;
        SpriteRenderer[] renderers = Array.Empty<SpriteRenderer>();
        Vector2 spawnPosition;
        float invulnerableTimer;
        float spawnProtectionTimer;
        float hurtTimer;
        float deathTimer;
        float flashClock;

        public int MaxHealth => maxHealth;
        public int Health { get; private set; }
        public int Lives { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsGameOver { get; private set; }
        /// <summary>GML: invulnerable. Set by a hit, cleared when flash_time runs out.</summary>
        public bool IsInvulnerable => invulnerableTimer > 0f;
        /// <summary>GML: justspawned. Set by a (re)spawn, cleared when flash_time runs out.</summary>
        public bool IsSpawnProtected => spawnProtectionTimer > 0f;
        /// <summary>GML: hurting. Drives the *_hurt sprite pose.</summary>
        public bool IsHurting => hurtTimer > 0f;

        /// <summary>Raised after a hit that left the ship alive; the argument is the HP remaining.</summary>
        public event Action<int> Damaged;
        /// <summary>Raised when HP reaches 0; the argument is the lives remaining.</summary>
        public event Action<int> Died;
        public event Action Respawned;
        /// <summary>Raised when the last life is gone and the death wait has run out.</summary>
        public event Action GameOver;

        void Awake() => ResetState();

        /// <summary>
        /// Everything Awake does, callable from EditMode tests (see PlayerShipController.ResetState).
        /// Starts a fresh game: full HP, full lives, spawn-protected like the original's Create event.
        /// </summary>
        public void ResetState()
        {
            ship = GetComponent<PlayerShipController>();
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            spawnPosition = transform.position;
            Health = maxHealth;
            Lives = startingLives;
            IsDead = false;
            IsGameOver = false;
            invulnerableTimer = 0f;
            hurtTimer = 0f;
            deathTimer = 0f;
            spawnProtectionTimer = spawnProtectionSeconds;
            ApplyVisuals();
        }

        void Update() => Tick(Time.deltaTime);

        /// <summary>
        /// GML: the player's hit collisions (obj_bullet, obj_rocket, par_enemy, obj_rocket_explosion)
        /// followed by player_hurt. Returns false when the hit didn't land: dead, still invulnerable
        /// from the last hit, or spawn-protected (unless <paramref name="ignoresSpawnProtection"/>,
        /// which only obj_rocket_explosion does in the original).
        /// </summary>
        public bool TryTakeHit(bool ignoresSpawnProtection = false)
        {
            if (IsDead || IsGameOver) return false;
            if (IsInvulnerable) return false;
            if (IsSpawnProtected && !ignoresSpawnProtection) return false;

            invulnerableTimer = invulnerableSeconds;
            hurtTimer = hurtPoseSeconds;
            Health = Mathf.Max(0, Health - 1);

            if (Health <= 0)
                Die();
            else
                Damaged?.Invoke(Health);
            return true;
        }

        /// <summary>Advances the original's alarms by <paramref name="deltaTime"/> seconds.</summary>
        public void Tick(float deltaTime)
        {
            if (IsGameOver) return;

            invulnerableTimer = Mathf.Max(0f, invulnerableTimer - deltaTime);
            spawnProtectionTimer = Mathf.Max(0f, spawnProtectionTimer - deltaTime);
            hurtTimer = Mathf.Max(0f, hurtTimer - deltaTime);
            flashClock += deltaTime;

            if (IsDead)
            {
                deathTimer -= deltaTime;
                if (deathTimer <= 0f)
                {
                    if (Lives > 0) Respawn();
                    else EndGame();
                }
            }

            ApplyVisuals();
        }

        void Die()
        {
            Lives = Mathf.Max(0, Lives - 1);
            IsDead = true;
            hurtTimer = 0f; // player_hurt clears hurting on death
            deathTimer = deathWaitSeconds;
            ship.Die();
            Died?.Invoke(Lives);
        }

        /// <summary>GML: reset_player.</summary>
        void Respawn()
        {
            IsDead = false;
            Health = maxHealth;
            invulnerableTimer = 0f;
            hurtTimer = 0f;
            spawnProtectionTimer = spawnProtectionSeconds;

            Vector2 position = respawnPoint != null ? (Vector2)respawnPoint.position : spawnPosition;
            float jitter = UnityEngine.Random.Range(-respawnDirectionJitter, respawnDirectionJitter);
            ship.Respawn(position, respawnDirection + jitter);
            Respawned?.Invoke();
        }

        /// <summary>GML: game_restart, reached once the death wait runs out with no lives left.</summary>
        void EndGame()
        {
            IsGameOver = true;
            GameOver?.Invoke();
            if (reloadSceneOnGameOver && Application.isPlaying)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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
