using System;
using Unity.Hare.Content;

namespace Unity.Hare.Player
{
    /// <summary>
    /// obj_player's health and lives rules (gmlHARE: player_hurt, reset_player, the hit collisions and Alarms
    /// 1/4/5/6/7). HP = MAXHP = 6 and LIVES = 3; every hit costs exactly 1 HP and makes the ship invulnerable for
    /// flash_time; a respawn is protected for flash_time too, and only rocket explosions ignore that protection.
    /// At 0 HP a life goes, the ship stops, and after alarm_death_wait either reset_player runs or the game
    /// restarts. The original only counted a hit while the source was on screen (in_view); a damage source that
    /// needs that check applies it before sending the command.
    /// </summary>
    public sealed class ShipHealth
    {
        readonly EventSink sink;
        readonly ShipFlight flight;
        readonly Func<float, float, float> random;
        HealthTuning tuning = GameContent.Current.Health;
        float invulnerableTimer;
        float spawnProtectionTimer;
        float hurtTimer;
        float deathTimer;

        internal ShipHealth(EventSink sink, ShipFlight flight, Func<float, float, float> random)
        {
            this.sink = sink;
            this.flight = flight;
            this.random = random;
            Reset(tuning);
        }

        public HealthTuning Tuning => tuning;
        public int MaxHealth => tuning.MaxHealth;
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

        /// <summary>A fresh game: full HP, full lives, spawn-protected like the original's Create event.</summary>
        public void Reset(HealthTuning newTuning)
        {
            tuning = newTuning ?? GameContent.Current.Health;
            Health = tuning.MaxHealth;
            Lives = tuning.StartingLives;
            IsDead = false;
            IsGameOver = false;
            invulnerableTimer = 0f;
            hurtTimer = 0f;
            deathTimer = 0f;
            spawnProtectionTimer = GameClock.Seconds(tuning.SpawnProtectionSteps);
        }

        /// <summary>
        /// The player's hit collisions followed by player_hurt. False when the hit didn't land: dead, still
        /// invulnerable from the last hit, or spawn-protected (unless the source ignores that, which only
        /// obj_rocket_explosion does in the original).
        /// </summary>
        public bool TryTakeHit(bool ignoresSpawnProtection)
        {
            if (IsDead || IsGameOver) return false;
            if (IsInvulnerable) return false;
            if (IsSpawnProtected && !ignoresSpawnProtection) return false;

            invulnerableTimer = GameClock.Seconds(tuning.InvulnerableSteps);
            hurtTimer = GameClock.Seconds(tuning.HurtPoseSteps);
            Health = Math.Max(0, Health - 1);

            if (Health <= 0)
                Die();
            else
                sink.Emit(new PlayerDamaged { Health = Health });
            return true;
        }

        /// <summary>Advances the original's alarms by <paramref name="deltaTime"/> seconds.</summary>
        public void Tick(float deltaTime)
        {
            if (IsGameOver) return;

            invulnerableTimer = Math.Max(0f, invulnerableTimer - deltaTime);
            spawnProtectionTimer = Math.Max(0f, spawnProtectionTimer - deltaTime);
            hurtTimer = Math.Max(0f, hurtTimer - deltaTime);

            if (!IsDead) return;
            deathTimer -= deltaTime;
            if (deathTimer > 0f) return;
            if (Lives > 0) Respawn();
            else EndGame();
        }

        void Die()
        {
            Lives = Math.Max(0, Lives - 1);
            IsDead = true;
            hurtTimer = 0f; // player_hurt clears hurting on death
            deathTimer = GameClock.Seconds(tuning.DeathWaitSteps);
            flight.Die();
            sink.Emit(new PlayerDied { Lives = Lives });
        }

        /// <summary>
        /// obj_player's Step past the buffer zone of an edge that does not loop (U.HARE-T52):
        /// <c>if(LIVES &gt; 0) { LIVES -= 1; } else { game_restart(); } reset_player();</c>. With a life to spare it goes
        /// (<see cref="PlayerLeftArena"/>) and the ship is reset to fly back in (<see cref="PlayerRespawned"/>); with none
        /// the game restarts (<see cref="GameEnded"/>). Refused while dead or once the game is over.
        /// </summary>
        public bool LeaveArena()
        {
            if (IsDead || IsGameOver) return false;
            if (Lives > 0)
            {
                Lives--;
                sink.Emit(new PlayerLeftArena { Lives = Lives });
                Respawn();
            }
            else EndGame();
            return true;
        }

        /// <summary>GML: reset_player.</summary>
        void Respawn()
        {
            IsDead = false;
            Health = tuning.MaxHealth;
            invulnerableTimer = 0f;
            hurtTimer = 0f;
            spawnProtectionTimer = GameClock.Seconds(tuning.SpawnProtectionSteps);

            float direction = tuning.RespawnDirection + random(-tuning.RespawnDirectionJitter, tuning.RespawnDirectionJitter);
            flight.Respawn(direction);
            sink.Emit(new PlayerRespawned { Direction = direction });
        }

        /// <summary>GML: game_restart, reached once the death wait runs out with no lives left.</summary>
        void EndGame()
        {
            IsGameOver = true;
            sink.Emit(new GameEnded());
        }
    }
}
