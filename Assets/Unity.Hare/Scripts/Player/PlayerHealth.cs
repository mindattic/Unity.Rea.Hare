using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Hare.Content;
using Unity.Hare.Effects;

namespace Unity.Hare.Player
{
    /// <summary>
    /// The view on obj_player's health and lives (gmlHARE: player_hurt, reset_player, the hit collisions and
    /// Alarms 1/4/5/6/7). The rules live in Core's <see cref="ShipHealth"/> (numbers in <see cref="HealthTuning"/>,
    /// converted from steps at room_gamescreen's 120 steps/sec); this component forwards hits and time as commands and
    /// plays what comes back (U.HARE-T54): a hit plays sfx_hurt1..8 and raises <see cref="Damaged"/> (the arena puts
    /// obj_losthealth on its HUD); a death scatters spawn_death_particles (with sfx_death) where the ship was (the ship
    /// is not drawn for the 320-step wait); reset_player puts it at <see cref="RespawnPoint"/> (the arena's room_width / 2,
    /// just above the room) heading the way the rules chose, and plays sfx_flyover; game over runs <see cref="GameRestart"/>
    /// (the room flow's game_restart), or reloads the scene with no flow. <see cref="PlayerLook"/> draws the ship in
    /// Alarm 5's colour (<see cref="Tint"/>, <see cref="ColorOf"/>) and hides it while dead (U.HARE-T55). LIVES is the session's once the room calls <see cref="UseLives"/>. The original only counted a hit
    /// while the source was on screen (in_view); damage sources that need that check apply it before calling
    /// <see cref="TryTakeHit"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerShipController))]
    public class PlayerHealth : SeatView
    {
        /// <summary>reset_player's audio_play_sound(sfx_flyover, 1, 0).</summary>
        public const string RespawnSound = "sfx_flyover";

        /// <summary>
        /// The loaded content's health/lives tuning (AGENTS.md rule 4); no longer an Inspector field. Read live,
        /// not cached at construction, so a component built before <see cref="ContentBootstrap"/> runs still picks
        /// up the loaded file once it does.
        /// </summary>
        static HealthTuning Tuning => GameContent.Current.Health;

        [Header("Respawn (GML: reset_player)")]
        [Tooltip("Where the ship re-enters. Empty = the position it started the scene at.")]
        [SerializeField] Transform respawnPoint;

        [Header("Game over (GML: game_restart)")]
        [SerializeField] bool reloadSceneOnGameOver = true;

        Vector2 spawnPosition;

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
        /// <summary>GML: sprite_index, the pose the ship draws (spr_rabbit_leap_hurt after a hit in flight...).</summary>
        public string Pose => Core.Pose;
        /// <summary>GML: color, Alarm 5's blend while invulnerable.</summary>
        public Color Tint => ColorOf(Core.Health.Color);

        /// <summary>
        /// Where reset_player flies the ship back in from; null for the position it started the scene at. The arena
        /// (U.HARE-T52) sets it to its <c>(room_width / 2, -sprite_height)</c>.
        /// </summary>
        public Transform RespawnPoint
        {
            get => respawnPoint;
            set => respawnPoint = value;
        }

        /// <summary>
        /// game_restart as the game plays it (the arena hands it the room flow's <c>RestartGame</c>); null reloads the
        /// scene, for a room played without the flow.
        /// </summary>
        public Action GameRestart { get; set; }

        /// <summary>Raised after a hit that left the ship alive; the argument is the HP remaining.</summary>
        public event Action<int> Damaged;
        /// <summary>Raised when HP reaches 0; the argument is the lives remaining.</summary>
        public event Action<int> Died;
        public event Action Respawned;
        /// <summary>Raised when the last life is gone and the death wait has run out.</summary>
        public event Action GameOver;

        void Awake() => ResetState();

        /// <summary>
        /// Everything Awake does, callable from EditMode tests. Starts a fresh ship: full HP, spawn-protected like the
        /// original's Create event (its own LIVES back at the start; the session's are left alone).
        /// </summary>
        public void ResetState()
        {
            spawnPosition = transform.position;
            Send(new ResetHealth(Id, Tuning));
        }

        /// <summary>Counts LIVES with <paramref name="ledger"/> (the session's); null goes back to the ship's own.</summary>
        public void UseLives(ILivesLedger ledger) => Send(new UseLives(Id, ledger));

        void Update() => Tick(Time.deltaTime);

        /// <summary>
        /// GML: the player's hit collisions (obj_bullet, obj_rocket, par_enemy, obj_rocket_explosion) followed by
        /// player_hurt. Returns false when the hit didn't land: dead, still invulnerable from the last hit, or
        /// spawn-protected (unless <paramref name="ignoresSpawnProtection"/>, which only obj_rocket_explosion does
        /// in the original). <paramref name="reaction"/> is how the hit moves the ship first (U.HARE-T53): by default a
        /// flying ship spins, except for a source that ignores spawn protection (obj_rocket_explosion); obj_rocket also
        /// flings a ship perched on a rock.
        /// </summary>
        public bool TryTakeHit(bool ignoresSpawnProtection = false, HitReaction? reaction = null) =>
            Send(new TakeHit(Id, ignoresSpawnProtection, reaction)).Accepted;

        /// <summary>
        /// obj_player's Step past the buffer zone of an edge that does not loop (the arena's
        /// <see cref="Arena.PlayerOutOfBounds"/>): a life goes and reset_player flies the ship back in, or the game
        /// restarts with none left. False while dead or once the game is over.
        /// </summary>
        public bool LoseLifeOutOfBounds() => Send(new LeaveArena(Id)).Accepted;

        /// <summary>Advances the original's alarms by <paramref name="deltaTime"/> seconds.</summary>
        public void Tick(float deltaTime)
        {
            if (IsGameOver) return;
            Send(new TickHealth(Id, deltaTime));
        }

        protected override void OnPlayerEvent(PlayerEvent e)
        {
            switch (e)
            {
                case PlayerDamaged damaged:
                    HareArt.Play("sfx_hurt" + damaged.HurtSound);
                    Damaged?.Invoke(damaged.Health);
                    break;
                case PlayerDied died:
                    // player_hurt: spawn_death_particles(player.x, player.y) (it plays sfx_death).
                    if (Application.isPlaying) DeathParticle.SpawnDeathParticles(transform.position);
                    Died?.Invoke(died.Lives);
                    break;
                case PlayerRespawned _:
                    // GML reset_player: x = room_width / 2, y = -sprite_height (the rules chose the heading), sfx_flyover.
                    Vector2 position = respawnPoint != null ? (Vector2)respawnPoint.position : spawnPosition;
                    transform.position = new Vector3(position.x, position.y, transform.position.z);
                    var body = GetComponent<Rigidbody2D>();
                    if (body != null) body.position = position;
                    HareArt.Play(RespawnSound);
                    Respawned?.Invoke();
                    break;
                case GameEnded _:
                    GameOver?.Invoke();
                    if (!Application.isPlaying) break;
                    if (GameRestart != null) GameRestart();
                    else if (reloadSceneOnGameOver) SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                    break;
            }
        }

        /// <summary>globals.gml's colour constants (c_pink, c_amber, c_cornflowerblue).</summary>
        public static Color ColorOf(ShipColor color)
        {
            switch (color)
            {
                case ShipColor.Pink: return new Color32(255, 192, 203, 255);
                case ShipColor.Amber: return new Color32(255, 191, 0, 255);
                case ShipColor.CornflowerBlue: return new Color32(154, 206, 235, 255);
                default: return Color.white;
            }
        }
    }
}
